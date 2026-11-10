using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.Tests;

/// <summary>
/// Fictional sample records on the standard tables, using Microsoft's usual sample company names.
/// The scenario: Contoso (credit limit 50,000) has 42,500 on open orders, which is "near the limit".
/// </summary>
public static class SampleData
{
    public static readonly Guid Contoso = new("a1f0c0de-0000-4000-8000-000000000001");
    public static readonly Guid Fabrikam = new("a1f0c0de-0000-4000-8000-000000000002");
    public static readonly Guid Northwind = new("a1f0c0de-0000-4000-8000-000000000003");
    public static readonly Guid AdventureWorks = new("a1f0c0de-0000-4000-8000-000000000004");

    /// <summary>Order states: 0 Active, 1 Submitted, 2 Canceled, 3 Fulfilled, 4 Invoiced.</summary>
    public static Entity[] Accounts() =>
    [
        Account(Contoso, "Contoso Ltd", creditLimit: 50_000m),
        Account(Fabrikam, "Fabrikam Inc", creditLimit: null),
        Account(Northwind, "Northwind Traders", creditLimit: 10_000m, onHold: true),
        Account(AdventureWorks, "Adventure Works", creditLimit: 20_000m),
    ];

    public static Entity[] Orders() =>
    [
        Order(Contoso, 30_000m, state: 0),   // active: counts
        Order(Contoso, 12_500m, state: 1),   // submitted: counts
        Order(Contoso, 9_000m, state: 2),    // canceled: ignored
        Order(Contoso, 70_000m, state: 4),   // invoiced: ignored
        Order(Fabrikam, 5_000m, state: 0),
        Order(AdventureWorks, 25_000m, state: 1), // already over its 20,000 limit
    ];

    public static Entity[] Contacts() =>
    [
        Contact("Sara", "Ahmed", "sara.ahmed@contoso.example", active: true),
        Contact("Omar", "Hassan", "omar.hassan@fabrikam.example", active: false), // inactive: its email may be reused
    ];

    /// <summary>A fresh in-memory Dataverse seeded with the sample accounts, orders and contacts.</summary>
    public static Fakes.FakeOrganizationService Dataverse() =>
        new Fakes.FakeOrganizationService().With(Accounts()).With(Orders()).With(Contacts());

    public static Entity Account(Guid id, string name, decimal? creditLimit, bool onHold = false)
    {
        var account = new Entity("account", id) { ["name"] = name, ["creditonhold"] = onHold };
        if (creditLimit is { } limit)
        {
            account["creditlimit"] = new Money(limit);
        }

        return account;
    }

    public static Entity Order(Guid customerId, decimal total, int state) =>
        new("salesorder", Guid.NewGuid())
        {
            ["customerid"] = new EntityReference("account", customerId),
            ["totalamount"] = new Money(total),
            ["statecode"] = new OptionSetValue(state),
        };

    public static Entity Contact(string firstName, string lastName, string email, bool active) =>
        new("contact", Guid.NewGuid())
        {
            ["firstname"] = firstName,
            ["lastname"] = lastName,
            ["emailaddress1"] = email,
            ["statecode"] = new OptionSetValue(active ? 0 : 1),
        };
}
