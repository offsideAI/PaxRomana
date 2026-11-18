// Two-player Pong. Left stick (or D-pad) moves your paddle, A starts a match, first to 7 wins.
// With one controller connected the right paddle is played by the console.
// On a PC: W/S or Up/Down move the left paddle, Space or Enter starts.
using System;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace WSBX
{
    public sealed partial class MainPage : Page
    {
        // All game coordinates are in "arena heights": y runs 0..1 top to bottom, x runs 0..aspect.
        private const double PaddleHeight = 0.2;
        private const double PaddleWidth = 0.022;
        private const double PaddleInset = 0.07;
        private const double PaddleSpeed = 1.5;
        private const double BallRadius = 0.016;
        private const double ServeSpeed = 0.95;
        private const double MaxSpeed = 2.3;
        private const double DeadZone = 0.18;
        private const int WinScore = 7;

        private enum Phase { Title, Serving, Playing, Over }

        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Random _random = new Random();
        private readonly double[] _paddleY = { 0.5, 0.5 };
        private readonly int[] _score = { 0, 0 };
        private readonly double[] _rumble = { 0, 0 };
        private Phase _phase = Phase.Title;
        private double _lastTime, _ballX, _ballY, _velX, _velY, _speed, _serveTimer, _aspect = 16.0 / 9.0;
        private int _serveDirection = 1;
        private bool _startWasDown;

        public MainPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                _clock.Start();
                _lastTime = 0;
                CenterBall();
                CompositionTarget.Rendering += OnFrame;
            };
            Unloaded += (s, e) => CompositionTarget.Rendering -= OnFrame;
        }

        private void OnFrame(object sender, object args)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Min(now - _lastTime, 0.05);
            _lastTime = now;

            double width = Arena.ActualWidth, height = Arena.ActualHeight;
            if (width < 1 || height < 1) return;
            _aspect = width / height;

            var pads = Gamepad.Gamepads;
            Gamepad padOne = pads.Count > 0 ? pads[0] : null;
            Gamepad padTwo = pads.Count > 1 ? pads[1] : null;

            // start / restart
            bool startDown = KeyDown(VirtualKey.Space) || KeyDown(VirtualKey.Enter);
            foreach (var pad in pads)
            {
                if ((pad.GetCurrentReading().Buttons & GamepadButtons.A) != 0) startDown = true;
            }
            if (startDown && !_startWasDown && (_phase == Phase.Title || _phase == Phase.Over)) StartMatch();
            _startWasDown = startDown;

            // paddles
            double moveOne = PadAxis(padOne);
            if (KeyDown(VirtualKey.W) || KeyDown(VirtualKey.Up)) moveOne = -1;
            if (KeyDown(VirtualKey.S) || KeyDown(VirtualKey.Down)) moveOne = 1;
            double moveTwo = padTwo != null ? PadAxis(padTwo) : ComputerAxis();
            MovePaddle(0, moveOne, dt);
            MovePaddle(1, moveTwo, dt);

            // ball
            if (_phase == Phase.Serving)
            {
                _serveTimer -= dt;
                if (_serveTimer <= 0) Serve();
            }
            else if (_phase == Phase.Playing)
            {
                StepBall(dt);
            }

            // controller rumble runs out
            StopRumble(0, padOne, dt);
            StopRumble(1, padTwo, dt);

            Draw(width, height, padTwo != null);
        }

        private static bool KeyDown(VirtualKey key)
        {
            return (Window.Current.CoreWindow.GetAsyncKeyState(key) & CoreVirtualKeyStates.Down) != 0;
        }

        // -1 = up, +1 = down
        private static double PadAxis(Gamepad pad)
        {
            if (pad == null) return 0;
            var reading = pad.GetCurrentReading();
            if ((reading.Buttons & GamepadButtons.DPadUp) != 0) return -1;
            if ((reading.Buttons & GamepadButtons.DPadDown) != 0) return 1;
            double y = reading.LeftThumbstickY;
            return Math.Abs(y) < DeadZone ? 0 : -y;
        }

        // The console's paddle follows the ball when it is coming, a little slower than a player can move.
        private double ComputerAxis()
        {
            double target = _velX > 0 && _phase == Phase.Playing ? _ballY : 0.5;
            double delta = target - _paddleY[1];
            if (Math.Abs(delta) < 0.03) return 0;
            return Math.Max(-0.72, Math.Min(0.72, delta * 6));
        }

        private void MovePaddle(int player, double axis, double dt)
        {
            double half = PaddleHeight / 2;
            _paddleY[player] = Math.Max(half, Math.Min(1 - half, _paddleY[player] + axis * PaddleSpeed * dt));
        }

        private void StartMatch()
        {
            _score[0] = 0;
            _score[1] = 0;
            _serveDirection = _random.Next(2) == 0 ? -1 : 1;
            BeginServe();
        }

        private void BeginServe()
        {
            CenterBall();
            _phase = Phase.Serving;
            _serveTimer = 0.9;
        }

        private void CenterBall()
        {
            _ballX = _aspect / 2;
            _ballY = 0.5;
            _velX = 0;
            _velY = 0;
        }

        private void Serve()
        {
            _speed = ServeSpeed;
            double angle = (_random.NextDouble() - 0.5) * 0.9;
            _velX = Math.Cos(angle) * _speed * _serveDirection;
            _velY = Math.Sin(angle) * _speed;
            _phase = Phase.Playing;
        }

        private void StepBall(double dt)
        {
            double previousX = _ballX;
            _ballX += _velX * dt;
            _ballY += _velY * dt;

            // top and bottom walls
            if (_ballY < BallRadius) { _ballY = BallRadius; _velY = Math.Abs(_velY); }
            if (_ballY > 1 - BallRadius) { _ballY = 1 - BallRadius; _velY = -Math.Abs(_velY); }

            // paddles: the ball bounces when it crosses the paddle's front face while level with the paddle
            double leftFace = PaddleInset + PaddleWidth, rightFace = _aspect - PaddleInset - PaddleWidth;
            if (_velX < 0 && previousX - BallRadius >= leftFace && _ballX - BallRadius < leftFace && Touches(0))
            {
                _ballX = leftFace + BallRadius;
                Bounce(0, 1);
            }
            else if (_velX > 0 && previousX + BallRadius <= rightFace && _ballX + BallRadius > rightFace && Touches(1))
            {
                _ballX = rightFace - BallRadius;
                Bounce(1, -1);
            }

            // out of play
            if (_ballX < -BallRadius * 2) Point(1);
            else if (_ballX > _aspect + BallRadius * 2) Point(0);
        }

        private bool Touches(int player)
        {
            return Math.Abs(_ballY - _paddleY[player]) <= PaddleHeight / 2 + BallRadius;
        }

        // Where the ball meets the paddle sets the return angle; every return is a little faster.
        private void Bounce(int player, int direction)
        {
            double offset = Math.Max(-1, Math.Min(1, (_ballY - _paddleY[player]) / (PaddleHeight / 2)));
            double angle = offset * Math.PI / 3.2;
            _speed = Math.Min(MaxSpeed, _speed * 1.06);
            _velX = Math.Cos(angle) * _speed * direction;
            _velY = Math.Sin(angle) * _speed;
            Rumble(player, 0.5, 0.1);
        }

        private void Point(int scorer)
        {
            _score[scorer]++;
            Rumble(1 - scorer, 0.9, 0.35);
            if (_score[scorer] >= WinScore)
            {
                _phase = Phase.Over;
                CenterBall();
            }
            else
            {
                _serveDirection = scorer == 0 ? 1 : -1;     // serve towards whoever just lost the point
                BeginServe();
            }
        }

        private void Rumble(int player, double strength, double seconds)
        {
            var pads = Gamepad.Gamepads;
            if (player >= pads.Count) return;
            pads[player].Vibration = new GamepadVibration { LeftMotor = strength, RightMotor = strength };
            _rumble[player] = seconds;
        }

        private void StopRumble(int player, Gamepad pad, double dt)
        {
            if (_rumble[player] <= 0) return;
            _rumble[player] -= dt;
            if (_rumble[player] <= 0 && pad != null) pad.Vibration = new GamepadVibration();
        }

        private void Draw(double width, double height, bool twoPlayers)
        {
            double pw = PaddleWidth * height, ph = PaddleHeight * height, d = BallRadius * 2 * height;

            PaddleLeft.Width = pw; PaddleLeft.Height = ph;
            Canvas.SetLeft(PaddleLeft, PaddleInset * height);
            Canvas.SetTop(PaddleLeft, _paddleY[0] * height - ph / 2);

            PaddleRight.Width = pw; PaddleRight.Height = ph;
            Canvas.SetLeft(PaddleRight, width - PaddleInset * height - pw);
            Canvas.SetTop(PaddleRight, _paddleY[1] * height - ph / 2);

            Ball.Width = d; Ball.Height = d;
            Canvas.SetLeft(Ball, _ballX * height - d / 2);
            Canvas.SetTop(Ball, _ballY * height - d / 2);
            Ball.Visibility = _phase == Phase.Title || _phase == Phase.Over ? Visibility.Collapsed : Visibility.Visible;

            Net.Width = 2; Net.Height = height;
            Canvas.SetLeft(Net, width / 2 - 1);

            ScoreLeft.Text = _score[0].ToString();
            ScoreRight.Text = _score[1].ToString();
            Canvas.SetLeft(ScoreLeft, width * 0.25 - ScoreLeft.ActualWidth / 2);
            Canvas.SetLeft(ScoreRight, width * 0.75 - ScoreRight.ActualWidth / 2);
            Canvas.SetTop(ScoreLeft, height * 0.04);
            Canvas.SetTop(ScoreRight, height * 0.04);

            if (_phase == Phase.Title)
            {
                Banner.Text = "WSBX PONG";
                Message.Text = (twoPlayers ? "Two controllers connected" : "One player against the console. Connect a second controller for two players")
                    + "\nLeft stick moves your paddle. Press A to start";
                Banner.Visibility = Message.Visibility = Visibility.Visible;
            }
            else if (_phase == Phase.Over)
            {
                Banner.Text = _score[0] > _score[1] ? "BLUE WINS" : "ORANGE WINS";
                Message.Text = "Press A to play again";
                Banner.Visibility = Message.Visibility = Visibility.Visible;
            }
            else
            {
                Banner.Visibility = Message.Visibility = Visibility.Collapsed;
            }
        }
    }
}
