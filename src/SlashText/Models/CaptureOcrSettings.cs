namespace SlashText.Models;

public sealed record CaptureOcrSettings
{
    public string Model { get; init; } = "Best";
    public string Languages { get; init; } = "por+eng";
    public string Layout { get; init; } = "Auto";
    public bool ImproveDifficultImages { get; init; } = true;

    public CaptureOcrSettings Normalize() => this with
    {
        Model = Model == "Fast" ? "Fast" : "Best",
        Languages = Languages is "por" or "eng" ? Languages : "por+eng",
        Layout = Layout is "Block" or "Sparse" ? Layout : "Auto"
    };
}
