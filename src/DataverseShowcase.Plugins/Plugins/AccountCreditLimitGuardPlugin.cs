using System.Globalization;
using DataverseShowcase.Core.Credit;
using DataverseShowcase.Plugins.Data;
using DataverseShowcase.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins;

/// <summary>
/// Stops an account's credit limit from being lowered below what the customer already has on open
/// (active or submitted) sales orders. Raising or clearing the limit is always allowed.
/// <para><b>Register:</b> <c>Update</c> of <c>account</c>, stage <b>PreOperation (20)</b>, synchronous,
/// filtering attribute <c>creditlimit</c>.</para>
/// </summary>
public sealed class AccountCreditLimitGuardPlugin : PluginBase
{
    public AccountCreditLimitGuardPlugin()
        : this(null, null)
    {
    }

    public AccountCreditLimitGuardPlugin(string? unsecureConfiguration, string? secureConfiguration)
        : base(unsecureConfiguration, secureConfiguration)
    {
    }

    protected override void ExecuteDataversePlugin(ILocalPluginContext context)
    {
        context.EnsureRegistration("account", PipelineStage.PreOperation, "Update");
        var target = context.GetTargetEntity();

        // Without filtering attributes the plugin also runs for unrelated updates; ignore those cheaply.
        if (!target.Contains("creditlimit"))
        {
            return;
        }

        var newLimit = target.GetAttributeValue<Money>("creditlimit")?.Value;

        // SYSTEM context: the total must include orders the current user can't see.
        var openOrders = new CreditRepository(context.SystemService).GetOpenOrdersTotal(target.Id);
        var decision = CreditPolicy.ValidateLimitChange(newLimit, openOrders);

        context.Trace(string.Format(
            CultureInfo.InvariantCulture,
            "Credit limit change for {0}: new {1}, open orders {2:N2}, allowed {3}",
            target.Id, newLimit?.ToString("N2", CultureInfo.InvariantCulture) ?? "none", openOrders, decision.IsAllowed));

        if (!decision.IsAllowed)
        {
            throw new InvalidPluginExecutionException(decision.Reason);
        }
    }
}
