namespace SlashText.Models;

public enum SnippetFormat
{
    Plain,
    Markdown
}

public sealed class Snippet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Trigger { get; set; } = "/";
    public string Category { get; set; } = "Geral";
    public string Content { get; set; } = string.Empty;
    public SnippetFormat Format { get; set; } = SnippetFormat.Plain;
    public bool Enabled { get; set; } = true;
    public List<string> ConfirmKeys { get; set; } = ["Enter", "Tab", "Space"];
    public bool HasLegacyIncompatibleTrigger { get; set; }

    public override string ToString() => $"{Trigger}  ·  {Name}";
}
