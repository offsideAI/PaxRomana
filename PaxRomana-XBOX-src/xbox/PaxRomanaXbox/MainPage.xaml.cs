// Hosts the web game full screen and connects it to the controller.
//  - Every frame the controller state is posted into the page (the game reads it in src/gamepad.js), so input does not
//    depend on the embedded browser's own gamepad support.
//  - The page posts haptic levels back, and they are applied to all four motors: the two in the grips and the two
//    impulse motors in the triggers.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Gaming.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PaxRomanaXbox
{
    public sealed partial class MainPage : Page
    {
        private const string Host = "paxromana.game";
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly StringBuilder _json = new StringBuilder(256);
        private CoreWebView2 _core;
        private double _lastRumble = -10, _lastNone = -10;
        private bool _vibrating;

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Web.EnsureCoreWebView2Async();
                _core = Web.CoreWebView2;
                string folder = Path.Combine(Package.Current.InstalledLocation.Path, "web");
                _core.SetVirtualHostNameToFolderMapping(Host, folder, CoreWebView2HostResourceAccessKind.Allow);
                _core.Settings.AreDefaultContextMenusEnabled = false;
                _core.Settings.IsStatusBarEnabled = false;
                _core.Settings.IsZoomControlEnabled = false;
                _core.WebMessageReceived += OnWebMessage;
                _core.NavigationCompleted += (s, a) =>
                {
                    Status.Visibility = Visibility.Collapsed;
                    Web.Focus(FocusState.Programmatic);
                };
                _core.Navigate("https://" + Host + "/index.html?xbox=1");
                CompositionTarget.Rendering += OnFrame;
            }
            catch (Exception ex)
            {
                Status.Text = "Could not start the game view.\n" + ex.Message;
            }
        }

        // Controller -> game. Buttons are sent in the web "standard gamepad" order.
        private void OnFrame(object sender, object args)
        {
            if (_core == null) return;
            double now = _clock.Elapsed.TotalSeconds;

            // If the game stops sending haptics (paused, crashed, navigated away), do not leave the motors running.
            if (_vibrating && now - _lastRumble > 0.6) SetVibration(0, 0, 0, 0);

            var pads = Gamepad.Gamepads;
            if (pads.Count == 0)
            {
                if (now - _lastNone > 0.25) { _lastNone = now; Post("{\"t\":\"pad\",\"none\":true}"); }
                return;
            }
            GamepadReading r = pads[0].GetCurrentReading();
            GamepadButtons b = r.Buttons;
            var sb = _json;
            sb.Clear();
            sb.Append("{\"t\":\"pad\",\"a\":[");
            Num(sb, r.LeftThumbstickX); sb.Append(',');
            Num(sb, -r.LeftThumbstickY); sb.Append(',');       // web sticks are positive down
            Num(sb, r.RightThumbstickX); sb.Append(',');
            Num(sb, -r.RightThumbstickY);
            sb.Append("],\"b\":[");
            Bit(sb, b, GamepadButtons.A); Bit(sb, b, GamepadButtons.B); Bit(sb, b, GamepadButtons.X); Bit(sb, b, GamepadButtons.Y);
            Bit(sb, b, GamepadButtons.LeftShoulder); Bit(sb, b, GamepadButtons.RightShoulder);
            Num(sb, r.LeftTrigger); sb.Append(','); Num(sb, r.RightTrigger); sb.Append(',');
            Bit(sb, b, GamepadButtons.View); Bit(sb, b, GamepadButtons.Menu);
            Bit(sb, b, GamepadButtons.LeftThumbstick); Bit(sb, b, GamepadButtons.RightThumbstick);
            Bit(sb, b, GamepadButtons.DPadUp); Bit(sb, b, GamepadButtons.DPadDown); Bit(sb, b, GamepadButtons.DPadLeft);
            sb.Append((b & GamepadButtons.DPadRight) != 0 ? '1' : '0');
            sb.Append("]}");
            Post(sb.ToString());
        }

        private static void Num(StringBuilder sb, double v)
        {
            sb.Append(v.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void Bit(StringBuilder sb, GamepadButtons all, GamepadButtons one)
        {
            sb.Append((all & one) != 0 ? "1," : "0,");
        }

        private void Post(string json)
        {
            try { _core.PostWebMessageAsJson(json); }
            catch (Exception) { /* the page is navigating; the next frame will get through */ }
        }

        // Game -> controller: { "t": "rumble", "l": 0..1, "h": 0..1, "lt": 0..1, "rt": 0..1 }
        private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                JsonObject m = JsonObject.Parse(e.WebMessageAsJson);
                if (m.GetNamedString("t", "") != "rumble") return;
                SetVibration(m.GetNamedNumber("l", 0), m.GetNamedNumber("h", 0), m.GetNamedNumber("lt", 0), m.GetNamedNumber("rt", 0));
                _lastRumble = _clock.Elapsed.TotalSeconds;
            }
            catch (Exception) { /* ignore anything that is not a haptics message */ }
        }

        private void SetVibration(double low, double high, double leftTrigger, double rightTrigger)
        {
            var pads = Gamepad.Gamepads;
            if (pads.Count == 0) { _vibrating = false; return; }
            pads[0].Vibration = new GamepadVibration
            {
                LeftMotor = Clamp(low),
                RightMotor = Clamp(high),
                LeftTrigger = Clamp(leftTrigger),
                RightTrigger = Clamp(rightTrigger),
            };
            _vibrating = low + high + leftTrigger + rightTrigger > 0.001;
        }

        private static double Clamp(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }
}
