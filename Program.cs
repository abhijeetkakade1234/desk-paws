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
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (Environment.GetCommandLineArgs().Contains("--preview"))
        {
            using var cat = new CatWindow();
            using var bitmap = new Bitmap(cat.Width, cat.Height);
            cat.DrawToBitmap(bitmap, new Rectangle(Point.Empty, cat.Size));
            bitmap.Save(Path.Combine(Environment.CurrentDirectory, "preview.png"));
            return;
        }
        Application.Run(new CatWindow());
    }    
}

