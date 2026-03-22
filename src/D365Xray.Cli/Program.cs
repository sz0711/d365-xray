using System.CommandLine;
using D365Xray.Cli;
using D365Xray.Connectors;
using D365Xray.Core;
using D365Xray.Core.Model;
using D365Xray.Diff;
using D365Xray.Reporting;
using D365Xray.Risk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Options ─────────────────────────────────────────────────
var envOption = new Option<string[]>("--env", ["-e"])
{
    Description = "Dataverse environment URL(s). Repeat for each environment.",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

var nameOption = new Option<string[]>("--name", ["-n"])
{
    Description = "Display name for each environment (same order as --env).",
    AllowMultipleArgumentsPerToken = true
};

var authOption = new Option<string>("--auth", ["-a"])
{
    Description = "Authentication method: Default, ClientSecret, Interactive, DeviceCode.",
    DefaultValueFactory = _ => "Default"
};

var tenantOption = new Option<string?>("--tenant-id")
{
    Description = "Entra ID tenant ID (required for ClientSecret)."
};

var clientIdOption = new Option<string?>("--client-id")
{
    Description = "App registration client ID (required for ClientSecret / DeviceCode)."
};

var clientSecretOption = new Option<string?>("--client-secret")
{
    Description = "Client secret (required for ClientSecret auth). Prefer env vars."
};

var outputOption = new Option<string>("--output", ["-o"])
{
    Description = "Output directory for reports.",
    DefaultValueFactory = _ => "./output"
};

var aiInstructionsOption = new Option<string?>("--ai-instructions")
{
    Description = "Path to a Markdown file with custom AI instructions. Enables optional AI enrichment."
};

// ── Scan command ────────────────────────────────────────────
var scanCommand = new Command("scan", "Scan and compare Dataverse environments.")
{
    envOption,
    nameOption,
    authOption,
    tenantOption,
    clientIdOption,
    clientSecretOption,
    outputOption,
    aiInstructionsOption
};

scanCommand.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
{
    var envUrls = parseResult.GetValue(envOption) ?? [];
    var names = parseResult.GetValue(nameOption) ?? [];
    var auth = parseResult.GetValue(authOption) ?? "Default";
    var tenantId = parseResult.GetValue(tenantOption);
    var clientId = parseResult.GetValue(clientIdOption);
    var secret = parseResult.GetValue(clientSecretOption);
    var output = parseResult.GetValue(outputOption) ?? "./output";
    var aiInstructionsPath = parseResult.GetValue(aiInstructionsOption);

    if (envUrls.Length == 0)
    {
        Console.Error.WriteLine("Error: at least one --env URL is required.");
        return ExitCodes.ConfigurationError;
    }

    if (!Enum.TryParse<AuthMethod>(auth, ignoreCase: true, out var authMethod))
    {
        Console.Error.WriteLine($"Error: unknown auth method '{auth}'. Valid: Default, ClientSecret, Interactive, DeviceCode.");
        return ExitCodes.ConfigurationError;
    }

    // Build environment configs
    var envArgs = new List<ScanEnvironmentArg>(envUrls.Length);
    for (var i = 0; i < envUrls.Length; i++)
    {
        if (!Uri.TryCreate(envUrls[i], UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            Console.Error.WriteLine($"Error: invalid environment URL '{envUrls[i]}'.");
            return ExitCodes.ConfigurationError;
        }

        var displayName = i < names.Length ? names[i] : $"Env{i + 1}";
        var config = new DataverseConnectionConfig
        {
            EnvironmentUrl = uri,
            DisplayName = displayName,
            AuthMethod = authMethod,
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = secret
        };

        envArgs.Add(new ScanEnvironmentArg { Config = config });
    }

    // Build host with DI
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Services
        .AddConnectors()
        .AddDiffEngine()
        .AddRiskScoring()
        .AddReporting()
        .AddSingleton<IAiAnalysisAdapter, NullAiAnalysisAdapter>();

    // Register per-environment keyed clients
    foreach (var envArg in envArgs)
    {
        builder.Services.AddDataverseEnvironment(envArg.Config);
    }

    using var host = builder.Build();

    var scanCmd = new ScanCommand(
        host.Services,
        host.Services.GetRequiredService<ILogger<ScanCommand>>(),
        host.Services.GetRequiredService<IAiAnalysisAdapter>());

    try
    {
        return await scanCmd.ExecuteAsync(envArgs, output, aiInstructionsPath, ct);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operation cancelled.");
        return ExitCodes.GeneralError;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Fatal error: {ex.Message}");
        return ExitCodes.GeneralError;
    }
});

// ── Root command ────────────────────────────────────────────
var rootCommand = new RootCommand("d365-xray – Deep analysis & comparison tool for Dynamics 365 / Dataverse environments")
{
    scanCommand
};

var configuration = new InvocationConfiguration();
return await rootCommand.Parse(args).InvokeAsync(configuration);
