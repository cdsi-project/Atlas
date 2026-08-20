using System.Runtime.InteropServices;

namespace CDSI.Agent.WinForms;

internal static class MainWindowActivator
{
    private const int RestoreWindow = 9;

    public static void RequestActivation(Form window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.IsDisposed || !window.IsHandleCreated)
        {
            return;
        }

        try
        {
            window.BeginInvoke(new Action(() => Activate(window)));
        }
        catch (InvalidOperationException)
        {
        }
    }

    internal static void Activate(Form window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.IsDisposed)
        {
            return;
        }

        if (!window.Visible)
        {
            window.Show();
        }

        if (window.WindowState == FormWindowState.Minimized)
        {
            ShowWindow(window.Handle, RestoreWindow);
        }

        window.BringToFront();
        window.Activate();
        SetForegroundWindow(window.Handle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
