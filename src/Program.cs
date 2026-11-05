using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DwaTop
{
    static class Program
    {
        public const string Version = "1.0";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--make-icon") { Icons.WriteIco(args[1]); return 0; }
            string action = args.Length > 0 ? args[0].Trim().TrimStart('-', '/') : null;

            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            bool created;
            var mutex = new Mutex(true, "Local\\dwaTop.single", out created);
            if (!created)
            {
                HotkeyManager.SendToRunning(string.IsNullOrEmpty(action) ? "show" : action);
                return 0;
            }
            if (action == "quit") return 0;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Log.Error(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Error(e.ExceptionObject as Exception);
                try { if (Manager.I != null) Manager.I.RestoreAll(); } catch { }
            };

            bool firstRun = !File.Exists(Config.FilePath);
            Config.Load();
            if (firstRun) Config.Save();
            UI.Init();
            int recovered = Manager.RecoverSession();
            DarkOverlay.Init();
            Log.Info("dwaTop " + Version + " démarré");

            Application.Run(new AppContext(action, firstRun, recovered));
            GC.KeepAlive(mutex);
            return 0;
        }
    }
}
