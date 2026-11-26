namespace DeskPaws;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--self-test"))
        {
            if (!CatWindow.IsLeftKey('A') || !CatWindow.IsLeftKey('V') || CatWindow.IsLeftKey('J') || CatWindow.IsLeftKey('M'))
                throw new Exception("Keyboard hand mapping failed.");
            return;
        }
        ApplicationConfiguration.Initialize();
        if (Environment.GetCommandLineArgs().Contains("--preview"))
        {
            using var cat = new CatWindow();
            using var bitmap = new Bitmap(cat.Width, cat.Height);
            cat.DrawToBitmap(bitmap, new Rectangle(Point.Empty, cat.Size));
            bitmap.Save(Path.Combine(Environment.CurrentDirectory, "preview.png"));
            return;
        }
        using var mutex = new Mutex(true, "Local\\DeskPaws.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("Desk Paws is already running. Look for its cat icon in the taskbar or system tray.", "Desk Paws");
            return;
        }
        Application.Run(new CatWindow());
    }    
}

