using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace BrowserSwitcher.Helpers;

/// <summary>
/// 在托盘图标附近弹出 ContextMenu，避免多显示器下错位到其他屏。
/// </summary>
public static class TrayContextMenuPresenter
{
    /// <summary>
    /// 在当前光标位置（设备无关像素）打开菜单。
    /// </summary>
    /// <param name="menu">要显示的菜单。</param>
    /// <param name="owner">用于取得 DPI / 前置窗口的可选宿主。</param>
    public static void ShowNearCursor(ContextMenu menu, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(menu);

        if (!GetCursorPos(out var point))
        {
            menu.Placement = PlacementMode.MousePoint;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 0;
            menu.IsOpen = true;
            return;
        }

        var dpi = GetDpiScaleNear(point.X, point.Y, owner);
        menu.Placement = PlacementMode.AbsolutePoint;
        menu.PlacementTarget = null;
        menu.HorizontalOffset = point.X / dpi.DpiScaleX;
        menu.VerticalOffset = point.Y / dpi.DpiScaleY;
        menu.StaysOpen = false;

        if (owner != null)
        {
            var handle = new WindowInteropHelper(owner).Handle;
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        }

        menu.IsOpen = true;
    }

    private static DpiScale GetDpiScaleNear(int x, int y, Window? owner)
    {
        try
        {
            var monitor = MonitorFromPoint(
                new POINT { X = x, Y = y },
                MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero
                && GetDpiForMonitor(
                    monitor,
                    MonitorDpiType.EffectiveDpi,
                    out var dpiX,
                    out var dpiY) == 0
                && dpiX > 0
                && dpiY > 0)
            {
                return new DpiScale(dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch (EntryPointNotFoundException)
        {
            // 旧系统回退
        }

        if (owner != null)
        {
            return VisualTreeHelper.GetDpi(owner);
        }

        return new DpiScale(1.0, 1.0);
    }

    private const int MonitorDefaultToNearest = 2;

    private enum MonitorDpiType
    {
        EffectiveDpi = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);
}
