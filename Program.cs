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
        Application.Run(new CatWindow());
    }    
}

