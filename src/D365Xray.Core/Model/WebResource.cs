namespace D365Xray.Core.Model;

/// <summary>
/// A web resource in a Dataverse environment.
/// Maps to the webresource entity.
/// </summary>
public sealed record WebResource
{
    public required Guid WebResourceId { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public required WebResourceType WebResourceType { get; init; }
    public string? SolutionUniqueName { get; init; }
    public bool IsManaged { get; init; }
    public bool IsCustomizable { get; init; } = true;
    public DateTimeOffset? ModifiedOn { get; init; }
}

/// <summary>
/// Web resource type as defined in Dataverse.
/// </summary>
public enum WebResourceType
{
    Html = 1,
    Css = 2,
    JScript = 3,
    Xml = 4,
    Png = 5,
    Jpg = 6,
    Gif = 7,
    Silverlight = 8,
    Xsl = 9,
    Ico = 10,
    Svg = 11,
    Resx = 12,
    Unknown = 0
}
