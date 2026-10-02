using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AATool
{
    public static class Program
    {
        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e) =>
            Debug.SaveReport(e.ExceptionObject as Exception);

        private static void GlobalThreadExceptionHandler(object sender, ThreadExceptionEventArgs e) =>
            Debug.SaveReport(e.Exception);

        private static void UseDefaultWinformsColors()
        {
            //mono's winforms only partially applies the desktop's gtk colors (e.g. light text on white panels with
            //dark themes). it skips gtk when it thinks it's running under kde, so pretend to be kde while
            //winforms initializes to get the same default colors as on windows
            string session = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
            Environment.SetEnvironmentVariable("DESKTOP_SESSION", "KDE");
            try
            {
                _ = SystemInformation.VirtualScreen;
            }
            finally
            {
                Environment.SetEnvironmentVariable("DESKTOP_SESSION", session);
            }
        }

        [STAThread]
        static void Main()
        {
            //assets and settings are relative to the install folder, regardless of where aatool was launched from
            Environment.CurrentDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);

            if (!Platform.IsWindows)
            {
                UseDefaultWinformsColors();

                //otherwise the windows are grouped under the mono runtime's name
                if (Environment.GetEnvironmentVariable("SDL_VIDEO_X11_WMCLASS") is null)
                    Environment.SetEnvironmentVariable("SDL_VIDEO_X11_WMCLASS", "AATool");
            }

            //add crash reporting events
            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;
            Application.ThreadException += GlobalThreadExceptionHandler;

            //start application
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var main = new Main())
                main.Run();
        }
    }
}
