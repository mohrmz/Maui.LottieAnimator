using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Maui.LottieAnimator
{
    public partial class MainPage : ContentPage
    {
        private string[] animationFiles = Array.Empty<string>();
        private bool _updatingFromPlayer;
        private CancellationTokenSource? _frameRepeatCts;
        private readonly double[] _speedSteps = new[] { 0.5, 1.0, 1.5, 2.0 };
        private int _speedIndex = 1; 

        public MainPage()
        {
            InitializeComponent();
            LoadAnimations();
            LottiePlayer.ProgressChanged += OnProgressChanged;
            LottiePlayer.TimeChanged += OnTimeChanged;
            UpdateLoopVisual();
            ApplySpeed();
        }

        private void LoadAnimations()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string prefix = "Maui.LottieAnimator.Resources.Raw.Animations.";

            var resources = assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                         && r.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            animationFiles = resources;

            foreach (var res in resources)
            {
                string fileName = Path.GetFileNameWithoutExtension(res.Replace(prefix, ""));
                AnimationPicker.Items.Add(fileName);
            }

            if (resources.Length > 0)
            {
                LoadAnimation(resources[0]);
                AnimationPicker.SelectedIndex = 0;
            }
        }

        private void LoadAnimation(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null) return;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            LottiePlayer.LoadAnimationFromBytes(ms.ToArray());
            ConfigureProgressSlider();
            UpdateTimeLabels(0, LottiePlayer.AnimationDuration);
        }

        private void AnimationPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (AnimationPicker.SelectedIndex < 0) return;
            string selectedResource = animationFiles[AnimationPicker.SelectedIndex];
            LoadAnimation(selectedResource);
        }

        private void Play_Clicked(object sender, EventArgs e) => LottiePlayer.Play();
        private void Pause_Clicked(object sender, EventArgs e) => LottiePlayer.Pause();
        private void Stop_Clicked(object sender, EventArgs e) => LottiePlayer.Stop();
        private void Loop_Clicked(object sender, EventArgs e)
        {
            LottiePlayer.IsLooping = !LottiePlayer.IsLooping;
            UpdateLoopVisual();
        }

        private void ProgressSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_updatingFromPlayer) return;
            LottiePlayer.SeekProgress(e.NewValue);
        }

        private void PreviousFrame_Clicked(object sender, EventArgs e) => LottiePlayer.StepFrame(-1);
        private void NextFrame_Clicked(object sender, EventArgs e) => LottiePlayer.StepFrame(1);

        private void PreviousFrame_Pressed(object sender, EventArgs e) => StartFrameRepeat(-1);
        private void NextFrame_Pressed(object sender, EventArgs e) => StartFrameRepeat(1);
        private void FrameStep_Released(object sender, EventArgs e) => StopFrameRepeat();

        private void SpeedButton_Clicked(object sender, EventArgs e)
        {
            _speedIndex = (_speedIndex + 1) % _speedSteps.Length;
            ApplySpeed();
        }

        private void ApplySpeed()
        {
            double speed = _speedSteps[_speedIndex];
            LottiePlayer.PlaybackSpeed = speed;
            SpeedButton.Text = $"{speed:0.#}x";
        }

        private void ConfigureProgressSlider()
        {
            ProgressSlider.Minimum = 0;
            ProgressSlider.Maximum = 1;
            ProgressSlider.Value = 0;
        }

        private void OnProgressChanged(object? sender, double progress)
        {
            _updatingFromPlayer = true;
            ProgressSlider.Value = progress;
            _updatingFromPlayer = false;
        }

        private void OnTimeChanged(object? sender, double seconds)
        {
            UpdateTimeLabels(seconds, LottiePlayer.AnimationDuration);
        }

        private void UpdateTimeLabels(double currentSeconds, double totalSeconds)
        {
            string fmt(double s)
            {
                if (double.IsNaN(s) || double.IsInfinity(s) || s < 0)
                    return "00:00";
                return TimeSpan.FromSeconds(s).ToString(@"mm\:ss");
            }

            CurrentTimeLabel.Text = fmt(currentSeconds);
            TotalTimeLabel.Text = fmt(totalSeconds);
        }

        private void UpdateLoopVisual()
        {
            bool isOn = LottiePlayer.IsLooping;
            LoopButton.BackgroundColor = isOn ? Color.FromArgb("#c7f5c4") : Color.FromArgb("#f0f0f0");
            LoopButton.TextColor = isOn ? Colors.DarkGreen : Colors.Black;
        }

        private void StartFrameRepeat(int direction)
        {
            StopFrameRepeat();
            _frameRepeatCts = new CancellationTokenSource();
            var token = _frameRepeatCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    while (!token.IsCancellationRequested)
                    {
                        MainThread.BeginInvokeOnMainThread(() => LottiePlayer.StepFrame(direction));
                        await Task.Delay(120, token);
                    }
                }
                catch (TaskCanceledException)
                {
                   
                }
            }, token);
        }

        private void StopFrameRepeat()
        {
            _frameRepeatCts?.Cancel();
            _frameRepeatCts?.Dispose();
            _frameRepeatCts = null;
        }
    }
}
