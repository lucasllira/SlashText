namespace SlashText.Services;

public readonly record struct ExpansionStep(
    string Segment,
    bool SendTabAfter);

public static class ExpansionPlan
{
    public static IReadOnlyList<ExpansionStep> Create(string rendered)
    {
        var segments = rendered.Split(TemplateEngine.TabMarker, StringSplitOptions.None);
        return segments
            .Select((segment, index) => new ExpansionStep(segment, index < segments.Length - 1))
            .ToArray();
    }
}
