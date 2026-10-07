using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SlashText.Services;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Vector = System.Windows.Vector;

internal static class CaptureCustomStampChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception e) when (e is ArgumentException or InvalidDataException or FileNotFoundException) { return; }
        throw new InvalidOperationException(message);
    }
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "slashdesk-custom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalog = new CaptureStampCatalog(Path.Combine(root, "collection"));
            var png = Path.Combine(root, "Marca ç.png");
            using (var source = new Bitmap(1200, 600, PixelFormat.Format32bppArgb))
            {
                using var g = Graphics.FromImage(source); g.Clear(Color.Transparent); g.FillRectangle(Brushes.Red, 300, 100, 600, 400);
                source.Save(png, ImageFormat.Png);
            }
            var value = catalog.Import(png); File.Delete(png);
            var reloaded = new CaptureStampCatalog(Path.Combine(root, "collection"));
            Check(reloaded.Items.Single().Value == value && reloaded.Items.Single().Name == "Marca ç", "Import survives restart and original deletion");
            var asset = reloaded.GetImage(value)!;
            using (var image = asset.CreateBitmap())
                Check(image.Width == 512 && image.Height == 256 && image.GetPixel(0, 0).A == 0, "PNG normalization preserves aspect/alpha and bounds resolution");
            using var canvas = new Bitmap(300, 200);
            using (var g = Graphics.FromImage(canvas)) g.Clear(Color.White);
            using var document = new CaptureEditorDocument(canvas);
            var annotation = new CaptureAnnotation { Kind = CaptureAnnotationKind.Stamp, Text = value, StampImage = asset, Start = new Point(150, 100), Size = 120 };
            document.AddAnnotation(annotation); document.MarkSaved();
            using var before = document.Render();
            reloaded.Remove(value);
            Check(reloaded.Items.Count == 0, "Collection removal persists");
            document.Undo(); Check(document.CanRedo, "Custom stamp participates in undo");
            document.Redo(); document.DiscardChanges();
            using var after = document.Render();
            for (var y = 0; y < after.Height; y++) for (var x = 0; x < after.Width; x++)
                Check(before.GetPixel(x, y) == after.GetPixel(x, y), "Removal must not change a document's stamp or redo/export");
            Check(after.GetPixel(150, 100).R > 240 && after.GetPixel(150, 100).G < 20 && after.GetPixel(91, 71) == Color.FromArgb(255, 255, 255, 255), "Stamp composite preserves transparent area");
            var output = Path.Combine(root, "export.png"); after.Save(output, ImageFormat.Png);
            var clipboard = CaptureService.CreateClipboardData(output).GetImage();
            var pixel = new byte[4]; clipboard.CopyPixels(new System.Windows.Int32Rect(150, 100, 1, 1), pixel, 4, 0);
            Check(pixel[2] > 240 && pixel[1] < 20, "Clipboard uses saved composition including custom image");
            var jpg = Path.Combine(root, "Foto.jpg"); canvas.Save(jpg, ImageFormat.Jpeg);
            var jpegValue = catalog.Import(jpg); Check(catalog.TryGet(jpegValue, out _), "JPEG import is supported");
            var count = catalog.Items.Count;
            var invalid = Path.Combine(root, "bad.png"); File.WriteAllText(invalid, "not an image");
            Reject(() => catalog.Import(invalid), "Invalid image rejected");
            var gif = Path.Combine(root, "animated.gif"); canvas.Save(gif, ImageFormat.Gif);
            Reject(() => catalog.Import(gif), "GIF cannot enter the static custom collection");
            Reject(() => catalog.GetImage("custom:../../other"), "Arbitrary asset path rejected");
            Check(catalog.Items.Count == count, "Failed imports do not mutate the collection");
            var transformed = annotation.Transform(-20, -10, 1.5, 1.5);
            Check(ReferenceEquals(asset, transformed.StampImage) && transformed.Start == new Point(195, 135), "Overlay transforms keep immutable custom resource");
            Geometry();
            Console.WriteLine("Custom emoji import/lifetime/clipboard and selection geometry: PASS");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    private static void Geometry()
    {
        var bounds = new Rect(-1920, -1080, 3840, 2160); var selection = new Rect(-300, -200, 400, 300);
        for (var handle = 0; handle < 8; handle++)
        {
            foreach (var delta in new[] { new Vector(-9999, -9999), new Vector(9999, 9999), new Vector(20, 15) })
            {
                var resized = CaptureSelectionGeometry.Resize(selection, handle, delta, bounds);
                Check(bounds.Contains(resized) && resized.Width >= 8 && resized.Height >= 8, "All eight handles remain inside bounds without inversion");
                if (handle is 0 or 3 or 5) Check(resized.Right == selection.Right, "West resize preserves opposite edge");
                if (handle is 2 or 4 or 7) Check(resized.Left == selection.Left, "East resize preserves opposite edge");
            }
        }
        var moved = CaptureSelectionGeometry.Move(selection, new Vector(-9999, 9999), bounds);
        Check(moved.Width == 400 && moved.Height == 300 && bounds.Contains(moved), "Movement clamps while keeping dimensions");
    }
}
