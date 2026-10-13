using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using WpfClipboard = System.Windows.Clipboard;

internal static class ClipboardNativeChecks
{
    internal static async Task RunChildAsync(string root)
    {
        var output = Path.Combine(root, "isolated-clipboard");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--clipboard-native-isolated"); start.ArgumentList.Add(output);
        using var child = Process.Start(start)!;
        if (!await Task.Run(() => child.WaitForExit(30000))) { child.Kill(); throw new Exception("Own clipboard fixture child timed out"); }
        if (child.ExitCode is not (0 or 2)) throw new InvalidOperationException(File.ReadAllText(Path.Combine(output, "result.txt")));
        Console.WriteLine(File.ReadAllText(Path.Combine(output, "result.txt")));
    }

    internal static int RunIsolated(string output)
    {
        Directory.CreateDirectory(output);
        Exception? failure = null;
        var isolationReady = false;
        try
        {
            // Select a new, noninteractive window station BEFORE any WPF window.
            // Its clipboard is separate from the user's interactive clipboard.
            var station = CreateWindowStation("SlashDeskClipboardFixture-" + Guid.NewGuid().ToString("N"), 0, 0x002E, IntPtr.Zero);
            if (station == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowStation for fixture failed");
            if (!SetProcessWindowStation(station)) throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetProcessWindowStation for fixture failed");
            // Explicit creator-only DACL avoids inheriting station-specific access
            // bits as desktop rights. This applies only to our new fixture desktop.
            var descriptor = IntPtr.Zero;
            var attributesPointer = IntPtr.Zero;
            IntPtr desktop;
            try
            {
                var sid = WindowsIdentity.GetCurrent().User!.Value;
                if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;" + sid + ")", 1, out descriptor, out _)) throw new Win32Exception();
                var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
                attributesPointer = Marshal.AllocHGlobal(attributes.Length);
                Marshal.StructureToPtr(attributes, attributesPointer, false);
                desktop = CreateDesktop("Default", null, IntPtr.Zero, 0, 0x0083, attributesPointer);
            }
            finally { if (attributesPointer != IntPtr.Zero) Marshal.FreeHGlobal(attributesPointer); if (descriptor != IntPtr.Zero) LocalFree(descriptor); }
            if (desktop == IntPtr.Zero) throw new Win32Exception();
            var thread = new Thread(() =>
            {
                try
                {
                    if (!SetThreadDesktop(desktop)) throw new Win32Exception();
                    isolationReady = true;
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                    dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try { await CheckRealBackendAsync(); }
                        catch (Exception e) { failure = e; }
                        finally { dispatcher.InvokeShutdown(); }
                    }));
                    Dispatcher.Run();
                }
                catch (Exception e) { failure = e; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            GC.KeepAlive(station); GC.KeepAlive(desktop);
        }
        catch (Exception e) { failure = e; }
        // The child exits immediately; OS closes its station/desktop and native
        // fixture handles. Never switch back and write to the user's station.
        var unavailable = !isolationReady && failure is Win32Exception { NativeErrorCode: 5 };
        File.WriteAllText(Path.Combine(output, "result.txt"), unavailable ?
            "Clipboard native: UNAVAILABLE (Windows denied creation/selection of an isolated station; interactive clipboard was not accessed. Native/Win+V pilot validation remains required)." : failure?.ToString() ??
            "Clipboard native: PASS (separate noninteractive Windows station; raw flags, empty/text/rich/image restoration, concurrent copy and locked retry).");
        return unavailable ? 2 : failure is null ? 0 : 1;
    }

    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("Native clipboard: " + name); }
    private static System.Windows.DataObject Payload() => ClipboardPrivacy.CreateExpansionData("fixture\ntransient", SnippetFormat.Plain, Guid.NewGuid());
    private static async Task CheckRealBackendAsync()
    {
        WpfClipboard.Clear();
        using (var session = await ExpansionClipboardSession.CaptureAsync(new WindowsExpansionClipboard(), CancellationToken.None))
        {
            await session.PublishAsync(Payload(), CancellationToken.None);
            Check(WpfClipboard.GetText() == "fixture\ntransient", "native publication readable by WPF");
            Check(OpenClipboard(IntPtr.Zero), "read native flags");
            try
            {
                foreach (var name in new[] { ClipboardPrivacy.IncludeHistory, ClipboardPrivacy.UploadCloud })
                {
                    var bytes = ClipboardPrivacyChecks.ReadGlobal(GetClipboardData((uint)System.Windows.DataFormats.GetDataFormat(name).Id));
                    Check(bytes.Length == 4 && BitConverter.ToUInt32(bytes) == 0, "zero DWORD present in OS clipboard: " + name);
                }
            }
            finally { CloseClipboard(); }
            Check(await session.RestoreAsync() && !WpfClipboard.GetDataObject()!.GetFormats(false).Any(), "originally empty restored to empty");
        }
        foreach (var kind in new[] { "text", "rich", "image" })
        {
            var data = new System.Windows.DataObject(); data.SetText("prior fixture");
            var html = HtmlClipboardFormatter.Create("<b>prior fixture</b>");
            if (kind == "rich") { data.SetData(System.Windows.DataFormats.Html, html); data.SetData(System.Windows.DataFormats.Rtf, "{\\rtf1 prior fixture}"); }
            if (kind == "image") data.SetImage(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)0xA3, 16).ToArray(), 8));
            WpfClipboard.SetDataObject(data, true);
            byte[]? previousPixels = null;
            if (kind == "image") { previousPixels = new byte[16]; WpfClipboard.GetImage()!.CopyPixels(previousPixels, 8, 0); }
            using var session = await ExpansionClipboardSession.CaptureAsync(new WindowsExpansionClipboard(), CancellationToken.None);
            await session.PublishAsync(Payload(), CancellationToken.None);
            Check(await session.RestoreAsync() && WpfClipboard.GetText() == "prior fixture", "restore prior " + kind);
            if (kind == "rich") Check((string)WpfClipboard.GetData(System.Windows.DataFormats.Html)! == html, "restore HTML byte offsets");
            if (kind == "image") { var image = WpfClipboard.GetImage()!; var pixels = new byte[16]; image.CopyPixels(pixels, 8, 0); Check(image.PixelWidth == 2 && pixels.SequenceEqual(previousPixels!), "restore bitmap pixels"); }
        }
        WpfClipboard.SetText("old");
        using (var session = await ExpansionClipboardSession.CaptureAsync(new WindowsExpansionClipboard(), CancellationToken.None))
        {
            await session.PublishAsync(Payload(), CancellationToken.None); WpfClipboard.SetText("concurrent copy");
            Check(await session.RestoreAsync() && WpfClipboard.GetText() == "concurrent copy", "preserve actual concurrent OLE copy");
        }
        using (var session = await ExpansionClipboardSession.CaptureAsync(new WindowsExpansionClipboard(), CancellationToken.None))
        {
            await session.PublishAsync(Payload(), CancellationToken.None);
            using var ready = new ManualResetEventSlim(); using var proceed = new ManualResetEventSlim();
            Exception? competitorFailure = null;
            var competitor = new Thread(() =>
            {
                try
                {
                    using var window = new HwndSource(new HwndSourceParameters("FixtureClipboardCompetitor") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
                    Check(OpenClipboard(window.Handle), "competitor holds lock");
                    try
                    {
                        ready.Set(); Check(proceed.Wait(TimeSpan.FromSeconds(5)), "release competitor");
                        var bytes = Encoding.Unicode.GetBytes("copy during busy retry\0");
                        var handle = GlobalAlloc(2, (UIntPtr)bytes.Length); var pointer = GlobalLock(handle);
                        Marshal.Copy(bytes, 0, pointer, bytes.Length); GlobalUnlock(handle);
                        Check(EmptyClipboard() && SetClipboardData(13, handle) != IntPtr.Zero, "competitor publishes own copy");
                    }
                    finally { CloseClipboard(); }
                }
                catch (Exception e) { competitorFailure = e; ready.Set(); }
            });
            competitor.SetApartmentState(ApartmentState.STA); competitor.Start();
            Check(ready.Wait(TimeSpan.FromSeconds(5)), "competitor ready");
            var restore = session.RestoreAsync(); proceed.Set();
            Check(await restore, "busy restore completed"); competitor.Join();
            if (competitorFailure is not null) throw competitorFailure;
            Check(WpfClipboard.GetText() == "copy during busy retry", "new copy survives actual locked retry");
        }
        WpfClipboard.Clear();
    }
    [DllImport("user32.dll", EntryPoint = "CreateWindowStationW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowStation(string name, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetProcessWindowStation(IntPtr station);
    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateDesktop(string name, string? device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { internal int Length; internal IntPtr Descriptor; internal int Inherit; }
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr handle);
}
