using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.Infrastructure;

public interface ILocalPluginContext
{
    IPluginExecutionContext ExecutionContext { get; }

    /// <summary>Runs as the user who triggered the operation, so their security roles apply.</summary>
    IOrganizationService UserService { get; }

    /// <summary>Runs as SYSTEM. Use it only for checks that must see every record (for example duplicate detection).</summary>
    IOrganizationService SystemService { get; }

    void Trace(string message);

    /// <summary>The <c>Target</c> entity of a Create or Update message.</summary>
    Entity GetTargetEntity();

    /// <summary>Fails fast when the plugin is registered on a different message, table or stage than it was written for.</summary>
    void EnsureRegistration(string primaryEntityName, int stage, params string[] messageNames);
}

/// <summary>Plugin pipeline stages (Dataverse <c>SdkMessageProcessingStep.stage</c> values).</summary>
public static class PipelineStage
{
    public const int PreValidation = 10;
    public const int PreOperation = 20;
    public const int MainOperation = 30;
    public const int PostOperation = 40;
}

public sealed class LocalPluginContext : ILocalPluginContext
{
    private readonly ITracingService _tracing;
    private readonly IOrganizationServiceFactory _serviceFactory;
    private IOrganizationService? _userService;
    private IOrganizationService? _systemService;

    public LocalPluginContext(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        ExecutionContext = Resolve<IPluginExecutionContext>(serviceProvider);
        _tracing = Resolve<ITracingService>(serviceProvider);
        _serviceFactory = Resolve<IOrganizationServiceFactory>(serviceProvider);
    }

    public IPluginExecutionContext ExecutionContext { get; }

    public IOrganizationService UserService => _userService ??= _serviceFactory.CreateOrganizationService(ExecutionContext.UserId);

    public IOrganizationService SystemService => _systemService ??= _serviceFactory.CreateOrganizationService(null);

    public void Trace(string message) => _tracing.Trace(message);

    public Entity GetTargetEntity() =>
        ExecutionContext.InputParameters.TryGetValue("Target", out var target) && target is Entity entity
            ? entity
            : throw new InvalidPluginExecutionException($"{ExecutionContext.MessageName} did not include a Target entity.");

    public void EnsureRegistration(string primaryEntityName, int stage, params string[] messageNames)
    {
        if (messageNames is null)
        {
            throw new ArgumentNullException(nameof(messageNames));
        }

        var context = ExecutionContext;
        var messageMatches = messageNames.Any(m => string.Equals(m, context.MessageName, StringComparison.OrdinalIgnoreCase));
        if (!messageMatches || context.Stage != stage ||
            !string.Equals(context.PrimaryEntityName, primaryEntityName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPluginExecutionException(
                $"This plugin is registered incorrectly: expected {string.Join("/", messageNames)} of {primaryEntityName} at stage {stage}, " +
                $"but was called for {context.MessageName} of {context.PrimaryEntityName} at stage {context.Stage}.");
        }
    }

    private static T Resolve<T>(IServiceProvider serviceProvider) =>
        (T?)serviceProvider.GetService(typeof(T))
        ?? throw new InvalidPluginExecutionException($"The plugin pipeline did not provide {typeof(T).Name}.");
}
