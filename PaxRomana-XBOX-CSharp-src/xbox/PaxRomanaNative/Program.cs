// Entry point of the Xbox (UWP) app. MonoGame creates the window and runs the game loop; everything else is the
// shared game code under Game/.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace PaxRomanaNative
{
    // A diary of the launch, written to startup.log in the app's local data folder, where the Xbox Device Portal can
    // read it (scripts/xbox-diagnose.sh fetches it). If the game stops before it can draw its own error screen, this
    // file says how far it got and why it stopped. It is rewritten on every launch.
    static class StartupLog
    {
        static readonly object gate = new object();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static string path;

        public static void Begin()
        {
            try
            {
                path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "startup.log");
                File.WriteAllText(path, "");
            }
            catch (Exception) { path = null; }
            string where = "?", version = "?";
            try { where = AnalyticsInfo.VersionInfo.DeviceFamily; } catch (Exception) { }
            try
            {
                ulong v = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
                version = (v >> 48) + "." + ((v >> 32) & 0xFFFF) + "." + ((v >> 16) & 0xFFFF) + "." + (v & 0xFFFF);
            }
            catch (Exception) { }
            Write("launch at " + DateTime.UtcNow.ToString("u") + " on " + where + " " + version);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        static extern IntPtr LoadLibraryExW(string name, IntPtr reserved, uint flags);

        // Records which of the system libraries the graphics and audio layer can call are on this console. A build
        // compiled with .NET Native needs every one of them to exist before the app can start at all, so this is
        // the list to check before turning .NET Native back on.
        public static void ProbeSystemLibraries()
        {
            const uint SEARCH_SYSTEM32 = 0x800;
            string found = "", missing = "";
            foreach (string name in new[] { "d3d11.dll", "dxgi.dll", "d2d1.dll", "dwrite.dll", "xaudio2_9.dll", "xaudio2_8.dll", "mfplat.dll", "mfreadwrite.dll", "mf.dll", "xinput1_4.dll" })
            {
                try
                {
                    if (LoadLibraryExW(name, IntPtr.Zero, SEARCH_SYSTEM32) != IntPtr.Zero) found += " " + name;
                    else missing += " " + name + " (error " + Marshal.GetLastWin32Error() + ")";
                }
                catch (Exception e) { missing += " " + name + " (" + e.GetType().Name + ")"; }
            }
            Write("system libraries present:" + (found.Length > 0 ? found : " none"));
            Write("system libraries missing:" + (missing.Length > 0 ? missing : " none"));
        }

        public static void Write(string line)
        {
            if (path == null) return;
            lock (gate)
            {
                try { File.AppendAllText(path, (clock.ElapsedMilliseconds / 1000.0).ToString("0.000") + "  " + line + "\r\n"); } catch (Exception) { }
            }
        }
    }

    public static class Program
    {
        static void Main()
        {
            StartupLog.Begin();
            PaxRomana.PaxGame.Log = StartupLog.Write;
            try { StartupLog.ProbeSystemLibraries(); } catch (Exception e) { StartupLog.Write("could not check the system libraries: " + e); }
            // Whatever goes wrong outside the game's own error handling ends up in the diary before the app closes.
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) => StartupLog.Write("UNHANDLED: " + e.ExceptionObject);
                TaskScheduler.UnobservedTaskException += (s, e) => StartupLog.Write("UNOBSERVED TASK ERROR: " + e.Exception);
                CoreApplication.UnhandledErrorDetected += (s, e) =>
                {
                    try { e.UnhandledError.Propagate(); }
                    catch (Exception ex) { StartupLog.Write("UNHANDLED (app model): " + ex); throw; }
                };
            }
            catch (Exception e) { StartupLog.Write("could not attach the error handlers: " + e); }

            try
            {
                var factory = new MonoGame.Framework.GameFrameworkViewSource<XboxGame>();
                StartupLog.Write("starting the app view");
                CoreApplication.Run(factory);
                StartupLog.Write("the app view returned (normal exit)");
            }
            catch (Exception e)
            {
                StartupLog.Write("STOPPED: " + e);
                throw;
            }
        }
    }

    // The game with the few things that are specific to the console.
    public sealed class XboxGame : PaxRomana.PaxGame
    {
        public XboxGame()
        {
            // On a console: controller prompts from the first frame, no mouse, no quitting to a desktop.
            // (The same package also runs on a Windows PC, where it behaves like the desktop build.)
            try { IsConsole = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox"; } catch (Exception) { IsConsole = true; }
            try
            {
                // Draw edge to edge on the television, at the display's real resolution. The HUD keeps itself
                // inside the title-safe area.
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                CoreWindow window = CoreWindow.GetForCurrentThread();
                double scale = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                int w = (int)Math.Round(window.Bounds.Width * scale), h = (int)Math.Round(window.Bounds.Height * scale);
                Log("window " + window.Bounds.Width + " x " + window.Bounds.Height + " at scale " + scale + " -> " + w + " x " + h);
                if (w >= 640 && h >= 360)
                {
                    Graphics.PreferredBackBufferWidth = w;
                    Graphics.PreferredBackBufferHeight = h;
                }
            }
            catch (Exception e)
            {
                // Not fatal: MonoGame falls back to the size of the visible area.
                Log("could not size the picture to the window: " + e.GetType().Name + " " + e.Message);
            }
        }
    }
}
