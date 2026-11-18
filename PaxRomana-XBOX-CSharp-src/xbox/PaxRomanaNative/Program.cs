// Entry point of the Xbox (UWP) app. MonoGame creates the window and runs the game loop; everything else is the
// shared game code under Game/.
using System;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace PaxRomanaNative
{
    public static class Program
    {
        static void Main()
        {
            var factory = new MonoGame.Framework.GameFrameworkViewSource<XboxGame>();
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
                if (w >= 640 && h >= 360)
                {
                    Graphics.PreferredBackBufferWidth = w;
                    Graphics.PreferredBackBufferHeight = h;
                }
            }
            catch (Exception)
            {
                // Not fatal: MonoGame falls back to the size of the visible area.
            }
        }
    }
}
