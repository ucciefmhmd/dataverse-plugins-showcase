using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins.Tests.Fakes;

/// <summary>Runs a plugin the way the Dataverse pipeline does: through <see cref="IPlugin.Execute"/> with an <see cref="IServiceProvider"/>.</summary>
public sealed class PluginHarness : IServiceProvider, IOrganizationServiceFactory, ITracingService
{
    public PluginHarness(FakeOrganizationService service, FakeExecutionContext context)
    {
        Service = service;
        Context = context;
    }

    public FakeOrganizationService Service { get; }

    public FakeExecutionContext Context { get; }

    public List<string> TraceLog { get; } = [];

    /// <summary>The user ids services were created for; <c>null</c> means SYSTEM.</summary>
    public List<Guid?> ServicesCreatedFor { get; } = [];

    public static PluginHarness For(string message, string entity, int stage, Entity? target = null, FakeOrganizationService? service = null)
    {
        var context = new FakeExecutionContext
        {
            MessageName = message,
            PrimaryEntityName = entity,
            Stage = stage,
            PrimaryEntityId = target?.Id ?? Guid.Empty,
        };

        if (target is not null)
        {
            context.InputParameters["Target"] = target;
        }

        return new PluginHarness(service ?? new FakeOrganizationService(), context);
    }

    public void Run(IPlugin plugin)
    {
        if (plugin is null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        plugin.Execute(this);
    }

    public object? GetService(Type serviceType) =>
        serviceType == typeof(IPluginExecutionContext) ? Context
        : serviceType == typeof(IOrganizationServiceFactory) ? this
        : serviceType == typeof(ITracingService) ? this
        : null;

    public IOrganizationService CreateOrganizationService(Guid? userId)
    {
        ServicesCreatedFor.Add(userId);
        return Service;
    }

    public void Trace(string format, params object[] args) =>
        TraceLog.Add(args is { Length: > 0 } ? string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args) : format);
}

/// <summary>A settable <see cref="IPluginExecutionContext"/>.</summary>
public sealed class FakeExecutionContext : IPluginExecutionContext
{
    public int Stage { get; set; }

    public IPluginExecutionContext? ParentContext { get; set; }

    public int Mode { get; set; }

    public int IsolationMode { get; set; } = 2; // sandbox

    public int Depth { get; set; } = 1;

    public string MessageName { get; set; } = string.Empty;

    public string PrimaryEntityName { get; set; } = string.Empty;

    public Guid? RequestId { get; set; } = Guid.NewGuid();

    public string SecondaryEntityName { get; set; } = "none";

    public ParameterCollection InputParameters { get; } = [];

    public ParameterCollection OutputParameters { get; } = [];

    public ParameterCollection SharedVariables { get; } = [];

    public Guid UserId { get; set; } = Guid.NewGuid();

    public Guid InitiatingUserId { get; set; }

    public Guid BusinessUnitId { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = "contoso-test";

    public Guid PrimaryEntityId { get; set; }

    public EntityImageCollection PreEntityImages { get; } = [];

    public EntityImageCollection PostEntityImages { get; } = [];

    public EntityReference? OwningExtension { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public bool IsExecutingOffline { get; set; }

    public bool IsOfflinePlayback { get; set; }

    public bool IsInTransaction { get; set; } = true;

    public Guid OperationId { get; set; }

    public DateTime OperationCreatedOn { get; set; } = DateTime.UtcNow;
}
