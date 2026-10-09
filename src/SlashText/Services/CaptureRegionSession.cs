using System.Drawing;

namespace SlashText.Services;

// Session-only handoff: the crop remains separate from its annotations.
// Exported image files still contain the flattened composition.
public sealed class CaptureRegionSession : IDisposable
{
    public Bitmap Source { get; }
    public IReadOnlyList<CaptureAnnotation> Annotations { get; }

    public CaptureRegionSession(Bitmap source, IEnumerable<CaptureAnnotation> annotations)
    {
        Source = new Bitmap(source);
        Annotations = annotations.Select(a => a.Transform(0, 0)).ToArray();
    }

    public void Dispose() => Source.Dispose();
}
