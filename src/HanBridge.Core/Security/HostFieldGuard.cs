using System.Runtime.InteropServices;

namespace HanBridge.Core.Security;

public static partial class HostFieldGuard
{
    private const int GwlStyle = -16;
    private const long EsPassword = 0x0020;

    public static unsafe bool IsPasswordFieldFocused()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var info = new GuiThreadInfo
        {
            Size = (uint)Marshal.SizeOf<GuiThreadInfo>()
        };

        if (!GetGuiThreadInfo(threadId, ref info))
        {
            return false;
        }

        var focusedWindow = info.FocusWindow != IntPtr.Zero ? info.FocusWindow : info.ActiveWindow;
        if (focusedWindow == IntPtr.Zero)
        {
            return false;
        }

        Span<char> classNameBuffer = stackalloc char[256];
        int classNameLength;
        fixed (char* classNamePointer = classNameBuffer)
        {
            classNameLength = GetClassName(focusedWindow, classNamePointer, classNameBuffer.Length);
        }

        if (classNameLength <= 0)
        {
            return false;
        }

        var className = new string(classNameBuffer[..classNameLength]);
        if (className.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        var style = GetWindowLongPtr(focusedWindow, GwlStyle).ToInt64();
        return (style & EsPassword) != 0;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetGUIThreadInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGuiThreadInfo(uint threadId, ref GuiThreadInfo info);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    private static unsafe partial int GetClassName(IntPtr windowHandle, char* className, int maximumCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public Rect CaretRectangle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}