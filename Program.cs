using System;
using System.IO;
using System.Windows.Forms;

namespace WootMouseRemap
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Directory.CreateDirectory("Logs");
            Directory.CreateDirectory("Profiles");

            using var mutex = new System.Threading.Mutex(true, "WootMouseRemap.Singleton", out bool isNew);
            if (!isNew) return;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try { File.AppendAllText("Logs\\fatal.txt", $"[Unhandled] {DateTime.Now:u}\n{e.ExceptionObject}\n\n"); } catch { }
                MessageBox.Show("A fatal error occurred. See Logs\\fatal.txt", "WootMouseRemap", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.ThreadException += (_, e) =>
            {
                try { File.AppendAllText("Logs\\fatal.txt", $"[Thread] {DateTime.Now:u}\n{e.Exception}\n\n"); } catch { }
                MessageBox.Show("An error occurred. See Logs\\fatal.txt", "WootMouseRemap", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            using var msgWin = new RawInputMsgWindow();
            using var raw = new RawInput(msgWin);
            LowLevelHooks.Install();

            Application.ApplicationExit += (_, __) =>
            {
                LowLevelHooks.Uninstall();
            };

            using var form = new OverlayForm();

            // Handle form closing to minimize to tray instead of exiting
            form.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    form.Hide();
                }
            };

            Application.Run(form);
        }
    }
}
