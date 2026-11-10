using DataverseShowcase.Plugins.CustomApis;
using DataverseShowcase.Plugins.Data;
using DataverseShowcase.Plugins.Infrastructure;
using DataverseShowcase.Plugins.Tests.Fakes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseShowcase.Plugins.Tests;

public class AccountCreditLimitGuardPluginTests
{
    [Fact]
    public void Lowering_the_limit_below_open_orders_is_blocked()
    {
        // Contoso has 42,500 on active + submitted orders (canceled and invoiced ones don't count).
        var harness = Harness(new Entity("account", SampleData.Contoso) { ["creditlimit"] = new Money(40_000m) });

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new AccountCreditLimitGuardPlugin()));

        Assert.Contains("cannot be lower than the customer's open orders (42,500.00)", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(42_500)]   // exactly the open total
    [InlineData(100_000)]  // raising
    public void Limits_at_or_above_open_orders_are_allowed(int newLimit)
    {
        var harness = Harness(new Entity("account", SampleData.Contoso) { ["creditlimit"] = new Money(newLimit) });

        harness.Run(new AccountCreditLimitGuardPlugin());
    }

    [Fact]
    public void Clearing_the_limit_is_allowed()
    {
        var harness = Harness(new Entity("account", SampleData.Contoso) { ["creditlimit"] = null });

        harness.Run(new AccountCreditLimitGuardPlugin());
    }

    [Fact]
    public void Updates_that_do_not_touch_the_limit_are_ignored()
    {
        var harness = Harness(new Entity("account", SampleData.Contoso) { ["telephone1"] = "+20 2 1234 5678" });

        harness.Run(new AccountCreditLimitGuardPlugin());

        Assert.Empty(harness.Service.Queries);
    }

    private static PluginHarness Harness(Entity target) =>
        PluginHarness.For("Update", "account", PipelineStage.PreOperation, target, SampleData.Dataverse());
}

public class GetCustomerCreditSummaryApiTests
{
    [Fact]
    public void Account_near_its_limit_gets_a_full_summary()
    {
        var harness = Call(SampleData.Contoso);

        var outputs = harness.Context.OutputParameters;
        Assert.Equal(50_000m, outputs["CreditLimit"]);
        Assert.Equal(42_500m, outputs["OpenOrdersTotal"]);
        Assert.Equal(7_500m, outputs["AvailableCredit"]);
        Assert.Equal(85.0m, outputs["UtilisationPercent"]);
        Assert.Equal("NearLimit", outputs["Status"]);
    }

    [Fact]
    public void Account_without_a_limit_reports_no_limit_and_omits_limit_outputs()
    {
        var outputs = Call(SampleData.Fabrikam).Context.OutputParameters;

        Assert.Equal("NoLimit", outputs["Status"]);
        Assert.Equal(5_000m, outputs["OpenOrdersTotal"]);
        Assert.False(outputs.Contains("CreditLimit"));
        Assert.False(outputs.Contains("AvailableCredit"));
    }

    [Fact]
    public void Account_on_hold_has_no_available_credit()
    {
        var outputs = Call(SampleData.Northwind).Context.OutputParameters;

        Assert.Equal("OnHold", outputs["Status"]);
        Assert.Equal(0m, outputs["AvailableCredit"]);
        Assert.Equal(0m, outputs["OpenOrdersTotal"]); // no orders at all: the aggregate returns no value
    }

    [Fact]
    public void Account_already_over_its_limit_is_reported_as_over_limit()
    {
        var outputs = Call(SampleData.AdventureWorks).Context.OutputParameters;

        Assert.Equal("OverLimit", outputs["Status"]);
        Assert.Equal(0m, outputs["AvailableCredit"]);
    }

    [Fact]
    public void Runs_with_the_callers_permissions()
    {
        var harness = Call(SampleData.Contoso);

        Assert.All(harness.ServicesCreatedFor, id => Assert.Equal(harness.Context.UserId, id));
    }

    [Fact]
    public void Unknown_account_is_a_clear_error()
    {
        var error = Assert.Throws<InvalidPluginExecutionException>(() => Call(Guid.NewGuid()));

        Assert.Contains("was not found", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_account_id_is_rejected()
    {
        var harness = PluginHarness.For(GetCustomerCreditSummaryApi.MessageName, string.Empty, PipelineStage.MainOperation, service: SampleData.Dataverse());

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new GetCustomerCreditSummaryApi()));

        Assert.Equal("AccountId is required.", error.Message);
    }

    private static PluginHarness Call(Guid accountId)
    {
        var harness = PluginHarness.For(GetCustomerCreditSummaryApi.MessageName, string.Empty, PipelineStage.MainOperation, service: SampleData.Dataverse());
        harness.Context.InputParameters["AccountId"] = accountId;
        harness.Run(new GetCustomerCreditSummaryApi());
        return harness;
    }
}

public class CreditRepositoryTests
{
    [Fact]
    public void Open_orders_total_is_a_fetchxml_sum_over_active_and_submitted_orders()
    {
        var fetch = CreditRepository.BuildOpenOrdersTotalFetch(SampleData.Contoso);

        Assert.Contains("aggregate=\"true\"", fetch, StringComparison.Ordinal);
        Assert.Contains("aggregate=\"sum\"", fetch, StringComparison.Ordinal);
        Assert.Contains("<value>0</value><value>1</value>", fetch, StringComparison.Ordinal);
        Assert.Contains(SampleData.Contoso.ToString("D"), fetch, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_account_returns_null_instead_of_throwing()
    {
        var repository = new CreditRepository(SampleData.Dataverse());

        Assert.Null(repository.GetAccountCredit(Guid.NewGuid()));
    }

    [Fact]
    public void Account_lookup_only_reads_the_columns_it_needs()
    {
        var service = SampleData.Dataverse();

        new CreditRepository(service).GetAccountCredit(SampleData.Contoso);

        var query = Assert.IsType<QueryExpression>(Assert.Single(service.Queries));
        Assert.Equal(["creditlimit", "creditonhold"], query.ColumnSet.Columns);
    }
}

public class PluginBaseTests
{
    [Fact]
    public void Unexpected_errors_become_a_safe_message_with_the_correlation_id()
    {
        var harness = PluginHarness.For("Create", "contact", PipelineStage.PreOperation, new Entity("contact"));

        var error = Assert.Throws<InvalidPluginExecutionException>(() => harness.Run(new ThrowingPlugin()));

        Assert.Contains(harness.Context.CorrelationId.ToString(), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret internal detail", error.Message, StringComparison.Ordinal);
        Assert.Contains(harness.TraceLog, line => line.Contains("secret internal detail"));
    }

    [Fact]
    public void Entry_and_exit_are_traced()
    {
        var target = new Entity("account", SampleData.Contoso) { ["name"] = "Contoso Ltd" };
        var harness = PluginHarness.For("Update", "account", PipelineStage.PreOperation, target, SampleData.Dataverse());

        harness.Run(new AccountCreditLimitGuardPlugin());

        Assert.StartsWith("Entered AccountCreditLimitGuardPlugin", harness.TraceLog[0], StringComparison.Ordinal);
        Assert.Equal("Exiting AccountCreditLimitGuardPlugin", harness.TraceLog[harness.TraceLog.Count - 1]);
    }

    private sealed class ThrowingPlugin() : PluginBase(null, null)
    {
        protected override void ExecuteDataversePlugin(ILocalPluginContext context) =>
            throw new InvalidOperationException("secret internal detail");
    }
}
