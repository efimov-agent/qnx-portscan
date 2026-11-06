using System.Reflection;
using System.Text.Json;

namespace QnxPortScan.Core.Rules;

public sealed class RuleCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public RuleCatalog(IEnumerable<Rule> rules)
    {
        Rules = rules.ToList();
        var dup = Rules.GroupBy(r => r.Id).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidOperationException($"Duplicate rule id '{dup.Key}'.");
        foreach (var r in Rules)
        {
            if (r.Patterns.Count == 0) throw new InvalidOperationException($"Rule '{r.Id}' has no patterns.");
        }
    }

    public IReadOnlyList<Rule> Rules { get; }

    public static RuleCatalog FromJson(string json) =>
        new(JsonSerializer.Deserialize<List<Rule>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Empty rule file."));

    public static async Task<RuleCatalog> FromFileAsync(string path, CancellationToken ct = default) =>
        FromJson(await File.ReadAllTextAsync(path, ct));

    public static RuleCatalog LoadBuiltIn()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("QnxPortScan.Core.Rules.rules.json")
            ?? throw new InvalidOperationException("Built-in rules resource not found.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}
