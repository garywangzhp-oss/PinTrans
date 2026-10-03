using System.Runtime.InteropServices;

namespace HanBridge.Core.Ipc;

public static partial class RimeRefreshSignal
{
    private const byte VkF24 = 0x87;
    private const uint KeyEventKeyUp = 0x0002;

    public static void Send()
    {
        keybd_event(VkF24, 0, 0, 0);
        Thread.Sleep(10);
        keybd_event(VkF24, 0, KeyEventKeyUp, 0);
    }

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);
}