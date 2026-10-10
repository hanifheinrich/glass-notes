// GlassNotesBar - a small button on the Windows taskbar that shows and hides Glass Notes.
// It works like TrafficMonitor: this window becomes a child of Shell_TrayWnd, and
// ReBarWindow32 (the app buttons area) is narrowed to make room on its right.
//
// Build: build.cmd (uses the csc.exe that ships with .NET Framework 4, no SDK needed)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Native
{
    public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPSIBLINGS = 0x04000000;
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80;
    public const int LWA_COLORKEY = 0x1;
    public const int SW_RESTORE = 9, SW_SHOW = 5, SW_MINIMIZE = 6;
    public const uint SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 0x4, SWP_SHOWWINDOW = 0x40;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hgt, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, int key, byte alpha, int flags);
    [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref RECT r, int n);
}

class BarButton : Control
{
    // Transparent color key. Almost black so anti-aliased edges blend into a dark taskbar.
    static readonly Color KeyColor = Color.FromArgb(1, 1, 1);

    readonly IntPtr trayHwnd;
    bool hover;

    public event EventHandler LeftClicked;

    public BarButton(IntPtr tray)
    {
        trayHwnd = tray;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = KeyColor;
        Cursor = Cursors.Hand;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.Parent = trayHwnd;
            cp.Style = Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_CLIPSIBLINGS;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetLayeredWindowAttributes(Handle, ColorTranslator.ToWin32(KeyColor), 255, Native.LWA_COLORKEY);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && LeftClicked != null) LeftClicked(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(KeyColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        int h = Height, w = Width;
        if (hover)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 58, 58)))
                g.FillRectangle(b, 0, 0, w, h);
        }

        // Same outline logo as the Glass Notes title bar, drawn on a 24x24 grid and scaled.
        float size = h * 0.56f;
        float k = size / 24f;
        float ox = (w - size) / 2f, oy = (h - size) / 2f;
        Func<float, float, PointF> P = (x, y) => new PointF(ox + x * k, oy + y * k);

        using (Pen pen = new Pen(Color.White, Math.Max(1.2f, 1.5f * k)))
        {
            pen.LineJoin = LineJoin.Round;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;

            // Page with folded corner
            g.DrawPolygon(pen, new[] { P(4.5f, 2.5f), P(15.5f, 2.5f), P(19.5f, 6.5f), P(19.5f, 21.5f), P(4.5f, 21.5f) });
            g.DrawLines(pen, new[] { P(15.5f, 2.5f), P(15.5f, 6.5f), P(19.5f, 6.5f) });

            // Text lines
            g.DrawLine(pen, P(8f, 10f), P(16f, 10f));
            g.DrawLine(pen, P(8f, 13.5f), P(16f, 13.5f));
            g.DrawLine(pen, P(8f, 17f), P(12f, 17f));

            // Pen
            g.DrawLine(pen, P(15f, 15f), P(22.5f, 22.5f));
        }
    }
}

class BarContext : ApplicationContext
{
    // GlassNotes.exe next to this program, otherwise the default per-user install folder.
    static readonly string GlassNotesExe = FindGlassNotesExe();

    static string FindGlassNotesExe()
    {
        string sideBySide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlassNotes.exe");
        if (File.Exists(sideBySide)) return sideBySide;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\GlassNotes\GlassNotes.exe");
    }

    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly ToolTip tip = new ToolTip();
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    BarButton button;
    IntPtr tray, rebar, notify;
    IntPtr lastForeground;

    public BarContext()
    {
        menu.Items.Add("Show / hide Glass Notes", null, delegate { Toggle(); });
        menu.Items.Add(new ToolStripSeparator());
        // Closes it for this session; turn off "Show button on taskbar" in Glass Notes to remove it for good
        menu.Items.Add("Close taskbar button", null, delegate { ExitBar(); });

        timer.Interval = 250;
        timer.Tick += delegate { Tick(); };
        timer.Start();
        Tick();
    }

    // Icon only, so the button is roughly square
    int BarWidth(int height) { return (int)Math.Round(height * 1.1); }

    // Glass Notes sets this when "Show button on taskbar" is turned off,
    // so the button quits cleanly and gives the taskbar space back.
    readonly EventWaitHandle exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "GlassNotesBar_Exit");

    void Tick()
    {
        if (exitSignal.WaitOne(0)) { ExitBar(); return; }

        // Remember the last active window (other than the taskbar or this button) for the toggle logic.
        IntPtr fg = Native.GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != tray && (button == null || !button.IsHandleCreated || fg != button.Handle))
            lastForeground = fg;

        IntPtr t = Native.FindWindow("Shell_TrayWnd", null);
        if (t == IntPtr.Zero) return;

        if (t != tray || button == null || !button.IsHandleCreated || !Native.IsWindow(button.Handle))
        {
            // First run, or Explorer has just restarted.
            tray = t;
            rebar = Native.FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
            notify = Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            if (rebar == IntPtr.Zero || notify == IntPtr.Zero) return;
            if (button != null) { try { button.Dispose(); } catch { } }
            button = new BarButton(tray);
            button.LeftClicked += delegate { Toggle(); };
            button.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
            };
            button.CreateControl();
            tip.SetToolTip(button, "Glass Notes - click to show or hide");
        }

        Layout();
    }

    void Layout()
    {
        Native.RECT trayR, rb, nt;
        Native.GetWindowRect(tray, out trayR);
        if (trayR.Bottom - trayR.Top > trayR.Right - trayR.Left) return; // vertical taskbar: not supported

        Native.GetWindowRect(rebar, out rb);
        Native.GetWindowRect(notify, out nt);
        Native.MapWindowPoints(IntPtr.Zero, tray, ref rb, 2);
        Native.MapWindowPoints(IntPtr.Zero, tray, ref nt, 2);

        int h = rb.Bottom - rb.Top;
        int w = BarWidth(h);
        int targetRight = nt.Left - w;

        if (rb.Right != targetRight && targetRight > rb.Left + 100)
            Native.MoveWindow(rebar, rb.Left, rb.Top, targetRight - rb.Left, h, true);

        if (button.Left != targetRight || button.Top != rb.Top || button.Width != w || button.Height != h)
            Native.SetWindowPos(button.Handle, IntPtr.Zero, targetRight, rb.Top, w, h,
                Native.SWP_NOACTIVATE | Native.SWP_NOZORDER | Native.SWP_SHOWWINDOW);
    }

    void RestoreRebar()
    {
        if (!Native.IsWindow(rebar) || !Native.IsWindow(notify)) return;
        Native.RECT rb, nt;
        Native.GetWindowRect(rebar, out rb);
        Native.GetWindowRect(notify, out nt);
        Native.MapWindowPoints(IntPtr.Zero, tray, ref rb, 2);
        Native.MapWindowPoints(IntPtr.Zero, tray, ref nt, 2);
        Native.MoveWindow(rebar, rb.Left, rb.Top, nt.Left - rb.Left, rb.Bottom - rb.Top, true);
    }

    static Process FindGlassNotes()
    {
        Process[] ps = Process.GetProcessesByName("GlassNotes");
        return ps.Length > 0 ? ps[0] : null;
    }

    // Glass Notes main window: a top-level window of its process with a title and no owner.
    static IntPtr FindMainWindow(int pid, bool visibleOnly)
    {
        IntPtr found = IntPtr.Zero;
        Native.EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            int p;
            Native.GetWindowThreadProcessId(h, out p);
            if (p == pid && Native.GetWindowTextLength(h) > 0 && Native.GetWindow(h, 4 /*GW_OWNER*/) == IntPtr.Zero
                && (!visibleOnly || Native.IsWindowVisible(h)))
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    DateTime lastToggle = DateTime.MinValue, lastLaunch = DateTime.MinValue;

    void Launch()
    {
        // Glass Notes is not single-instance, so don't start it twice while it is still loading.
        if ((DateTime.Now - lastLaunch).TotalSeconds < 5) return;
        lastLaunch = DateTime.Now;
        if (File.Exists(GlassNotesExe)) Process.Start(GlassNotesExe);
        else MessageBox.Show("Glass Notes was not found at:\n" + GlassNotesExe, "Glass Notes taskbar button");
    }

    void Toggle()
    {
        if ((DateTime.Now - lastToggle).TotalMilliseconds < 400) return;
        lastToggle = DateTime.Now;

        Process proc = FindGlassNotes();
        if (proc == null) { Launch(); return; }

        IntPtr main = FindMainWindow(proc.Id, true);
        if (main == IntPtr.Zero) main = FindMainWindow(proc.Id, false);
        if (main == IntPtr.Zero) { Launch(); return; }

        int fgPid = 0;
        if (lastForeground != IntPtr.Zero) Native.GetWindowThreadProcessId(lastForeground, out fgPid);
        bool showingAndActive = Native.IsWindowVisible(main) && !Native.IsIconic(main) && fgPid == proc.Id;

        if (showingAndActive)
        {
            Native.ShowWindow(main, Native.SW_MINIMIZE);
            lastForeground = IntPtr.Zero;
        }
        else
        {
            Native.ShowWindow(main, Native.IsIconic(main) ? Native.SW_RESTORE : Native.SW_SHOW);
            Native.SetForegroundWindow(main);
        }
    }

    void ExitBar()
    {
        timer.Stop();
        RestoreRebar();
        if (button != null) button.Dispose();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        RestoreRebar();
        base.ExitThreadCore();
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex m = new Mutex(true, "GlassNotesBar_SingleInstance", out created))
        {
            if (!created) return;
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            BarContext ctx = new BarContext();
            Application.Run(ctx);
        }
    }
}
