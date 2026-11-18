using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PaxRomanaXbox
{
    sealed partial class App : Application
    {
        public App()
        {
            // Let the game start its sound without a mouse click: on a console the first input is a controller button,
            // which the embedded browser does not count as a user gesture.
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Draw edge to edge on the television; the game keeps its own HUD inside the title-safe area.
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            // B is "crouch" in the game, not "leave the app".
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, args) => { args.Handled = true; };

            var root = Window.Current.Content as Frame;
            if (root == null)
            {
                root = new Frame();
                Window.Current.Content = root;
            }
            if (root.Content == null)
            {
                root.Navigate(typeof(MainPage), e.Arguments);
            }
            Window.Current.Activate();
        }
    }
}
