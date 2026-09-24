using System.Runtime.InteropServices;
namespace K86LayoutLight;
internal static class QuietPolicy
{
    public static bool IsQuiet(uint now, uint lastInput, int delay)
    {
        uint elapsed = unchecked(now - lastInput);
        // Reject future/anomalous timestamps, handle the 32-bit uptime wrap.
        return elapsed <= int.MaxValue && elapsed >= (uint)delay;
    }
}
internal sealed class KeyboardActivity
{
    long last = Environment.TickCount64;
    public void Observe(uint type, long time) 
    { 
        if (type == 1) 
            Interlocked.Exchange(ref last, time); 
    }
    public bool IsQuiet(long now, int delay) 
    { 
        long elapsed = now - Interlocked.Read(ref last);
        return elapsed >= delay; 
    }
}
internal static class InputQuiet
{
    [StructLayout(LayoutKind.Sequential)] struct RawDevice { public ushort Page, Usage; public uint Flags; public IntPtr Window; }
    [StructLayout(LayoutKind.Sequential)] struct RawHeader { public uint Type, Size; public IntPtr Device, WParam; }
    [DllImport("user32.dll", SetLastError=true)] static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr handle, uint command, out RawHeader header, ref uint size, uint headerSize);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    static readonly KeyboardActivity activity = new();
    static bool registered;
    public static bool Register(IntPtr window)
    {
        registered = RegisterRawInputDevices(new[] { new RawDevice { Page=1, Usage=6, Flags=0x100, Window=window } }, 1, (uint)Marshal.SizeOf<RawDevice>());
        activity.Observe(1, Environment.TickCount64);
        return registered;
    }
    public static void Observe(IntPtr raw)
    {
        uint size = (uint)Marshal.SizeOf<RawHeader>();
        uint result = GetRawInputData(raw, 0x10000005, out var header, ref size, size);
        if (result == uint.MaxValue) 
        { 
            activity.Observe(1, Environment.TickCount64);
            return; 
        }
        activity.Observe(header.Type, Environment.TickCount64);
    }
    public static bool CanSend(int delay)
    {
        if (!registered || GetForegroundWindow() == IntPtr.Zero || !activity.IsQuiet(Environment.TickCount64, delay))
            return false;
        // VK 1,2,4,5,6 are mouse buttons: deliberately excluded.
        // Raw keyboard events include both key-down and key-up, even in background.
        for (int key = 8; key < 255; key++)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0) 
            {
                activity.Observe(1, Environment.TickCount64);
                return false; 
            }
        }
        if ((GetAsyncKeyState(3) & 0x8000) != 0) 
        { 
            activity.Observe(1, Environment.TickCount64);
            return false;
        }
        return activity.IsQuiet(Environment.TickCount64, delay);
    }
}
