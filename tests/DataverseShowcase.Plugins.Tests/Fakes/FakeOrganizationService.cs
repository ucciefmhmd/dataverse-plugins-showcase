using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseShowcase.Plugins.Tests.Fakes;

/// <summary>
/// A small in-memory stand-in for Dataverse. It supports what the plugins use: <see cref="QueryExpression"/>
/// with Equal / NotEqual conditions and TopCount, and FetchXML with eq / in conditions and a sum aggregate.
/// Unsupported queries throw, so a test can never pass against something the fake silently ignored.
/// </summary>
public sealed class FakeOrganizationService : IOrganizationService
{
    private readonly List<Entity> _rows = [];

    public List<QueryBase> Queries { get; } = [];

    public FakeOrganizationService With(params Entity[] rows)
    {
        _rows.AddRange(rows);
        return this;
    }

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        Queries.Add(query);
        return query switch
        {
            QueryExpression expression => RunQueryExpression(expression),
            FetchExpression fetch => RunFetch(fetch.Query),
            _ => throw new NotSupportedException($"FakeOrganizationService does not support {query.GetType().Name}."),
        };
    }

    public Guid Create(Entity entity) => throw new NotSupportedException();

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

    public void Update(Entity entity) => throw new NotSupportedException();

    public void Delete(string entityName, Guid id) => throw new NotSupportedException();

    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();

    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
        throw new NotSupportedException();

    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) =>
        throw new NotSupportedException();

    private EntityCollection RunQueryExpression(QueryExpression query)
    {
        if (query.Criteria.FilterOperator != LogicalOperator.And || query.Criteria.Filters.Count > 0 || query.LinkEntities.Count > 0)
        {
            throw new NotSupportedException("Only a flat AND filter is supported.");
        }

        var matches = _rows
            .Where(r => r.LogicalName == query.EntityName)
            .Where(r => query.Criteria.Conditions.All(c => Matches(r, c)))
            .Take(query.TopCount ?? int.MaxValue)
            .Select(r => Project(r, query.ColumnSet))
            .ToList();

        return new EntityCollection(matches) { EntityName = query.EntityName };
    }

    private EntityCollection RunFetch(string fetchXml)
    {
        var fetch = XElement.Parse(fetchXml);
        var entity = fetch.Element("entity") ?? throw new NotSupportedException("FetchXML without <entity>.");
        var entityName = (string)entity.Attribute("name")!;
        var attribute = entity.Element("attribute") ?? throw new NotSupportedException("FetchXML without <attribute>.");

        if ((string?)fetch.Attribute("aggregate") != "true" || (string?)attribute.Attribute("aggregate") != "sum")
        {
            throw new NotSupportedException("Only sum aggregates are supported.");
        }

        var conditions = entity.Element("filter")?.Elements("condition").ToList() ?? [];
        var rows = _rows.Where(r => r.LogicalName == entityName && conditions.All(c => MatchesFetchCondition(r, c))).ToList();

        var attributeName = (string)attribute.Attribute("name")!;
        var alias = (string)attribute.Attribute("alias")!;
        var result = new Entity(entityName);

        // Like Dataverse, an aggregate over no rows returns no value.
        if (rows.Count > 0)
        {
            var sum = rows.Sum(r => r.GetAttributeValue<Money>(attributeName)?.Value ?? 0m);
            result[alias] = new AliasedValue(entityName, attributeName, new Money(sum));
        }

        return new EntityCollection([result]) { EntityName = entityName };
    }

    private static bool Matches(Entity row, ConditionExpression condition)
    {
        var actual = Normalize(row.Contains(condition.AttributeName) ? row[condition.AttributeName] : IdIfPrimaryKey(row, condition.AttributeName));
        var expected = condition.Values.Select(Normalize).ToList();

        return condition.Operator switch
        {
            ConditionOperator.Equal => expected.Count == 1 && Equals(actual, expected[0]),
            ConditionOperator.NotEqual => expected.Count == 1 && !Equals(actual, expected[0]),
            _ => throw new NotSupportedException($"Condition operator {condition.Operator} is not supported."),
        };
    }

    private static bool MatchesFetchCondition(Entity row, XElement condition)
    {
        var attributeName = (string)condition.Attribute("attribute")!;
        var actual = Normalize(row.Contains(attributeName) ? row[attributeName] : null)?.ToString();
        var op = (string)condition.Attribute("operator")!;

        return op switch
        {
            "eq" => string.Equals(actual, (string?)condition.Attribute("value"), StringComparison.OrdinalIgnoreCase),
            "in" => condition.Elements("value").Any(v => string.Equals(actual, v.Value, StringComparison.OrdinalIgnoreCase)),
            _ => throw new NotSupportedException($"FetchXML operator '{op}' is not supported."),
        };
    }

    /// <summary>Compares lookups by id, choices by value and money by amount, like Dataverse does in queries.</summary>
    private static object? Normalize(object? value) => value switch
    {
        EntityReference reference => reference.Id,
        OptionSetValue option => option.Value,
        Money money => money.Value,
        string text => text.ToUpperInvariant(), // Dataverse string comparisons are case-insensitive
        _ => value,
    };

    private static Guid? IdIfPrimaryKey(Entity row, string attributeName) =>
        string.Equals(attributeName, row.LogicalName + "id", StringComparison.OrdinalIgnoreCase) ? row.Id : null;

    private static Entity Project(Entity row, ColumnSet columns)
    {
        var copy = new Entity(row.LogicalName, row.Id);
        foreach (var attribute in row.Attributes.Where(a => columns.AllColumns || columns.Columns.Contains(a.Key)))
        {
            copy[attribute.Key] = attribute.Value;
        }

        return copy;
    }
}
