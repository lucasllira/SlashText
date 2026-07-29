using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SlashText.Models;
using SlashText.Views;

namespace SlashText.Services;

public sealed class CaptureService
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const uint MonitorDefaultToNearest = 2;
    private readonly JsonFileStore<List<CaptureRecord>> _historyStore =
        new(AppPaths.CaptureHistoryFile);
    private List<CaptureRecord> _history = [];

    public IReadOnlyList<CaptureRecord> History => _history;
    public CaptureAnnotationKind PreferredRegionTool { get; private set; } =
        CaptureAnnotationKind.Arrow;

    public async Task LoadAsync()
    {
        _history = await _historyStore.LoadAsync();
    }

    public Rectangle ActiveMonitorBounds()
    {
        var foreground = GetForegroundWindow();
        var monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref info)
            ? info.Monitor.ToRectangle()
            : System.Windows.Forms.Screen.PrimaryScreen?.Bounds ??
              new Rectangle(0, 0, 1920, 1080);
    }

    public Rectangle? WindowUnderCursorBounds()
    {
        if (!GetCursorPos(out var point))
        {
            return null;
        }

        var window = GetAncestor(WindowFromPoint(point), 2);
        if (window == IntPtr.Zero)
        {
            return null;
        }

        if (DwmGetWindowAttribute(
                window,
                DwmwaExtendedFrameBounds,
                out NativeRect rect,
                Marshal.SizeOf<NativeRect>()) != 0 &&
            !GetWindowRect(window, out rect))
        {
            return null;
        }

        return rect.ToRectangle();
    }

    public Rect? SelectRegion(Window? owner)
    {
        var selector = new RegionCaptureWindow();
        if (owner is not null)
        {
            selector.Owner = owner;
        }
        if (selector.ShowDialog() != true)
        {
            return null;
        }

        PreferredRegionTool = selector.PreferredTool;
        return selector.SelectedRegion;
    }

    public async Task<CaptureRecord?> CaptureAndProcessAsync(
        Rectangle bounds,
        string type,
        CaptureSettings settings,
        bool openEditor = false,
        Window? owner = null,
        CaptureAnnotationKind initialTool = CaptureAnnotationKind.Arrow)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        using var captured = CaptureBitmap(bounds);
        Bitmap output = captured;
        var save = settings.SaveAutomatically;
        var copy = settings.CopyToClipboard;
        try
        {
            if (openEditor)
            {
                var editor = new CaptureEditorWindow(captured, initialTool);
                if (owner is { IsVisible: true })
                {
                    editor.Owner = owner;
                    editor.WindowStartupLocation =
                        WindowStartupLocation.CenterOwner;
                }
                if (editor.ShowDialog() != true ||
                    editor.EditedBitmap is null)
                {
                    return null;
                }

                output = editor.EditedBitmap;
                switch (editor.RequestedOutput)
                {
                    case CaptureEditorOutput.Clipboard:
                        save = false;
                        copy = true;
                        break;
                    case CaptureEditorOutput.File:
                        save = true;
                        copy = false;
                        break;
                }
            }

            string filePath = string.Empty;
            if (save)
            {
                filePath = Save(output, type, settings);
            }
            if (copy)
            {
                CopyToClipboard(output);
            }

            var record = new CaptureRecord
            {
                CreatedAt = DateTimeOffset.Now,
                Type = type,
                FilePath = filePath,
                Width = output.Width,
                Height = output.Height
            };
            _history.Insert(0, record);
            if (_history.Count > 1000)
            {
                _history.RemoveRange(1000, _history.Count - 1000);
            }
            await _historyStore.SaveAsync(_history);
            return record;
        }
        finally
        {
            if (!ReferenceEquals(output, captured))
            {
                output.Dispose();
            }
        }
    }

    public static Bitmap CaptureBitmap(Rectangle bounds)
    {
        var bitmap = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            bounds.Left,
            bounds.Top,
            0,
            0,
            bounds.Size,
            CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    public static string ResolveDirectoryTemplate(string template, DateTimeOffset now)
    {
        return template
            .Replace("{year}", now.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", now.ToString("MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month-name}", now.ToString("MMMM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", now.ToString("dd"), StringComparison.OrdinalIgnoreCase);
    }

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }
        return builder.ToString().Trim().TrimEnd('.');
    }

    private static string Save(Bitmap bitmap, string type, CaptureSettings settings)
    {
        var now = DateTimeOffset.Now;
        var directory = ResolveDirectoryTemplate(
            Environment.ExpandEnvironmentVariables(settings.OutputDirectoryTemplate),
            now);
        Directory.CreateDirectory(directory);

        var app = ProcessName(GetForegroundWindow());
        var baseName = settings.FileNameTemplate
            .Replace("{date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HH-mm-ss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{type}", type, StringComparison.OrdinalIgnoreCase)
            .Replace("{app}", app, StringComparison.OrdinalIgnoreCase);
        baseName = SanitizeFileName(baseName);
        var jpeg = settings.ImageFormat.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ||
                   settings.ImageFormat.Equals("JPG", StringComparison.OrdinalIgnoreCase);
        var extension = jpeg ? ".jpg" : ".png";
        var path = UniquePath(directory, baseName, extension);

        if (!jpeg)
        {
            bitmap.Save(path, ImageFormat.Png);
            return path;
        }

        var encoder = ImageCodecInfo.GetImageEncoders()
            .First(item => item.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality,
            Math.Clamp(settings.JpegQuality, 1, 100));
        bitmap.Save(path, encoder, parameters);
        return path;
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (var index = 2; File.Exists(path); index++)
        {
            path = Path.Combine(directory, $"{name}_{index}{extension}");
        }
        return path;
    }

    private static void CopyToClipboard(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            Clipboard.SetImage(source);
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static string ProcessName(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var processId);
        try
        {
            return processId == 0
                ? "desktop"
                : System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return "desktop";
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window,
        int attribute,
        out NativeRect value,
        int size);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly Rectangle ToRectangle() =>
            Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
