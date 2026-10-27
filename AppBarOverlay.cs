using System.Drawing;
using System.Runtime.InteropServices;

namespace TopMaskAppBar;

internal sealed class AppBarOverlay : Form
{
    private const uint SwpNoActivate = 0x0010;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private DisplayTarget? _target;
    private int _height;
    private bool _registered;
    private bool _layoutInProgress;
    private bool _callbackQueued;
    private NativeMethods.Rect? _lastBounds;

    public bool IsMaskActive => _registered;

    public AppBarOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        TopMost = true;
        Text = "TopMask 黑色遮罩";
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public bool StartOrUpdate(DisplayTarget target, int height, out string error)
    {
        error = string.Empty;
        if (IsDisposed)
        {
            error = "遮罩視窗已關閉，請重新啟動本程式。";
            return false;
        }

        if (_registered && !string.Equals(_target?.DeviceName, target.DeviceName, StringComparison.OrdinalIgnoreCase))
            RemoveRegistration();

        _target = target;
        _height = Math.Clamp(height, 1, Math.Max(1, target.Bounds.Height - 1));

        if (!_registered)
        {
            var registration = CreateData();
            if (NativeMethods.SHAppBarMessage(NativeMethods.AbmNew, ref registration) == UIntPtr.Zero)
            {
                Hide();
                error = "Windows Shell 未接受 AppBar 註冊。";
                return false;
            }
            _registered = true;
        }

        if (!SetAppBarPosition(out error))
        {
            RemoveRegistration();
            Hide();
            return false;
        }

        // Position the hidden form before showing it so the default 300x300
        // WinForms size never flashes over the desktop during activation.
        if (!Visible)
            Show();

        return true;
    }

    public void StopMask()
    {
        RemoveRegistration();
        if (!IsDisposed)
            Hide();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.AppBarCallbackMessage &&
            unchecked((uint)message.WParam.ToInt64()) == NativeMethods.AbnPosChanged)
        {
            QueueReposition();
        }

        base.WndProc(ref message);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveRegistration();
        base.OnFormClosed(e);
    }

    private NativeMethods.AppBarData CreateData() => new()
    {
        Size = (uint)Marshal.SizeOf<NativeMethods.AppBarData>(),
        Window = Handle,
        CallbackMessage = NativeMethods.AppBarCallbackMessage,
        Edge = NativeMethods.AbeTop
    };

    private bool SetAppBarPosition(out string error)
    {
        error = string.Empty;
        if (_target is null || !_registered)
        {
            error = "尚未選取有效的顯示器。";
            return false;
        }

        if (_layoutInProgress)
            return true;

        _layoutInProgress = true;
        try
        {
            var monitor = _target.Bounds;
            var data = CreateData();
            data.Bounds = new NativeMethods.Rect
            {
                Left = monitor.Left,
                Top = monitor.Top,
                Right = monitor.Right,
                Bottom = checked(monitor.Top + _height)
            };

            if (NativeMethods.SHAppBarMessage(NativeMethods.AbmQueryPos, ref data) == UIntPtr.Zero)
            {
                error = "Windows Shell 無法預留所選螢幕的上方工作區。";
                return false;
            }

            // AppBar query may move the proposed top edge to avoid another bar. Keep
            // that approved edge, while restoring the exact pixel height requested.
            data.Bounds.Bottom = checked(data.Bounds.Top + _height);
            if (_lastBounds is { } previous && SameRect(previous, data.Bounds))
                return true;

            if (NativeMethods.SHAppBarMessage(NativeMethods.AbmSetPos, ref data) == UIntPtr.Zero)
            {
                error = "Windows Shell 無法設定 AppBar 位置。";
                return false;
            }

            var finalBounds = data.Bounds;
            if (finalBounds.Width <= 0 || finalBounds.Height <= 0 ||
                !NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost,
                    finalBounds.Left, finalBounds.Top, finalBounds.Width, finalBounds.Height,
                    SwpNoActivate))
            {
                error = $"無法顯示遮罩視窗 (Win32 {Marshal.GetLastWin32Error()})。";
                return false;
            }

            _lastBounds = finalBounds;
            return true;
        }
        catch (OverflowException)
        {
            error = "指定的遮罩高度超出 Windows 可用範圍。";
            return false;
        }
        finally
        {
            _layoutInProgress = false;
        }
    }

    private void QueueReposition()
    {
        if (!_registered || _layoutInProgress || _callbackQueued || IsDisposed || !IsHandleCreated)
            return;

        _callbackQueued = true;
        try
        {
            BeginInvoke((Action)(() =>
            {
                _callbackQueued = false;
                if (_registered && !SetAppBarPosition(out _))
                    StopMask();
            }));
        }
        catch (InvalidOperationException)
        {
            _callbackQueued = false;
        }
    }

    private void RemoveRegistration()
    {
        if (_registered && IsHandleCreated)
        {
            var data = CreateData();
            NativeMethods.SHAppBarMessage(NativeMethods.AbmRemove, ref data);
        }

        _registered = false;
        _target = null;
        _lastBounds = null;
    }

    private static bool SameRect(NativeMethods.Rect left, NativeMethods.Rect right) =>
        left.Left == right.Left && left.Top == right.Top &&
        left.Right == right.Right && left.Bottom == right.Bottom;
}
