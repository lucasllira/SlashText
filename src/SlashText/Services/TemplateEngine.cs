using System.Globalization;
using System.Text.RegularExpressions;

namespace SlashText.Services;

public sealed partial class TemplateEngine
{
    private static readonly HashSet<string> AutomaticNames =
        new(StringComparer.OrdinalIgnoreCase) { "data", "hora", "datahora" };

    public IReadOnlyList<TemplateField> GetFillableFields(string template)
    {
        var fields = new Dictionary<string, TemplateField>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in VariablePattern().Matches(template))
        {
            var expression = match.Groups["expression"].Value.Trim();
            var (name, argument) = Split(expression, '|');

            if (AutomaticNames.Contains(name) || name.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fields.TryAdd(name, new TemplateField(name, argument));
        }

        return fields.Values.ToList();
    }

    public string Render(
        string template,
        IReadOnlyDictionary<string, string>? values = null,
        DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.Now;
        values ??= new Dictionary<string, string>();

        return VariablePattern().Replace(template, match =>
        {
            var expression = match.Groups["expression"].Value.Trim();
            return Resolve(expression, values, reference);
        });
    }

    private static string Resolve(
        string expression,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset now)
    {
        var (token, fallbackOrFormat) = Split(expression, '|');

        if (token.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "dd/MM/yyyy", CultureInfo.CurrentCulture);
        }

        if (token.Equals("hora", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "HH:mm", CultureInfo.CurrentCulture);
        }

        if (token.Equals("datahora", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        }

        if (token.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var calculated = ApplyDateOffset(now, token[5..]);
            return calculated.ToString(fallbackOrFormat ?? "dd/MM/yyyy", CultureInfo.CurrentCulture);
        }

        return TryGetValue(values, token, out var value) ? value : fallbackOrFormat ?? string.Empty;
    }

    private static DateTimeOffset ApplyDateOffset(DateTimeOffset value, string offset)
    {
        var match = DateOffsetPattern().Match(offset);
        if (!match.Success || !int.TryParse(match.Groups["amount"].Value, out var amount))
        {
            return value;
        }

        return match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "d" => value.AddDays(amount),
            "m" => value.AddMonths(amount),
            "y" => value.AddYears(amount),
            _ => value
        };
    }

    private static bool TryGetValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string value)
    {
        foreach (var item in values)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static (string Name, string? Argument) Split(string expression, char separator)
    {
        var index = expression.IndexOf(separator);
        return index < 0
            ? (expression.Trim(), null)
            : (expression[..index].Trim(), expression[(index + 1)..].Trim());
    }

    [GeneratedRegex(@"\{\{(?<expression>[^{}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"^(?<amount>[+-]\d+)(?<unit>[dmy])$", RegexOptions.IgnoreCase)]
    private static partial Regex DateOffsetPattern();
}

public sealed record TemplateField(string Name, string? DefaultValue);

