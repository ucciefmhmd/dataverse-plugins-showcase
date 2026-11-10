using DataverseShowcase.Core.Credit;
using DataverseShowcase.Plugins.Data;
using DataverseShowcase.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.CustomApis;

/// <summary>
/// Implements the <c>ysm_GetCustomerCreditSummary</c> Custom API: given an account, it returns the credit
/// limit, the open orders total, the available credit, the utilisation and a status. Forms, Power Automate
/// flows and external systems get the same answer from one place, instead of re-implementing the rule.
/// <para>Definition: see <c>docs/custom-api.md</c>. The plugin is bound to the Custom API itself, so it runs
/// in the main operation stage (30).</para>
/// </summary>
public sealed class GetCustomerCreditSummaryApi : PluginBase
{
    public const string MessageName = "ysm_GetCustomerCreditSummary";

    public GetCustomerCreditSummaryApi()
        : this(null, null)
    {
    }

    public GetCustomerCreditSummaryApi(string? unsecureConfiguration, string? secureConfiguration)
        : base(unsecureConfiguration, secureConfiguration)
    {
    }

    protected override void ExecuteDataversePlugin(ILocalPluginContext context)
    {
        var execution = context.ExecutionContext;
        if (!string.Equals(execution.MessageName, MessageName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPluginExecutionException($"This plugin implements {MessageName}, not {execution.MessageName}.");
        }

        if (!execution.InputParameters.TryGetValue("AccountId", out var raw) || raw is not Guid accountId || accountId == Guid.Empty)
        {
            throw new InvalidPluginExecutionException("AccountId is required.");
        }

        // The caller's own permissions apply: users only get summaries for accounts they can read.
        var repository = new CreditRepository(context.UserService);
        var account = repository.GetAccountCredit(accountId)
            ?? throw new InvalidPluginExecutionException($"Account {accountId} was not found, or you don't have access to it.");

        var summary = CreditPolicy.Summarize(account.CreditLimit, account.OnHold, repository.GetOpenOrdersTotal(accountId));

        var outputs = execution.OutputParameters;
        outputs["OpenOrdersTotal"] = summary.OpenOrdersTotal;
        outputs["Status"] = summary.Status.ToString();
        if (summary.CreditLimit is { } limit)
        {
            outputs["CreditLimit"] = limit;
        }

        if (summary.AvailableCredit is { } available)
        {
            outputs["AvailableCredit"] = available;
        }

        if (summary.UtilisationPercent is { } utilisation)
        {
            outputs["UtilisationPercent"] = utilisation;
        }

        context.Trace($"Credit summary for {accountId}: {summary.Status}");
    }
}
