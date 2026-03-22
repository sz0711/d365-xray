namespace D365Xray.Cli;

/// <summary>
/// Well-known process exit codes for CI/CD integration.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Analysis completed, risk level is Low or Medium.</summary>
    public const int Success = 0;

    /// <summary>Unhandled error during execution.</summary>
    public const int GeneralError = 1;

    /// <summary>Analysis completed but overall risk is Critical (score &gt; 75).</summary>
    public const int CriticalRisk = 2;

    /// <summary>Invalid or missing CLI arguments / configuration.</summary>
    public const int ConfigurationError = 3;
}
