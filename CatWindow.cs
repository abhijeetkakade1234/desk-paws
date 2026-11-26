using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DeskPaws;

public sealed class CatWindow : Form
{
    private const int WmKeyDown = 0x100, WmKeyUp = 0x101, WmSysKeyDown = 0x104, WmSysKeyUp = 0x105;
    private const int WhKeyboardLl = 13;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    private readonly HookProc hookProc;
    private readonly HashSet<int> held = [];
    private IntPtr hook;
    private DateTime lastAction = DateTime.UtcNow;
    private Point lastMouse = Cursor.Position;
    private bool mouseActive, petting, dragging, leftPaw, rightPaw;
    private Point dragOffset;
    private int coat;
    private int sleepSeconds = 30;
    private bool leftHanded;

    private static readonly (string Name, Color Fur, Color Patch, Color Ear)[] Coats =
    [
        ("Orange", Color.FromArgb(240, 153, 80), Color.FromArgb(255, 221, 175), Color.FromArgb(217, 112, 94)),
        ("Tuxedo", Color.FromArgb(58, 62, 75), Color.WhiteSmoke, Color.FromArgb(211, 119, 138)),
        ("Cream", Color.FromArgb(246, 224, 190), Color.FromArgb(255, 247, 229), Color.FromArgb(223, 154, 163)),
        ("Gray", Color.FromArgb(132, 148, 164), Color.FromArgb(220, 230, 233), Color.FromArgb(207, 140, 158)),
        ("Brown", Color.FromArgb(147, 105, 84), Color.FromArgb(232, 191, 156), Color.FromArgb(210, 126, 137)),
        ("Calico", Color.FromArgb(247, 237, 220), Color.FromArgb(238, 153, 95), Color.FromArgb(216, 133, 148))
    ];

    public CatWindow()
    {
        Text = "Desk Paws";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        ClientSize = new Size(290, 270);
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - Width - 24, screen.Bottom - Height - 10);

        var menu = new ContextMenuStrip();
        var coats = new ToolStripMenuItem("Cat color");
        for (var i = 0; i < Coats.Length; i++)
        {
            var index = i;
            coats.DropDownItems.Add(Coats[i].Name, null, (_, _) => { coat = index; Invalidate(); });
        }
        menu.Items.Add(coats);
        var sizes = new ToolStripMenuItem("Size");
        foreach (var (name, scale) in new[] { ("Small", .8f), ("Medium", 1f), ("Large", 1.25f) })
            sizes.DropDownItems.Add(name, null, (_, _) => { ClientSize = new Size((int)(290 * scale), (int)(270 * scale)); Invalidate(); });
        menu.Items.Add(sizes);
        var sleeps = new ToolStripMenuItem("Sleep after");
        foreach (var seconds in new[] { 15, 30, 60, 120 })
            sleeps.DropDownItems.Add($"{seconds} seconds", null, (_, _) => sleepSeconds = seconds);
        menu.Items.Add(sleeps);
        menu.Items.Add("Left handed mouse", null, (_, _) => { leftHanded = !leftHanded; });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Close());
        ContextMenuStrip = menu;

        hookProc = KeyboardCallback;
        Load += (_, _) =>
        {
            hook = SetWindowsHookEx(WhKeyboardLl, hookProc, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                MessageBox.Show("Could not listen for keyboard activity. Try running the app normally again.", Text);
                Close();
                return;
            }
            timer.Start();
        };
        FormClosed += (_, _) => { timer.Stop(); if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); };
        timer.Tick += (_, _) =>
        {
            var mouse = Cursor.Position;
            mouseActive = mouse != lastMouse || (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0;
            lastMouse = mouse;
            if (mouseActive) lastAction = DateTime.UtcNow;
            Invalidate();
        };
        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Y < Height * .64) { petting = true; lastAction = DateTime.UtcNow; }
            else { dragging = true; dragOffset = e.Location; }
            Invalidate();
        };
        MouseMove += (_, e) => { if (dragging) Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y); };
        MouseUp += (_, _) => { petting = dragging = false; Invalidate(); };
    }

    private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var msg = message.ToInt32();
            var key = Marshal.ReadInt32(data);
            if (msg is WmKeyDown or WmSysKeyDown) held.Add(key);
            if (msg is WmKeyUp or WmSysKeyUp) held.Remove(key);
            // ponytail: key codes live only in this set while physically held; no text, log, or network path exists.
            leftPaw = held.Any(IsLeftKey);
            rightPaw = held.Any(k => !IsLeftKey(k));
            if (held.Count > 0) lastAction = DateTime.UtcNow;
            Invalidate();
        }
        return CallNextHookEx(hook, code, message, data);
    }

    internal static bool IsLeftKey(int key) => key switch
    {
        >= 0x41 and <= 0x47 or >= 0x51 and <= 0x54 or >= 0x31 and <= 0x35 => true,
        0x5A or 0x58 or 0x43 or 0x56 => true,
        0x09 or 0x14 or 0x1B or 0xA0 or 0xA2 or 0xA4 or 0xC0 or 0xBD => true,
        _ => false
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(Width / 290f, Height / 270f);
        var (name, fur, patch, ear) = Coats[coat];
        bool asleep = DateTime.UtcNow - lastAction > TimeSpan.FromSeconds(sleepSeconds);
        using var shadow = new SolidBrush(Color.FromArgb(55, 53, 39));
        using var body = new SolidBrush(fur);
        using var light = new SolidBrush(patch);
        using var pink = new SolidBrush(ear);
        using var ink = new Pen(Color.FromArgb(68, 54, 57), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var thin = new Pen(Color.FromArgb(98, 75, 72), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillEllipse(shadow, 44, 251, 210, 11);
        // Tail and body sit behind the desk.
        g.DrawArc(new Pen(fur, 17) { StartCap = LineCap.Round, EndCap = LineCap.Round }, 195, 146, 65, 80, 245, 220);
        g.FillEllipse(body, 73, 111, 146, 132);
        g.FillEllipse(light, 101, 153, 91, 78);
        // Ears, head, and coat markings.
        FillTriangle(g, body, [new(84, 87), new(85, 27), new(128, 59)]);
        FillTriangle(g, body, [new(164, 57), new(209, 27), new(207, 91)]);
        FillTriangle(g, pink, [new(93, 76), new(94, 44), new(118, 64)]);
        FillTriangle(g, pink, [new(177, 64), new(199, 44), new(199, 78)]);
        g.FillEllipse(body, 74, 52, 143, 128);
        g.FillEllipse(light, 111, 107, 68, 54);
        if (coat is 0 or 4) { g.DrawLine(thin, 141, 59, 138, 80); g.DrawLine(thin, 124, 65, 128, 79); g.DrawLine(thin, 159, 65, 154, 79); }
        if (coat == 5) { g.FillEllipse(new SolidBrush(Color.FromArgb(85, 84, 72)), 80, 72, 39, 35); g.FillEllipse(new SolidBrush(Color.FromArgb(235, 151, 95)), 177, 69, 33, 38); }
        if (asleep || petting)
        {
            g.DrawArc(ink, 103, 100, 26, 13, 10, 160);
            g.DrawArc(ink, 164, 100, 26, 13, 10, 160);
        }
        else
        {
            g.FillEllipse(Brushes.DarkSlateGray, 108, 101, 15, 19);
            g.FillEllipse(Brushes.DarkSlateGray, 169, 101, 15, 19);
            g.FillEllipse(Brushes.White, 111, 103, 5, 5);
            g.FillEllipse(Brushes.White, 172, 103, 5, 5);
        }
        FillTriangle(g, pink, [new(139, 125), new(153, 125), new(146, 133)]);
        g.DrawArc(thin, 133, 129, 13, 12, 0, 150);
        g.DrawArc(thin, 146, 129, 13, 12, 30, 150);
        g.DrawLine(thin, 108, 133, 81, 128); g.DrawLine(thin, 108, 140, 84, 145);
        g.DrawLine(thin, 184, 133, 211, 128); g.DrawLine(thin, 184, 140, 208, 145);
        // A little keyboard gives each paw somewhere to land.
        using var desk = new SolidBrush(Color.FromArgb(231, 213, 190));
        using var board = new SolidBrush(Color.FromArgb(250, 244, 230));
        FillRound(g, desk, new Rectangle(43, 210, 204, 48), 12);
        FillRound(g, board, new Rectangle(48, 211, 194, 40), 10);
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 9; col++)
            {
                var active = (col < 4 && leftPaw) || (col > 4 && rightPaw);
                using var keyBrush = new SolidBrush(active ? Color.FromArgb(250, 174, 139) : Color.FromArgb(224, 217, 207));
                FillRound(g, keyBrush, new Rectangle(58 + col * 20, 216 + row * 16, 15, 10), 3);
            }
        g.FillEllipse(body, 72, leftPaw ? 205 : 193, 49, 35);
        g.FillEllipse(body, 171, rightPaw || (mouseActive && !leftHanded) ? 205 : 193, 49, 35);
        g.DrawArc(thin, 83, leftPaw ? 222 : 210, 25, 9, 185, 170);
        g.DrawArc(thin, 182, rightPaw || mouseActive ? 222 : 210, 25, 9, 185, 170);
        if (mouseActive)
        {
            using var mouseBrush = new SolidBrush(Color.FromArgb(251, 236, 218));
            g.FillEllipse(mouseBrush, leftHanded ? 43 : 225, 196, 29, 39);
            g.DrawArc(thin, leftHanded ? 43 : 225, 196, 29, 39, 0, 360);
        }
        if (asleep)
        {
            using var font = new Font("Segoe UI", 17, FontStyle.Bold);
            g.DrawString("z z Z", font, Brushes.SteelBlue, 204, 48);
        }
        if (petting)
        {
            using var font = new Font("Segoe UI Symbol", 18);
            g.DrawString("♥", font, Brushes.IndianRed, 211, 53);
        }
    }

    private static void FillTriangle(Graphics g, Brush brush, Point[] points) => g.FillPolygon(brush, points);

    private static void FillRound(Graphics g, Brush brush, Rectangle r, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}

