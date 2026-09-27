using System.Runtime.InteropServices;

namespace Shigure;

/// <summary>
/// 无边框窗体缩放/最大化辅助：边缘 Padding + 子控件 HTTRANSPARENT + NCHITTEST，
/// 以及 WM_GETMINMAXINFO / MaximizedBounds 限制到工作区（不盖任务栏）。
/// </summary>
internal static class BorderlessFormChrome
{
    public const int ResizeBorderLogical = 8;
    public const int WmNcHitTest = 0x0084;
    public const int WmGetMinMaxInfo = 0x0024;
    public const nint HtTransparent = -1;

    public static int GetResizeBorder(Control control)
        => Math.Max(ResizeBorderLogical, (int)Math.Round(ResizeBorderLogical * control.DeviceDpi / 96f));

    /// <summary>
    /// 在客户区四边留出无子控件覆盖的热区，使 Form.WndProc 能直接收到边缘 NCHITTEST。
    /// </summary>
    public static void ApplyResizePadding(Form form)
    {
        var border = GetResizeBorder(form);
        var padding = new Padding(border);
        if (form.Padding != padding)
        {
            form.Padding = padding;
        }
    }

    /// <summary>
    /// 当前监视器工作区（供 Form 子类设置 protected MaximizedBounds）。
    /// </summary>
    public static Rectangle GetWorkingArea(Form form)
        => Screen.FromControl(form).WorkingArea;

    /// <summary>
    /// 处理 WM_NCHITTEST：四边/四角返回对应 HT*。调用方应在 Maximized 时跳过。
    /// </summary>
    public static bool TryHandleNcHitTest(Form form, ref Message m)
    {
        if (m.Msg != WmNcHitTest)
        {
            return false;
        }

        // 先走默认，再覆盖边缘；最大化由调用方跳过本方法。
        // 不用 base 时也必须给 Result，这里由调用方先 base 再调 HitTest。
        var screenPoint = new Point(
            unchecked((short)(long)m.LParam),
            unchecked((short)((long)m.LParam >> 16)));
        var hit = HitTestResize(form, form.PointToClient(screenPoint));
        if (hit != NativeMethods.HtClient)
        {
            m.Result = hit;
        }

        return true;
    }

    public static nint HitTestResize(Form form, Point clientPoint)
    {
        var border = GetResizeBorder(form);
        // 角区与边同厚即可：Padding 已把热区缩进到圆角内侧。
        var w = form.ClientSize.Width;
        var h = form.ClientSize.Height;

        var onLeft = clientPoint.X < border;
        var onRight = clientPoint.X >= w - border;
        var onTop = clientPoint.Y < border;
        var onBottom = clientPoint.Y >= h - border;

        if (onTop && onLeft)
        {
            return NativeMethods.HtTopLeft;
        }

        if (onTop && onRight)
        {
            return NativeMethods.HtTopRight;
        }

        if (onBottom && onLeft)
        {
            return NativeMethods.HtBottomLeft;
        }

        if (onBottom && onRight)
        {
            return NativeMethods.HtBottomRight;
        }

        if (onLeft)
        {
            return NativeMethods.HtLeft;
        }

        if (onRight)
        {
            return NativeMethods.HtRight;
        }

        if (onTop)
        {
            return NativeMethods.HtTop;
        }

        if (onBottom)
        {
            return NativeMethods.HtBottom;
        }

        return NativeMethods.HtClient;
    }

    /// <summary>
    /// 将最大化尺寸限制在当前监视器工作区（不含任务栏）。
    /// </summary>
    public static bool TryHandleGetMinMaxInfo(Form form, ref Message m)
    {
        if (m.Msg != WmGetMinMaxInfo || m.LParam == 0)
        {
            return false;
        }

        var screen = Screen.FromHandle(form.Handle);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
        info.PtMaxPosition = new NativeMethods.Point(work.Left - bounds.Left, work.Top - bounds.Top);
        info.PtMaxSize = new NativeMethods.Point(work.Width, work.Height);
        info.PtMaxTrackSize = new NativeMethods.Point(work.Width, work.Height);
        Marshal.StructureToPtr(info, m.LParam, fDeleteOld: false);
        return true;
    }

    /// <summary>
    /// 对贴近窗体边缘的子控件安装 HTTRANSPARENT，让边缘命中回落到 Form。
    /// </summary>
    public static EdgeHitTransparentScope InstallEdgeHitTransparent(Form form)
        => new(form);

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativeMethods.Point PtReserved;
        public NativeMethods.Point PtMaxSize;
        public NativeMethods.Point PtMaxPosition;
        public NativeMethods.Point PtMinTrackSize;
        public NativeMethods.Point PtMaxTrackSize;
    }

    internal sealed class EdgeHitTransparentScope : IDisposable
    {
        private readonly Form _form;
        private readonly List<EdgeHitFilter> _filters = [];
        private readonly HashSet<Control> _attached = [];
        private bool _disposed;

        public EdgeHitTransparentScope(Form form)
        {
            _form = form;
            _form.ControlAdded += OnControlAdded;
            if (_form.IsHandleCreated)
            {
                AttachTree(_form.Controls);
            }
            else
            {
                _form.HandleCreated += (_, _) => AttachTree(_form.Controls);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _form.ControlAdded -= OnControlAdded;
            foreach (var filter in _filters)
            {
                filter.Detach();
            }

            _filters.Clear();
            _attached.Clear();
        }

        private void OnControlAdded(object? sender, ControlEventArgs e)
        {
            if (e.Control is { } control)
            {
                AttachTree(control);
            }
        }

        private void AttachTree(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                AttachTree(control);
            }
        }

        private void AttachTree(Control control)
        {
            control.ControlAdded -= OnControlAdded;
            control.ControlAdded += OnControlAdded;
            if (_attached.Add(control))
            {
                _filters.Add(new EdgeHitFilter(_form, control));
            }

            AttachTree(control.Controls);
        }
    }

    private sealed class EdgeHitFilter : NativeWindow
    {
        private readonly Form _form;
        private readonly Control _control;
        private bool _assigned;

        public EdgeHitFilter(Form form, Control control)
        {
            _form = form;
            _control = control;
            _control.HandleCreated += OnHandleCreated;
            _control.HandleDestroyed += OnHandleDestroyed;
            if (_control.IsHandleCreated)
            {
                Assign();
            }
        }

        public void Detach()
        {
            _control.HandleCreated -= OnHandleCreated;
            _control.HandleDestroyed -= OnHandleDestroyed;
            if (_assigned)
            {
                ReleaseHandle();
                _assigned = false;
            }
        }

        private void OnHandleCreated(object? sender, EventArgs e) => Assign();

        private void OnHandleDestroyed(object? sender, EventArgs e)
        {
            if (_assigned)
            {
                ReleaseHandle();
                _assigned = false;
            }
        }

        private void Assign()
        {
            if (_assigned || !_control.IsHandleCreated)
            {
                return;
            }

            AssignHandle(_control.Handle);
            _assigned = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest
                && !_form.IsDisposed
                && _form.WindowState == FormWindowState.Normal)
            {
                var screenPoint = new Point(
                    unchecked((short)(long)m.LParam),
                    unchecked((short)((long)m.LParam >> 16)));
                var client = _form.PointToClient(screenPoint);
                var border = GetResizeBorder(_form);
                if (client.X < border
                    || client.Y < border
                    || client.X >= _form.ClientSize.Width - border
                    || client.Y >= _form.ClientSize.Height - border)
                {
                    m.Result = HtTransparent;
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }
}
