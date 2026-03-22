namespace D365Xray.Core.Model;

/// <summary>
/// Complete point-in-time capture of a single Dataverse environment.
/// This is the primary data unit flowing through the entire pipeline.
/// </summary>
public sealed record EnvironmentSnapshot
{
    public required SnapshotMetadata Metadata { get; init; }
    public required EnvironmentInfo Environment { get; init; }
    public IReadOnlyList<Solution> Solutions { get; init; } = [];
    public IReadOnlyList<SolutionComponent> Components { get; init; } = [];
    public IReadOnlyList<ComponentLayer> Layers { get; init; } = [];
    public IReadOnlyList<SolutionDependency> Dependencies { get; init; } = [];
    public IReadOnlyList<EnvironmentSetting> Settings { get; init; } = [];
}

/// <summary>
/// Versioning and provenance information for the snapshot.
/// </summary>
public sealed record SnapshotMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required string ToolVersion { get; init; }
    public TimeSpan? CapturedDuration { get; init; }
}
