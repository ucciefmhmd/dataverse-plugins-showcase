using DataverseShowcase.Plugins.Infrastructure;
using DataverseShowcase.Plugins.Tests.Fakes;
using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.Tests;

public class ContactDataQualityPluginTests
{
    [Fact]
    public void Create_normalises_email_and_phone_numbers_in_the_target()
    {
        var target = NewContact(email: "  Karim.Nabil@Northwind.EXAMPLE ", phone: "0020 (100) 123-4567", mobile: "+20 111 222 3333");
        var harness = Harness("Create", target);

        harness.Run(new ContactDataQualityPlugin());

        Assert.Equal("karim.nabil@northwind.example", target["emailaddress1"]);
        Assert.Equal("+201001234567", target["telephone1"]);
        Assert.Equal("+201112223333", target["mobilephone"]);
    }

    [Fact]
    public void Malformed_email_is_rejected_with_a_readable_message()
    {
        var harness = Harness("Create", NewContact(email: "karim.nabil@northwind"));

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new ContactDataQualityPlugin()));

        Assert.Contains("is not a valid email address", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_phone_is_rejected()
    {
        var harness = Harness("Create", NewContact(phone: "call me maybe"));

        Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new ContactDataQualityPlugin()));
    }

    [Fact]
    public void Email_of_another_active_contact_is_rejected_regardless_of_case()
    {
        var harness = Harness("Create", NewContact(email: "SARA.AHMED@contoso.example"));

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new ContactDataQualityPlugin()));

        Assert.Contains("already uses the email address 'sara.ahmed@contoso.example'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Email_of_an_inactive_contact_can_be_reused()
    {
        var harness = Harness("Create", NewContact(email: "omar.hassan@fabrikam.example"));

        harness.Run(new ContactDataQualityPlugin());

        Assert.Equal("omar.hassan@fabrikam.example", harness.Context.InputParameters["Target"] is Entity t ? t["emailaddress1"] : null);
    }

    [Fact]
    public void Updating_a_contact_with_its_own_email_is_not_a_duplicate()
    {
        var service = SampleData.Dataverse();
        var sara = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.QueryExpression("contact")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(true),
        }).Entities.Single(c => (string)c["firstname"] == "Sara");

        var target = new Entity("contact", sara.Id) { ["emailaddress1"] = "Sara.Ahmed@contoso.example" };
        var harness = PluginHarness.For("Update", "contact", PipelineStage.PreOperation, target, service);

        harness.Run(new ContactDataQualityPlugin());

        Assert.Equal("sara.ahmed@contoso.example", target["emailaddress1"]);
    }

    [Fact]
    public void Duplicate_check_runs_as_system_so_it_sees_every_contact()
    {
        var harness = Harness("Create", NewContact(email: "new.person@contoso.example"));

        harness.Run(new ContactDataQualityPlugin());

        Assert.Contains(null, harness.ServicesCreatedFor);
    }

    [Fact]
    public void Update_without_email_or_phone_changes_does_not_query_dataverse()
    {
        var target = new Entity("contact", Guid.NewGuid()) { ["jobtitle"] = "Buyer" };
        var harness = PluginHarness.For("Update", "contact", PipelineStage.PreOperation, target, SampleData.Dataverse());

        harness.Run(new ContactDataQualityPlugin());

        Assert.Empty(harness.Service.Queries);
    }

    [Fact]
    public void Clearing_the_email_is_allowed()
    {
        var target = new Entity("contact", Guid.NewGuid()) { ["emailaddress1"] = "   " };
        var harness = PluginHarness.For("Update", "contact", PipelineStage.PreOperation, target, SampleData.Dataverse());

        harness.Run(new ContactDataQualityPlugin());

        Assert.Null(target["emailaddress1"]);
    }

    [Fact]
    public void Wrong_registration_fails_fast()
    {
        var harness = PluginHarness.For("Create", "contact", PipelineStage.PostOperation, NewContact(email: "x@y.example"));

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new ContactDataQualityPlugin()));

        Assert.Contains("registered incorrectly", error.Message, StringComparison.Ordinal);
    }

    private static PluginHarness Harness(string message, Entity target) =>
        PluginHarness.For(message, "contact", PipelineStage.PreOperation, target, SampleData.Dataverse());

    private static Entity NewContact(string? email = null, string? phone = null, string? mobile = null)
    {
        var contact = new Entity("contact", Guid.NewGuid()) { ["firstname"] = "Karim", ["lastname"] = "Nabil" };
        if (email is not null)
        {
            contact["emailaddress1"] = email;
        }

        if (phone is not null)
        {
            contact["telephone1"] = phone;
        }

        if (mobile is not null)
        {
            contact["mobilephone"] = mobile;
        }

        return contact;
    }
}
