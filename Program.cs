using System;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using WootMouseRemap.Core.Services;
using WootMouseRemap.Core;

namespace WootMouseRemap
{
    internal static class Program
    {
        // Exposed app-wide service provider for UI components to resolve shared services
        public static IServiceProvider? AppServices { get; private set; }

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

            // Load user preferences for gating hooks
            var userPrefs = LoadUserPreferences();

            using var msgWin = new RawInputMsgWindow();
            using var raw = new RawInput(msgWin);
            LowLevelHooks.Install(userPrefs.EnableLowLevelHooks, userPrefs.ComplianceMode);

            Application.ApplicationExit += (_, __) =>
            {
                LowLevelHooks.Uninstall();
            };

            // Build application service provider (logging + core services)
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
            // Register core services
            services.AddAntiRecoilServices();
            // Allow UI to resolve ProfileService etc.
            AppServices = services.BuildServiceProvider();

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

        private static UserPreferences LoadUserPreferences()
        {
            try
            {
                const string uiStateFile = "ui_state.json";

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(uiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                if (!File.Exists(uiStateFile)) return new UserPreferences();
                var json = File.ReadAllText(uiStateFile);
                UiStateData state;
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    state = JsonSerializer.Deserialize<UiStateData>(json, options) ?? new UiStateData();
                }
                catch (JsonException) 
                {
                    state = new UiStateData(); // Safe fallback
                }
                return state?.UserPrefs ?? new UserPreferences();
            }
            catch
            {
                return new UserPreferences();
            }
        }
    }
}
