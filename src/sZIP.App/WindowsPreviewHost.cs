using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace sZIP.App;

// Registered Windows preview handlers run in their COM surrogate, not inside sZIP.
public sealed class WindowsPreviewHost : WindowsFormsHost
{
    private readonly Panel _panel = new() { BackColor = System.Drawing.Color.White };
    private readonly PreviewSite _site = new();
    private object? _instance;
    private IPreviewHandler? _handler;
    private IStream? _stream;
    public string? FailureReason { get; private set; }

    public WindowsPreviewHost()
    {
        Child = _panel;
        _panel.Resize += (_, _) =>
        {
            try
            {
                if (_handler is not null)
                {
                    var rect = ClientRect();
                    _handler.SetRect(ref rect);
                }
            }
            catch (COMException) { }
        };
    }

    public bool TryShow(string path)
    {
        try
        {
            var length = 260u;
            var value = new StringBuilder((int)length);
            var previewInterface = typeof(IPreviewHandler).GUID.ToString("B");
            if (AssocQueryString(0, 16, Path.GetExtension(path), previewInterface, value, ref length) != 0
                || !Guid.TryParse(value.ToString(), out var classId)) return false;
            var unknown = new Guid("00000000-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref classId, IntPtr.Zero, 4, ref unknown, out var instance));
            _instance = instance;
            _handler = (IPreviewHandler)instance;
            if (instance is IObjectWithSite site) site.SetSite(_site);
            if (instance is IInitializeWithStream withStream)
            {
                Marshal.ThrowExceptionForHR(SHCreateStreamOnFileEx(path, 0x20, 0, false, IntPtr.Zero, out var stream));
                _stream = stream;
                withStream.Initialize(stream, 0);
            }
            else if (instance is IInitializeWithFile withFile) withFile.Initialize(path, 0);
            else { UnloadPreview(); return false; }
            var rect = ClientRect();
            _handler.SetWindow(_panel.Handle, ref rect);
            _handler.DoPreview();
            return true;
        }
        catch (Exception exception) when (exception is COMException || exception is InvalidCastException
                                         || exception is ArgumentException)
        {
            FailureReason = exception.Message;
            UnloadPreview();
            return false;
        }
    }

    private Rect ClientRect() => new() { Right = _panel.ClientSize.Width, Bottom = _panel.ClientSize.Height };

    public void UnloadPreview()
    {
        try { _handler?.Unload(); } catch (COMException) { }
        try { if (_instance is IObjectWithSite site) site.SetSite(null); } catch (COMException) { }
        _handler = null;
        if (_instance is not null && Marshal.IsComObject(_instance)) Marshal.FinalReleaseComObject(_instance);
        _instance = null;
        if (_stream is not null && Marshal.IsComObject(_stream)) Marshal.FinalReleaseComObject(_stream);
        _stream = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) UnloadPreview();
        base.Dispose(disposing);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint str, string association, string extra,
        StringBuilder output, ref uint length);
    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int CoCreateInstance(ref Guid classId, IntPtr outer, uint context,
        ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object instance);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateStreamOnFileEx(string path, uint mode, uint attributes,
        [MarshalAs(UnmanagedType.Bool)] bool create, IntPtr reserved, out IStream stream);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct FrameInfo { public IntPtr Accelerator; public uint Entries; }
    [ComImport, Guid("8895B1C6-B41F-4C1C-A562-0D564250836F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPreviewHandler
    {
        void SetWindow(IntPtr parent, ref Rect rect);
        void SetRect(ref Rect rect);
        void DoPreview();
        void Unload();
        void SetFocus();
        void QueryFocus(out IntPtr window);
        [PreserveSig] int TranslateAccelerator(ref System.Windows.Interop.MSG message);
    }
    [ComImport, Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInitializeWithStream { void Initialize(IStream stream, uint mode); }
    [ComImport, Guid("B7D14566-0509-4CCE-A71F-0A554233BD9B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInitializeWithFile { void Initialize([MarshalAs(UnmanagedType.LPWStr)] string path, uint mode); }
    [ComImport, Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectWithSite
    {
        void SetSite([MarshalAs(UnmanagedType.IUnknown)] object? site);
        void GetSite(ref Guid interfaceId, out IntPtr site);
    }
    [ComVisible(true), Guid("FEC87AAF-35F9-447A-ADB7-20234491401A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPreviewHandlerFrame
    {
        [PreserveSig] int GetWindowContext(out FrameInfo info);
        [PreserveSig] int TranslateAccelerator(ref System.Windows.Interop.MSG message);
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class PreviewSite : IPreviewHandlerFrame
    {
        public int GetWindowContext(out FrameInfo info) { info = default; return 0; }
        public int TranslateAccelerator(ref System.Windows.Interop.MSG message) => 1;
    }
}
