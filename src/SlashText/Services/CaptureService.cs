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
    private readonly CaptureHistoryStore _historyStore =
        new(AppPaths.CaptureHistoryFile);
    private List<CaptureRecord> _history = [];

    public IReadOnlyList<CaptureRecord> History => _history;
    public string ResolveFilePath(CaptureRecord record) =>
        CapturePathResolver.Resolve(record, AppPaths.Current);
    public async Task LoadAsync()
    {
        _history = await _historyStore.LoadAsync();
        foreach (var record in _history)
        {
            if (string.IsNullOrWhiteSpace(record.Id))
            {
                record.Id = Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrWhiteSpace(record.MediaKind))
            {
                record.MediaKind = Path.GetExtension(ResolveFilePath(record))
                    .Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                    ? "video"
                    : Path.GetExtension(ResolveFilePath(record))
                        .Equals(".gif", StringComparison.OrdinalIgnoreCase)
                        ? "gif"
                        : "image";
            }
        }
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
        var window = WindowUnderCursorHandle();
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

    public IntPtr WindowUnderCursorHandle()
    {
        if (!GetCursorPos(out var point))
        {
            return IntPtr.Zero;
        }
        return GetAncestor(WindowFromPoint(point), 2);
    }

    public RecordingTarget ActiveMonitorTarget()
    {
        var bounds = ActiveMonitorBounds();
        var screen = System.Windows.Forms.Screen.FromRectangle(bounds);
        return new RecordingTarget(
            RecordingTargetKind.Monitor,
            bounds,
            DisplayDeviceName: screen.DeviceName);
    }

    public RecordingTarget? WindowUnderCursorTarget()
    {
        var handle = WindowUnderCursorHandle();
        var bounds = WindowUnderCursorBounds();
        return handle == IntPtr.Zero || bounds is null
            ? null
            : new RecordingTarget(RecordingTargetKind.Window, bounds.Value, handle);
    }

    public RecordingTarget? SelectRecordingRegion(Window? owner, string purpose)
    {
        var selector = new RegionSelectionWindow(purpose);
        if (owner is not null)
        {
            selector.Owner = owner;
        }
        if (selector.ShowDialog() != true)
        {
            return null;
        }
        var bounds = selector.SelectedBounds;
        var screen = System.Windows.Forms.Screen.FromRectangle(bounds);
        return new RecordingTarget(
            RecordingTargetKind.Region,
            bounds,
            DisplayDeviceName: screen.DeviceName);
    }

    public Bitmap? SelectAndEditRegion(Window? owner, bool includeCursor = false)
    {
        var selector = new RegionCaptureWindow(includeCursor);
        if (owner is not null)
        {
            selector.Owner = owner;
        }
        if (selector.ShowDialog() != true)
        {
            return null;
        }

        return selector.EditedBitmap;
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

        using var captured = CaptureBitmap(bounds, settings.IncludeCursor);
        return await ProcessBitmapAsync(
            captured,
            type,
            settings,
            openEditor,
            owner,
            initialTool);
    }

    public async Task AddMediaRecordAsync(CaptureRecord record)
    {
        record.PortableRelativePath ??=
            CapturePathResolver.CreatePortableRelativePath(record.FilePath, AppPaths.Current);
        _history.Insert(0, record);
        if (_history.Count > 1000)
        {
            _history.RemoveRange(1000, _history.Count - 1000);
        }
        await _historyStore.SaveAsync(_history);
    }

    public async Task<bool> DeleteAsync(string id, bool deleteFile)
    {
        var record = _history.FirstOrDefault(item => item.Id == id);
        if (record is null)
        {
            return false;
        }
        if (deleteFile &&
            !string.IsNullOrWhiteSpace(ResolveFilePath(record)) &&
            File.Exists(ResolveFilePath(record)))
        {
            File.Delete(ResolveFilePath(record));
        }
        _history.Remove(record);
        await _historyStore.SaveAsync(_history);
        return true;
    }

    public async Task<int> CleanOlderThanAsync(int days, bool deleteFiles)
    {
        if (days <= 0)
        {
            return 0;
        }
        var threshold = DateTimeOffset.Now.AddDays(-days);
        var expired = _history.Where(item => item.CreatedAt < threshold).ToList();
        foreach (var record in expired)
        {
            if (deleteFiles &&
                !string.IsNullOrWhiteSpace(ResolveFilePath(record)) &&
                File.Exists(ResolveFilePath(record)))
            {
                try
                {
                    File.Delete(ResolveFilePath(record));
                }
                catch (IOException)
                {
                    // A entrada é removida mesmo quando outro aplicativo mantém o arquivo aberto.
                }
            }
            _history.Remove(record);
        }
        if (expired.Count > 0)
        {
            await _historyStore.SaveAsync(_history);
        }
        return expired.Count;
    }

    public async Task<bool> EditExistingAsync(
        string id,
        CaptureSettings settings,
        Window owner)
    {
        var record = _history.FirstOrDefault(item => item.Id == id);
        if (record is null ||
            !record.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(ResolveFilePath(record)) ||
            !File.Exists(ResolveFilePath(record)))
        {
            return false;
        }

        var resolvedPath = ResolveFilePath(record);
        using var sourceFile = new Bitmap(resolvedPath);
        using var source = new Bitmap(sourceFile);
        var editor = new CaptureEditorWindow(source)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (editor.ShowDialog() != true || editor.EditedBitmap is null)
        {
            return false;
        }

        using var edited = editor.EditedBitmap;
        var extension = Path.GetExtension(resolvedPath);
        var temporary = resolvedPath + ".editing";
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            var encoder = ImageCodecInfo.GetImageEncoders()
                .First(item => item.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality,
                Math.Clamp(settings.JpegQuality, 1, 100));
            edited.Save(temporary, encoder, parameters);
        }
        else
        {
            edited.Save(temporary, ImageFormat.Png);
        }
        File.Move(temporary, resolvedPath, true);
        record.Width = edited.Width;
        record.Height = edited.Height;
        await _historyStore.SaveAsync(_history);
        return true;
    }

    public static void CopyFileToClipboard(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("O arquivo não está mais disponível.", path);
        }
        var collection = new System.Collections.Specialized.StringCollection
        {
            path
        };
        Clipboard.SetFileDropList(collection);
    }

    public async Task<CaptureRecord?> ProcessEditedRegionAsync(
        Bitmap captured,
        string type,
        CaptureSettings settings)
    {
        return await ProcessBitmapAsync(
            captured,
            type,
            settings,
            openEditor: false,
            owner: null,
            initialTool: CaptureAnnotationKind.Arrow);
    }

    public async Task<CaptureRecord?> CaptureScrollingAsync(
        IntPtr window,
        Rectangle bounds,
        CaptureSettings settings,
        Window? owner)
    {
        if (window == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }
        SetForegroundWindow(window);
        await Task.Delay(250);
        var frames = new List<Bitmap>();
        try
        {
            const int frameCount = 5;
            for (var index = 0; index < frameCount; index++)
            {
                frames.Add(CaptureBitmap(bounds, settings.IncludeCursor && index == 0));
                if (index < frameCount - 1)
                {
                    KeybdEvent(VirtualKeyPageDown, 0, 0, UIntPtr.Zero);
                    KeybdEvent(VirtualKeyPageDown, 0, KeyEventKeyUp, UIntPtr.Zero);
                    await Task.Delay(450);
                }
            }

            var overlap = Math.Max(1, bounds.Height / 6);
            var contentHeight = bounds.Height - overlap;
            using var stitched = new Bitmap(
                bounds.Width,
                bounds.Height + contentHeight * (frames.Count - 1),
                PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(stitched))
            {
                graphics.DrawImageUnscaled(frames[0], 0, 0);
                for (var index = 1; index < frames.Count; index++)
                {
                    graphics.DrawImage(
                        frames[index],
                        new Rectangle(0, bounds.Height + contentHeight * (index - 1), bounds.Width, contentHeight),
                        new Rectangle(0, overlap, bounds.Width, contentHeight),
                        GraphicsUnit.Pixel);
                }
            }
            return await ProcessBitmapAsync(
                stitched,
                "rolagem",
                settings,
                openEditor: true,
                owner,
                CaptureAnnotationKind.Arrow);
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    private async Task<CaptureRecord?> ProcessBitmapAsync(
        Bitmap captured,
        string type,
        CaptureSettings settings,
        bool openEditor,
        Window? owner,
        CaptureAnnotationKind initialTool)
    {
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
                PortableRelativePath = CapturePathResolver.CreatePortableRelativePath(
                    filePath,
                    AppPaths.Current),
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

    public static Bitmap CaptureBitmap(Rectangle bounds, bool includeCursor = false)
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
        if (includeCursor)
        {
            DrawCursor(graphics, bounds);
        }
        return bitmap;
    }

    private static void DrawCursor(Graphics graphics, Rectangle bounds)
    {
        var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref info) ||
            info.Flags != CursorShowing ||
            info.Handle == IntPtr.Zero ||
            !bounds.Contains(info.Position.X, info.Position.Y))
        {
            return;
        }
        var icon = new IconInfo();
        var hotspotX = 0;
        var hotspotY = 0;
        if (GetIconInfo(info.Handle, out icon))
        {
            hotspotX = (int)icon.HotspotX;
            hotspotY = (int)icon.HotspotY;
            if (icon.ColorBitmap != IntPtr.Zero)
            {
                DeleteObject(icon.ColorBitmap);
            }
            if (icon.MaskBitmap != IntPtr.Zero)
            {
                DeleteObject(icon.MaskBitmap);
            }
        }
        var device = graphics.GetHdc();
        try
        {
            DrawIconEx(
                device,
                info.Position.X - bounds.Left - hotspotX,
                info.Position.Y - bounds.Top - hotspotY,
                info.Handle,
                0,
                0,
                0,
                IntPtr.Zero,
                3);
        }
        finally
        {
            graphics.ReleaseHdc(device);
        }
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
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CursorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(
        IntPtr device,
        int x,
        int y,
        IntPtr icon,
        int width,
        int height,
        int step,
        IntPtr brush,
        int flags);

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

    private const int CursorShowing = 1;
    private const byte VirtualKeyPageDown = 0x22;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Handle;
        public NativePoint Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
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
