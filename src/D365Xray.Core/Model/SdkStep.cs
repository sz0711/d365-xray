namespace D365Xray.Core.Model;

/// <summary>
/// An SDK message processing step registered in a Dataverse environment.
/// Maps to the sdkmessageprocessingstep entity.
/// </summary>
public sealed record SdkStep
{
    public required Guid StepId { get; init; }
    public required string Name { get; init; }
    public string? MessageName { get; init; }
    public string? PrimaryEntity { get; init; }
    public required SdkStepStage Stage { get; init; }
    public required SdkStepMode Mode { get; init; }
    public int Rank { get; init; }
    public required bool IsDisabled { get; init; }
    public string? FilteringAttributes { get; init; }
    public string? PluginTypeName { get; init; }
    public Guid? PluginAssemblyId { get; init; }
    public string? SolutionUniqueName { get; init; }
    public string? Configuration { get; init; }
}

/// <summary>
/// The pipeline stage at which the step executes.
/// </summary>
public enum SdkStepStage
{
    PreValidation = 10,
    PreOperation = 20,
    MainOperation = 30,
    PostOperation = 40,
    Unknown = 0
}

/// <summary>
/// Execution mode of the step.
/// </summary>
public enum SdkStepMode
{
    Synchronous = 0,
    Asynchronous = 1,
    Unknown = -1
}
