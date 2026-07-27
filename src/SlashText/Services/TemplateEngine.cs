using System.Globalization;
using System.Text.RegularExpressions;

namespace SlashText.Services;

public sealed partial class TemplateEngine
{
    public const string TabMarker = "\u001FSLASHTEXT_TAB\u001F";

    private static readonly HashSet<string> AutomaticNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "hora", "datahora", "agora", "dia", "mes", "mes_nome",
        "ano", "semana", "dia_semana", "usuario", "tab"
    };

    public IReadOnlyList<TemplateField> GetFillableFields(string template)
    {
        var fields = new Dictionary<string, TemplateField>(StringComparer.CurrentCultureIgnoreCase);

        foreach (Match match in VariablePattern().Matches(template))
        {
            var expression = match.Groups["expression"].Value.Trim();
            var (token, argument) = Split(expression, '|');

            if (AutomaticNames.Contains(token) ||
                token.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fields.TryAdd(token, new TemplateField(token, argument));
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
            Resolve(match.Groups["expression"].Value.Trim(), values, reference));
    }

    private static string Resolve(
        string expression,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset now)
    {
        var (token, fallbackOrFormat) = Split(expression, '|');

        if (token.Equals("tab", StringComparison.OrdinalIgnoreCase))
        {
            return TabMarker;
        }

        if (token.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return Format(now, fallbackOrFormat, "dd/MM/yyyy");
        }

        if (token.Equals("hora", StringComparison.OrdinalIgnoreCase))
        {
            return Format(now, fallbackOrFormat, "HH:mm");
        }

        if (token.Equals("datahora", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("agora", StringComparison.OrdinalIgnoreCase))
        {
            return Format(now, fallbackOrFormat, "dd/MM/yyyy HH:mm");
        }

        if (token.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return Format(ApplyDateOffset(now, token[5..]), fallbackOrFormat, "dd/MM/yyyy");
        }

        if (token.Equals("dia", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "dd", CultureInfo.CurrentCulture);
        }

        if (token.Equals("mes", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "MM", CultureInfo.CurrentCulture);
        }

        if (token.Equals("mes_nome", StringComparison.OrdinalIgnoreCase))
        {
            return Capitalize(now.ToString(fallbackOrFormat ?? "MMMM", CultureInfo.CurrentCulture));
        }

        if (token.Equals("ano", StringComparison.OrdinalIgnoreCase))
        {
            return now.ToString(fallbackOrFormat ?? "yyyy", CultureInfo.CurrentCulture);
        }

        if (token.Equals("semana", StringComparison.OrdinalIgnoreCase))
        {
            return ISOWeek.GetWeekOfYear(now.DateTime).ToString("00", CultureInfo.CurrentCulture);
        }

        if (token.Equals("dia_semana", StringComparison.OrdinalIgnoreCase))
        {
            return Capitalize(now.ToString(fallbackOrFormat ?? "dddd", CultureInfo.CurrentCulture));
        }

        if (token.Equals("usuario", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(fallbackOrFormat)
                ? Environment.UserName
                : fallbackOrFormat;
        }

        return TryGetValue(values, token, out var value)
            ? value
            : fallbackOrFormat ?? string.Empty;
    }

    private static string Format(DateTimeOffset value, string? format, string defaultFormat)
    {
        try
        {
            return value.ToString(format ?? defaultFormat, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString(defaultFormat, CultureInfo.CurrentCulture);
        }
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
            if (item.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..];

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
