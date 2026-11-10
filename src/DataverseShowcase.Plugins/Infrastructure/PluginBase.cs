using System.Globalization;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.Infrastructure;

/// <summary>
/// Common plugin plumbing:
/// <list type="bullet">
/// <item>builds the per-execution context (services, tracing);</item>
/// <item>traces entry and exit with the correlation id, so a failing call can be found in the plug-in trace log;</item>
/// <item>turns unexpected exceptions into a user-safe <see cref="InvalidPluginExecutionException"/> while keeping details in the trace.</item>
/// </list>
/// Dataverse caches plugin instances and runs them concurrently, so derived classes must be stateless.
/// </summary>
public abstract class PluginBase : IPlugin
{
    protected PluginBase(string? unsecureConfiguration, string? secureConfiguration)
    {
        UnsecureConfiguration = unsecureConfiguration;
        SecureConfiguration = secureConfiguration;
    }

    protected string? UnsecureConfiguration { get; }

    protected string? SecureConfiguration { get; }

    public void Execute(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
        {
            throw new InvalidPluginExecutionException("The plugin was called without a service provider.");
        }

        var context = new LocalPluginContext(serviceProvider);
        var execution = context.ExecutionContext;
        var pluginName = GetType().Name;

        context.Trace(string.Format(
            CultureInfo.InvariantCulture,
            "Entered {0}: message {1}, table {2}, stage {3}, depth {4}, correlation {5}",
            pluginName, execution.MessageName, execution.PrimaryEntityName, execution.Stage, execution.Depth, execution.CorrelationId));

        try
        {
            ExecuteDataversePlugin(context);
        }
        catch (InvalidPluginExecutionException)
        {
            // Intended, user-facing validation messages pass through unchanged.
            throw;
        }
        catch (FaultException<OrganizationServiceFault> fault)
        {
            context.Trace($"Dataverse fault in {pluginName}: {fault.Detail?.Message ?? fault.Message}");
            throw new InvalidPluginExecutionException(
                $"{pluginName} could not complete a Dataverse operation. Correlation id: {execution.CorrelationId}.", fault);
        }
#pragma warning disable CA1031 // Last line of defence: never leak raw exception text to end users.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            context.Trace($"Unexpected error in {pluginName}: {ex}");
            throw new InvalidPluginExecutionException(
                $"An unexpected error occurred in {pluginName}. Correlation id: {execution.CorrelationId}.", ex);
        }
        finally
        {
            context.Trace($"Exiting {pluginName}");
        }
    }

    /// <summary>The plugin's actual work. Throw <see cref="InvalidPluginExecutionException"/> for messages meant for the user.</summary>
    protected abstract void ExecuteDataversePlugin(ILocalPluginContext context);
}
