using D365Xray.Core.Model;

namespace D365Xray.Risk;

/// <summary>
/// Built-in risk rules covering all Dynamics 365 finding categories.
/// Each rule assigns a base score reflecting the operational risk
/// of the finding in a typical enterprise D365 deployment.
/// </summary>
public static class DefaultRiskRules
{
    public static IReadOnlyList<RiskRule> All { get; } =
    [
        // ── Dependency conflicts ────────────────────────────────
        new RiskRule
        {
            RuleId = "R-DEP-001",
            Category = FindingCategory.DependencyConflict,
            MinimumSeverity = Severity.Critical,
            BaseScore = 95,
            Description = "A required dependency solution is missing. " +
                "This will cause import failures and may break existing functionality."
        },
        new RiskRule
        {
            RuleId = "R-DEP-002",
            Category = FindingCategory.DependencyConflict,
            MinimumSeverity = null,
            BaseScore = 60,
            Description = "A dependency conflict exists. " +
                "Review whether the dependent component can function without the required component."
        },

        // ── Layer overrides ─────────────────────────────────────
        new RiskRule
        {
            RuleId = "R-LAY-001",
            Category = FindingCategory.LayerOverride,
            MinimumSeverity = Severity.High,
            BaseScore = 80,
            Description = "An Active (unmanaged) layer overrides a managed solution component. " +
                "This blocks clean solution upgrades and causes unpredictable merge behavior."
        },
        new RiskRule
        {
            RuleId = "R-LAY-002",
            Category = FindingCategory.LayerOverride,
            MinimumSeverity = null,
            BaseScore = 50,
            Description = "A layer override exists on a component. " +
                "Consider removing the unmanaged customization or incorporating it into a managed solution."
        },

        // ── Solution drift ──────────────────────────────────────
        new RiskRule
        {
            RuleId = "R-SOL-001",
            Category = FindingCategory.SolutionDrift,
            MinimumSeverity = Severity.High,
            BaseScore = 70,
            Description = "A managed solution is installed in some environments but missing from others. " +
                "This indicates an incomplete deployment pipeline."
        },
        new RiskRule
        {
            RuleId = "R-SOL-002",
            Category = FindingCategory.SolutionDrift,
            MinimumSeverity = null,
            BaseScore = 40,
            Description = "A solution is present in some environments but not all. " +
                "Verify whether the solution is expected to be environment-specific."
        },

        // ── Version mismatch ────────────────────────────────────
        new RiskRule
        {
            RuleId = "R-VER-001",
            Category = FindingCategory.VersionMismatch,
            MinimumSeverity = Severity.Medium,
            BaseScore = 55,
            Description = "A solution has different versions across environments. " +
                "This suggests deployment drift or incomplete promotion."
        },

        // ── Missing components ──────────────────────────────────
        new RiskRule
        {
            RuleId = "R-CMP-001",
            Category = FindingCategory.MissingComponent,
            MinimumSeverity = Severity.High,
            BaseScore = 65,
            Description = "A component present in the baseline environment is missing from another environment. " +
                "This may indicate a broken or incomplete solution deployment."
        },
        new RiskRule
        {
            RuleId = "R-CMP-002",
            Category = FindingCategory.MissingComponent,
            MinimumSeverity = null,
            BaseScore = 35,
            Description = "A component is missing from a target environment. " +
                "Review whether the component was intentionally excluded."
        },

        // ── Settings drift ──────────────────────────────────────
        new RiskRule
        {
            RuleId = "R-SET-001",
            Category = FindingCategory.SettingsDrift,
            MinimumSeverity = Severity.High,
            BaseScore = 75,
            Description = "A security-related environment setting differs between environments. " +
                "Inconsistent security settings may expose production environments."
        },
        new RiskRule
        {
            RuleId = "R-SET-002",
            Category = FindingCategory.SettingsDrift,
            MinimumSeverity = null,
            BaseScore = 30,
            Description = "An environment setting differs between environments. " +
                "Verify whether the difference is intentional per-environment configuration."
        },

        // ── Security risk ───────────────────────────────────────
        new RiskRule
        {
            RuleId = "R-SEC-001",
            Category = FindingCategory.SecurityRisk,
            MinimumSeverity = Severity.High,
            BaseScore = 90,
            Description = "A high-severity security risk has been identified. " +
                "Immediate remediation is recommended."
        },
        new RiskRule
        {
            RuleId = "R-SEC-002",
            Category = FindingCategory.SecurityRisk,
            MinimumSeverity = null,
            BaseScore = 50,
            Description = "A security concern has been identified. Review and assess impact."
        },

        // ── Configuration anomaly ───────────────────────────────
        new RiskRule
        {
            RuleId = "R-CFG-001",
            Category = FindingCategory.ConfigurationAnomaly,
            MinimumSeverity = null,
            BaseScore = 40,
            Description = "An unusual configuration has been detected. " +
                "This may be intentional but warrants review."
        }
    ];
}
