// Entry point of the Xbox (UWP) app. MonoGame creates the window and runs the game loop; everything else is the
// shared game code under Game/.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace PaxRomanaNative
{
    // A diary of the launch, written to startup.log in the app's local data folder, where the Xbox Device Portal can
    // read it (scripts/xbox-diagnose.sh fetches it). If the game stops before it can draw its own error screen, this
    // file says how far it got and why it stopped. It is rewritten on every launch.
    //
    // Nothing in this class may depend on MonoGame or on the game: it has to work even when those cannot be loaded.
    static class StartupLog
    {
        static readonly object gate = new object();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static string path;
        static int firstChance;
        [ThreadStatic] static bool writing;

        // Finds somewhere writable. The usual place is the app's LocalState folder; the others are fallbacks, so that
        // a failure of one way of finding it does not leave the launch without a record.
        public static void Begin()
        {
            string problems = "";
            for (int attempt = 0; attempt < 3 && path == null; attempt++)
            {
                try
                {
                    string folder = attempt == 0 ? LocalStateFolder()
                        : attempt == 1 ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                        : Path.GetTempPath();
                    string candidate = Path.Combine(folder, "startup.log");
                    File.WriteAllText(candidate, "");
                    path = candidate;
                }
                catch (Exception e) { problems += " [" + attempt + ": " + e.GetType().Name + " " + e.Message + "]"; }
            }
            Write("launch at " + DateTime.UtcNow.ToString("u") + ", log at " + path + (problems.Length > 0 ? ", earlier attempts failed:" + problems : ""));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string LocalStateFolder() { return Windows.Storage.ApplicationData.Current.LocalFolder.Path; }

        // What is running, and where: the package version tells one build from the next.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Describe()
        {
            try
            {
                Windows.ApplicationModel.PackageVersion v = Windows.ApplicationModel.Package.Current.Id.Version;
                Write("package " + Windows.ApplicationModel.Package.Current.Id.FullName + ", version " + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision);
            }
            catch (Exception e) { Write("could not read the package identity: " + e.GetType().Name + " " + e.Message); }
            try
            {
                ulong n = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
                Write("device " + AnalyticsInfo.VersionInfo.DeviceFamily + " " + (n >> 48) + "." + ((n >> 32) & 0xFFFF) + "." + ((n >> 16) & 0xFFFF) + "." + (n & 0xFFFF)
                    + ", runtime " + RuntimeInformation.FrameworkDescription + ", " + (IntPtr.Size * 8) + "-bit");
            }
            catch (Exception e) { Write("could not read the device details: " + e.GetType().Name + " " + e.Message); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        static extern IntPtr LoadLibraryExW(string name, IntPtr reserved, uint flags);

        // Records which of the system libraries the graphics and audio layer can call are on this console. A build
        // compiled with .NET Native needs every one of them to exist before the app can start at all, so this is
        // the list to check before turning .NET Native back on.
        [MethodImpl(MethodImplOptions.NoInlining)]
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

        // Every exception as it is thrown, including the ones that are caught and handled, up to a limit. This is
        // what names the file when something reports "file not found".
        public static void FirstChance(Exception e)
        {
            if (writing || firstChance >= 40) return;
            firstChance++;
            string text;
            try { text = e.ToString(); } catch (Exception) { text = e.GetType().FullName; }
            FileNotFoundException missing = e as FileNotFoundException;
            if (missing != null) text = "[file: " + missing.FileName + "] " + text;
            Write("exception " + firstChance + " (0x" + e.HResult.ToString("X8") + "): " + (text.Length > 1500 ? text.Substring(0, 1500) : text));
        }

        public static void Write(string line)
        {
            if (path == null || writing) return;
            lock (gate)
            {
                writing = true;
                try { File.AppendAllText(path, (clock.ElapsedMilliseconds / 1000.0).ToString("0.000") + "  " + line + "\r\n"); } catch (Exception) { }
                writing = false;
            }
        }
    }

    public static class Program
    {
        // Main itself touches nothing but the diary, so that a problem loading MonoGame or the game shows up as an
        // entry in the diary instead of stopping the program before its first line.
        static void Main()
        {
            StartupLog.Begin();
            try { AppDomain.CurrentDomain.FirstChanceException += (s, e) => StartupLog.FirstChance(e.Exception); }
            catch (Exception e) { StartupLog.Write("no first-chance exception reports: " + e.GetType().Name); }
            try { AppDomain.CurrentDomain.UnhandledException += (s, e) => StartupLog.Write("UNHANDLED: " + e.ExceptionObject); }
            catch (Exception e) { StartupLog.Write("no unhandled exception reports: " + e.GetType().Name); }
            try { TaskScheduler.UnobservedTaskException += (s, e) => StartupLog.Write("UNOBSERVED TASK ERROR: " + e.Exception); }
            catch (Exception) { }
            try { StartupLog.Describe(); } catch (Exception e) { StartupLog.Write("could not describe the app: " + e); }
            try { StartupLog.ProbeSystemLibraries(); } catch (Exception e) { StartupLog.Write("could not check the system libraries: " + e); }

            try
            {
                StartupLog.Write("loading the game");
                RunGame();
                StartupLog.Write("the app view returned (normal exit)");
            }
            catch (Exception e)
            {
                StartupLog.Write("STOPPED: " + e);
                throw;
            }
        }

        // Kept out of Main (never inlined) so that MonoGame and the game are only loaded once the diary is open.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RunGame()
        {
            PaxRomana.PaxGame.Log = StartupLog.Write;
            try
            {
                CoreApplication.UnhandledErrorDetected += (s, e) =>
                {
                    try { e.UnhandledError.Propagate(); }
                    catch (Exception ex) { StartupLog.Write("UNHANDLED (app model): " + ex); throw; }
                };
            }
            catch (Exception e) { StartupLog.Write("could not attach the app-model error handler: " + e.GetType().Name); }
            var factory = new MonoGame.Framework.GameFrameworkViewSource<XboxGame>();
            StartupLog.Write("starting the app view");
            CoreApplication.Run(factory);
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
