using System.Runtime.InteropServices;

namespace ScreenTextMonitor.Core;

internal static class NativeMethods
{
    public const uint IDLE_PRIORITY_CLASS = 0x00000040;
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    public const uint NORMAL_PRIORITY_CLASS = 0x00000020;

    public const uint SND_SYNC = 0x0000;
    public const uint SND_ASYNC = 0x0001;
    public const uint SND_FILENAME = 0x00020000;
    public const uint SND_NODEFAULT = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public readonly double ToSeconds() => (((ulong)dwHighDateTime << 32) | dwLowDateTime) / 1e7;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetPriorityClass(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessTimes(IntPtr hProcess, out FILETIME lpCreationTime,
        out FILETIME lpExitTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    // 与 Python winsound.Beep 同源的内核蜂鸣 API，不依赖控制台，WinForms 下也可发声。
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Beep(uint dwFreq, uint dwDuration);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
}
