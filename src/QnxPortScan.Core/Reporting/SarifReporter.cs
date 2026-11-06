using System.Text.Json;
using System.Text.Json.Nodes;
using QnxPortScan.Core.Rules;
using QnxPortScan.Core.Scanning;

namespace QnxPortScan.Core.Reporting;

/// <summary>Writes SARIF 2.1.0 so findings show up in GitHub code scanning and IDEs.</summary>
public static class SarifReporter
{
    public const string ToolName = "qnx-portscan";
    public const string InformationUri = "https://github.com/efimov-agent/qnx-portscan";

    public static string Render(ScanResult result, RuleCatalog catalog, string toolVersion)
    {
        var rules = catalog.Rules.ToList();
        var index = rules.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);

        var driverRules = new JsonArray();
        foreach (var r in rules)
        {
            var rule = new JsonObject
            {
                ["id"] = r.Id,
                ["name"] = ToPascal(r.Title),
                ["shortDescription"] = new JsonObject { ["text"] = r.Title },
                ["fullDescription"] = new JsonObject { ["text"] = r.Message },
                ["help"] = new JsonObject
                {
                    ["text"] = r.Suggestion,
                    ["markdown"] = r.Suggestion + (r.Docs.Count > 0 ? "\n\n" + string.Join("\n", r.Docs.Select(d => $"- <{d}>")) : string.Empty),
                },
                ["defaultConfiguration"] = new JsonObject { ["level"] = Level(r.Severity) },
                ["properties"] = new JsonObject { ["tags"] = new JsonArray("portability", "qnx", r.Category) },
            };
            if (r.Docs.Count > 0) rule["helpUri"] = r.Docs[0];
            driverRules.Add(rule);
        }

        var results = new JsonArray();
        foreach (var f in result.Findings)
        {
            results.Add(new JsonObject
            {
                ["ruleId"] = f.Rule.Id,
                ["ruleIndex"] = index[f.Rule.Id],
                ["level"] = Level(f.Severity),
                ["message"] = new JsonObject
                {
                    ["text"] = $"'{f.Match}': {f.Rule.Title}. {f.Rule.Suggestion}" + (f.LinuxGuarded ? " (inside a Linux-only #ifdef branch)" : string.Empty),
                },
                ["locations"] = new JsonArray(new JsonObject
                {
                    ["physicalLocation"] = new JsonObject
                    {
                        ["artifactLocation"] = new JsonObject { ["uri"] = f.File, ["uriBaseId"] = "SRCROOT" },
                        ["region"] = new JsonObject
                        {
                            ["startLine"] = f.Line,
                            ["startColumn"] = f.Column,
                            ["endColumn"] = f.Column + f.Match.Length,
                            ["snippet"] = new JsonObject { ["text"] = f.Snippet },
                        },
                    },
                }),
                ["partialFingerprints"] = new JsonObject
                {
                    ["primaryLocationLineHash"] = Fingerprint(f),
                },
            });
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray(new JsonObject
            {
                ["tool"] = new JsonObject
                {
                    ["driver"] = new JsonObject
                    {
                        ["name"] = ToolName,
                        ["version"] = toolVersion,
                        ["semanticVersion"] = toolVersion,
                        ["informationUri"] = InformationUri,
                        ["rules"] = driverRules,
                    },
                },
                ["originalUriBaseIds"] = new JsonObject
                {
                    ["SRCROOT"] = new JsonObject { ["uri"] = new Uri(EnsureTrailingSlash(result.Root)).AbsoluteUri },
                },
                ["results"] = results,
            }),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Level(Severity s) => s switch
    {
        Severity.Error => "error",
        Severity.Warning => "warning",
        _ => "note",
    };

    private static string Fingerprint(Finding f)
    {
        var data = System.Text.Encoding.UTF8.GetBytes($"{f.Rule.Id}|{f.File}|{f.Snippet}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data))[..16].ToLowerInvariant();
    }

    private static string EnsureTrailingSlash(string p) => p.EndsWith('/') ? p : p + "/";

    private static string ToPascal(string title) =>
        string.Concat(title.Split([' ', '-', '/', '(', ')', ',', '.', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
