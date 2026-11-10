using System.Globalization;
using System.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseShowcase.Plugins.Data;

/// <summary>An account's credit settings as stored in Dataverse.</summary>
public sealed class AccountCredit
{
    public AccountCredit(Guid accountId, decimal? creditLimit, bool onHold)
    {
        AccountId = accountId;
        CreditLimit = creditLimit;
        OnHold = onHold;
    }

    public Guid AccountId { get; }

    public decimal? CreditLimit { get; }

    public bool OnHold { get; }
}

/// <summary>Credit-related reads against the standard <c>account</c> and <c>salesorder</c> tables.</summary>
public sealed class CreditRepository
{
    /// <summary>Sales order states that still consume credit: Active (0) and Submitted (1).</summary>
    public static readonly int[] OpenOrderStates = [0, 1];

    private readonly IOrganizationService _service;

    public CreditRepository(IOrganizationService service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <returns>The account's credit settings, or <c>null</c> when no such account exists.</returns>
    public AccountCredit? GetAccountCredit(Guid accountId)
    {
        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("creditlimit", "creditonhold"),
            TopCount = 1,
            NoLock = true,
        };
        query.Criteria.AddCondition("accountid", ConditionOperator.Equal, accountId);

        var account = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        return account is null
            ? null
            : new AccountCredit(
                accountId,
                account.GetAttributeValue<Money>("creditlimit")?.Value,
                account.GetAttributeValue<bool>("creditonhold"));
    }

    /// <summary>Sum of <c>totalamount</c> over the customer's open sales orders, computed by Dataverse (FetchXML aggregate).</summary>
    public decimal GetOpenOrdersTotal(Guid accountId)
    {
        var result = _service.RetrieveMultiple(new FetchExpression(BuildOpenOrdersTotalFetch(accountId)));
        var total = result.Entities.FirstOrDefault()?.GetAttributeValue<AliasedValue>("open_total")?.Value;

        return total switch
        {
            Money money => money.Value,
            decimal value => value,
            _ => 0m, // no open orders: the aggregate returns no value
        };
    }

    public static string BuildOpenOrdersTotalFetch(Guid accountId)
    {
        var states = string.Concat(OpenOrderStates.Select(s => $"<value>{s.ToString(CultureInfo.InvariantCulture)}</value>"));
        return $"""
            <fetch aggregate="true">
              <entity name="salesorder">
                <attribute name="totalamount" alias="open_total" aggregate="sum" />
                <filter type="and">
                  <condition attribute="customerid" operator="eq" value="{SecurityElement.Escape(accountId.ToString("D"))}" />
                  <condition attribute="statecode" operator="in">{states}</condition>
                </filter>
              </entity>
            </fetch>
            """;
    }
}

/// <summary>Duplicate checks against the standard <c>contact</c> table.</summary>
public sealed class ContactRepository
{
    private readonly IOrganizationService _service;

    public ContactRepository(IOrganizationService service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Whether another <b>active</b> contact already uses this (normalised) email address.</summary>
    public bool IsEmailUsedByAnotherContact(string email, Guid? excludeContactId)
    {
        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet(false),
            TopCount = 1,
            NoLock = true,
        };
        query.Criteria.AddCondition("emailaddress1", ConditionOperator.Equal, email);
        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
        if (excludeContactId is { } id && id != Guid.Empty)
        {
            query.Criteria.AddCondition("contactid", ConditionOperator.NotEqual, id);
        }

        return _service.RetrieveMultiple(query).Entities.Count > 0;
    }
}
