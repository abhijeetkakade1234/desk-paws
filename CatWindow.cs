using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DeskPaws;

public sealed class CatWindow : Form
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x100, WmKeyUp = 0x101, WmSysKeyDown = 0x104, WmSysKeyUp = 0x105;
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskPaws", "settings.json");
    private static readonly string[] CoatNames = ["Orange", "Tuxedo", "Calico", "Blue-gray"];
    private readonly Bitmap[] cats = [LoadArt("cat-orange.png"), LoadArt("cat-tuxedo.png"), LoadArt("cat-calico.png"), LoadArt("cat-bluegray.png")];
    private readonly Bitmap orangeLeft = LoadArt("cat-orange-left.png");
    private readonly Bitmap orangeRight = LoadArt("cat-orange-right.png");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    private readonly HookProc hookProc;
    private readonly HashSet<int> held = [];
    private readonly NotifyIcon tray;
    private readonly Settings settings;
    private IntPtr hook;
    private DateTime lastAction = DateTime.UtcNow;
    private Point lastMouse = Cursor.Position;
    private Point dragOffset;
    private bool mouseActive, petting, dragging, leftPaw, rightPaw, wasAsleep;

    public CatWindow()
    {
        settings = LoadSettings();
        Text = "Desk Paws";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        SetSize(settings.Size);
        var defaultLocation = new Point(Screen.PrimaryScreen!.WorkingArea.Right - Width - 24, Screen.PrimaryScreen.WorkingArea.Bottom - Height - 10);
        Location = settings.X >= 0 && settings.Y >= 0 && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(settings.X, settings.Y, Width, Height)))
            ? new Point(settings.X, settings.Y) : defaultLocation;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        var menu = new ContextMenuStrip();
        var coats = new ToolStripMenuItem("Cat color");
        for (var i = 0; i < CoatNames.Length; i++)
        {
            int index = i;
            coats.DropDownItems.Add(CoatNames[i], null, (_, _) => { settings.Coat = index; SaveSettings(); Invalidate(); });
        }
        menu.Items.Add(coats);
        var sizes = new ToolStripMenuItem("Size");
        foreach (var (label, size) in new[] { ("Small", 0), ("Medium", 1), ("Large", 2) })
            sizes.DropDownItems.Add(label, null, (_, _) => { settings.Size = size; SetSize(size); SaveSettings(); Invalidate(); });
        menu.Items.Add(sizes);
        var sleeps = new ToolStripMenuItem("Sleep after");
        foreach (var seconds in new[] { 15, 30, 60, 120 })
            sleeps.DropDownItems.Add($"{seconds} seconds", null, (_, _) => { settings.SleepSeconds = seconds; SaveSettings(); });
        menu.Items.Add(sleeps);
        var leftHanded = new ToolStripMenuItem("Left handed mouse") { CheckOnClick = true, Checked = settings.LeftHanded };
        leftHanded.CheckedChanged += (_, _) => { settings.LeftHanded = leftHanded.Checked; SaveSettings(); Invalidate(); };
        menu.Items.Add(leftHanded);
        menu.Items.Add(new ToolStripSeparator());
        var pause = new ToolStripMenuItem("Pause keyboard reactions") { CheckOnClick = true, Checked = settings.Paused };
        pause.CheckedChanged += (_, _) => { settings.Paused = pause.Checked; SetMonitoring(!settings.Paused); SaveSettings(); Invalidate(); };
        menu.Items.Add(pause);
        menu.Items.Add("Bring cat to front", null, (_, _) => { Show(); Activate(); TopMost = true; });
        menu.Items.Add("About and privacy", null, (_, _) => MessageBox.Show(
            "Desk Paws v1.1.0\n\nKeyboard codes are used only while keys are held to choose a paw animation. They are never saved or sent. Pause keyboard reactions from this menu to remove the keyboard hook entirely.\n\nPreferences and window position are saved in AppData.",
            "About Desk Paws", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Quit", null, (_, _) => Close());
        ContextMenuStrip = menu;
        tray = new NotifyIcon { Icon = Icon, Text = "Desk Paws", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => { Show(); Activate(); };

        hookProc = KeyboardCallback;
        Load += (_, _) => { SetMonitoring(!settings.Paused); pause.Checked = settings.Paused; timer.Start(); };
        FormClosed += (_, _) =>
        {
            timer.Stop();
            SetMonitoring(false);
            tray.Visible = false;
            tray.Dispose();
            timer.Dispose();
            foreach (var cat in cats) cat.Dispose();
            orangeLeft.Dispose();
            orangeRight.Dispose();
        };
        timer.Tick += (_, _) =>
        {
            var mouse = Cursor.Position;
            bool active = mouse != lastMouse || (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0;
            lastMouse = mouse;
            if (active) lastAction = DateTime.UtcNow;
            bool asleep = DateTime.UtcNow - lastAction > TimeSpan.FromSeconds(settings.SleepSeconds);
            if (mouseActive != active || wasAsleep != asleep) Invalidate();
            mouseActive = active;
            wasAsleep = asleep;
        };
        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            petting = e.Y < Height * .60;
            dragging = true;
            dragOffset = e.Location;
            if (petting) lastAction = DateTime.UtcNow;
            Invalidate();
        };
        MouseMove += (_, _) =>
        {
            if (!dragging) return;
            var next = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
            if (Math.Abs(next.X - Left) + Math.Abs(next.Y - Top) <= 3) return;
            petting = false;
            Location = next;
        };
        MouseUp += (_, _) =>
        {
            dragging = petting = false;
            settings.X = Left; settings.Y = Top;
            SaveSettings();
            Invalidate();
        };
    }

    private void SetSize(int size)
    {
        float scale = size switch { 0 => .8f, 2 => 1.25f, _ => 1f };
        ClientSize = new Size((int)(300 * scale), (int)(270 * scale));
    }

    private void SetMonitoring(bool enabled)
    {
        if (!enabled)
        {
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            held.Clear(); leftPaw = rightPaw = false;
            tray.Text = "Desk Paws (keyboard paused)";
            return;
        }
        if (hook != IntPtr.Zero) return;
        hook = SetWindowsHookEx(WhKeyboardLl, hookProc, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            MessageBox.Show("Keyboard reactions could not start. The cat will stay visible, but typing will not animate it.", Text);
            settings.Paused = true;
        }
        tray.Text = hook == IntPtr.Zero ? "Desk Paws (keyboard unavailable)" : "Desk Paws";
    }

    private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
        {
            int key = Marshal.ReadInt32(data);
            if (message.ToInt32() is WmKeyDown or WmSysKeyDown) held.Add(key);
            else held.Remove(key);
            // ponytail: key codes exist only while held; the app never turns them into text or stores a history.
            leftPaw = held.Any(IsLeftKey);
            rightPaw = held.Any(k => !IsLeftKey(k));
            lastAction = DateTime.UtcNow;
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
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.ScaleTransform(Width / 300f, Height / 270f);
        var image = settings.Coat == 0 && leftPaw && !rightPaw ? orangeLeft
            : settings.Coat == 0 && rightPaw && !leftPaw ? orangeRight
            : cats[settings.Coat];
        var catY = leftPaw || rightPaw ? 2 : 0;
        g.DrawImage(image, 10, catY, 280, 245);
        using var board = new SolidBrush(Color.FromArgb(57, 59, 62));
        using var key = new SolidBrush(Color.FromArgb(251, 249, 241));
        using var keyCap = new SolidBrush(Color.FromArgb(222, 219, 211));
        using var pressed = new SolidBrush(Color.FromArgb(255, 181, 119));
        using var border = new Pen(Color.FromArgb(34, 35, 38), 2);
        FillRound(g, board, new Rectangle(37, 171, 226, 73), 13);
        FillRound(g, key, new Rectangle(43, 175, 214, 60), 8);
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 10; col++)
                FillRound(g, (col < 5 && leftPaw) || (col >= 5 && rightPaw) ? pressed : keyCap,
                    new Rectangle(48 + col * 20, 180 + row * 17, 17, 13), 3);
        FillRound(g, keyCap, new Rectangle(88, 231, 124, 7), 3);
        if (mouseActive)
        {
            using var mouse = new SolidBrush(Color.FromArgb(251, 238, 222));
            var x = settings.LeftHanded ? 6 : 266;
            g.FillEllipse(mouse, x, 185, 28, 39);
            g.DrawEllipse(border, x, 185, 28, 39);
        }
        if (wasAsleep)
        {
            using var font = new Font("Segoe UI", 18, FontStyle.Bold);
            g.DrawString("Z z", font, Brushes.SteelBlue, 235, 19);
        }
        if (petting)
        {
            using var font = new Font("Segoe UI Symbol", 24);
            g.DrawString("♥", font, Brushes.HotPink, 234, 35);
        }
        if (settings.Paused)
        {
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            g.DrawString("PAUSED", font, Brushes.DarkSlateGray, 112, 246);
        }
    }

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

    private static Bitmap LoadArt(string file)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"DeskPaws.assets.{file}")
            ?? throw new InvalidOperationException($"Missing cat art: {file}");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Settings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new Settings();
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
            value.Coat = Math.Clamp(value.Coat, 0, CoatNames.Length - 1);
            value.Size = Math.Clamp(value.Size, 0, 2);
            if (value.SleepSeconds is not (15 or 30 or 60 or 120)) value.SleepSeconds = 30;
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings));
            File.Move(temp, SettingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            tray.ShowBalloonTip(3000, "Desk Paws", "Settings could not be saved.", ToolTipIcon.Warning);
        }
    }

    private sealed class Settings
    {
        public Settings() { }
        public int Coat { get; set; }
        public int Size { get; set; } = 1;
        public int SleepSeconds { get; set; } = 30;
        public bool LeftHanded { get; set; }
        public bool Paused { get; set; }
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
