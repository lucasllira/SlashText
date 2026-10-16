using System.Runtime.InteropServices;

namespace SlashText.Services;

// MMDevice API enumeration only: no capture, default endpoint lookup, or device changes.
// ScreenRecorderLib 7.0.1's ListAudioDevices dereferences a missing default endpoint.
internal static class WindowsAudioEndpoints
{
    internal static IReadOnlyList<RecordingAudioDevice> Enumerate(bool microphone)
    {
        var enumerator = (IDeviceEnumerator)new DeviceEnumerator();
        IDeviceCollection? collection = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(microphone ? 1 : 0, 1, out collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            var devices = new List<RecordingAudioDevice>();
            for (uint index = 0; index < count; index++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(index, out var device));
                IPropertyStore? properties = null;
                var idPointer = IntPtr.Zero;
                try
                {
                    Marshal.ThrowExceptionForHR(device.GetId(out idPointer));
                    var id = Marshal.PtrToStringUni(idPointer);
                    if (string.IsNullOrEmpty(id)) continue;
                    Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out properties));
                    var key = new PropertyKey { Format = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), Id = 14 };
                    var value = new PropertyValue();
                    try
                    {
                        Marshal.ThrowExceptionForHR(properties.GetValue(ref key, out value));
                        // PKEY_Device_FriendlyName is VT_LPWSTR, per the Windows SDK contract.
                        var name = value.Kind == 31 ? Marshal.PtrToStringUni(value.Pointer) : null;
                        devices.Add(new(id, string.IsNullOrWhiteSpace(name) ? id : name));
                    }
                    finally { PropVariantClear(ref value); }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(idPointer);
                    if (properties is not null) Marshal.ReleaseComObject(properties);
                    Marshal.ReleaseComObject(device);
                }
            }
            return devices;
        }
        finally
        {
            if (collection is not null) Marshal.ReleaseComObject(collection);
            Marshal.ReleaseComObject(enumerator);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { internal Guid Format; internal uint Id; }
    // PROPVARIANT is 24 bytes on this x64-only application (8-byte header + 16-byte union).
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropertyValue
    {
        [FieldOffset(0)] internal ushort Kind;
        [FieldOffset(8)] internal IntPtr Pointer;
    }
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PropertyValue value);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint stateMask, out IDeviceCollection collection);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, out IntPtr result);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
        [PreserveSig] int GetId(out IntPtr id);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
    }
}
