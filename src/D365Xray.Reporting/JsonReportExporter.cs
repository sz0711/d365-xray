using System.Text.Json;
using System.Text.Json.Serialization;
using D365Xray.Core.Model;

namespace D365Xray.Reporting;

/// <summary>
/// Exports a <see cref="RiskReport"/> as indented JSON (camelCase).
/// Produces <c>report.json</c> in the output directory.
/// </summary>
internal sealed class JsonReportExporter
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task ExportAsync(
        RiskReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "report.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, report, SerializerOptions, cancellationToken);
    }
}
