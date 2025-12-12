using System;
using System.IO;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Skottie;
using System.Reflection;
using System.Text.Json;
using Animation = SkiaSharp.Skottie.Animation;

namespace Maui.LottieAnimator.Controls
{
    public class LottiePlayerView : ContentView, IDisposable
    {
        private readonly SKCanvasView _canvasView;
        private Animation? _animation;
        private bool _isPlaying;
        private double _progress; 
        private double _frameRate = 60; 
        private double _playbackSpeed = 1.0; 
        private bool _isLooping;
        private double _timelineDurationSeconds; 
        private double _skottieDurationSeconds;  
        private (double? DurationSeconds, double? Fps, double? TotalFrames, double? InPoint, double? OutPoint) _lastParsedMetadata;
        private DateTime _lastUpdate;

        public LottiePlayerView()
        {
            _canvasView = new SKCanvasView();
            _canvasView.PaintSurface += OnPaintSurface;
            Content = _canvasView;
        }

        public bool IsPlaying => _isPlaying;
        public bool IsLooping
        {
            get => _isLooping;
            set => _isLooping = value;
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = Math.Max(0, Math.Min(1, value));
                SeekToProgress(_progress);
                ProgressChanged?.Invoke(this, _progress);
                TimeChanged?.Invoke(this, CurrentTime);
                FrameChanged?.Invoke(this, CurrentFrame);
            }
        }

        public event EventHandler<double>? ProgressChanged;
        public event EventHandler<double>? TimeChanged;

        public double AnimationDuration => _timelineDurationSeconds;

        public int TotalFrames =>
            _animation == null
                ? 0
                : GetTotalFrames();

        public int CurrentFrame =>
            TotalFrames <= 1
                ? 0
                : Math.Max(0, Math.Min(TotalFrames - 1,
                    (int)Math.Round(_progress * (TotalFrames - 1))));

        public double CurrentTime => AnimationDuration * _progress;

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Math.Max(0.1, Math.Min(4.0, value));
        }

        public event EventHandler<int>? FrameChanged;

        public void LoadAnimationFromBytes(byte[] bytes)
        {
            DisposeAnimation();

            using var skData = SKData.CreateCopy(bytes);
            _animation = Animation.Create(skData);

            var metadata = ParseMetadata(bytes);
            _frameRate = metadata.Fps ?? GetAnimationFps(_animation);
            _skottieDurationSeconds = _animation?.Duration.TotalSeconds ?? 0;
            _timelineDurationSeconds = metadata.DurationSeconds
                                       ?? _skottieDurationSeconds;
            _progress = 0;
            _isPlaying = false;
            Invalidate();
        }

        private double GetAnimationFps(Animation animation)
        {
            var fpsProp = animation.GetType().GetProperty("Fps", BindingFlags.Instance | BindingFlags.Public);
            if (fpsProp?.GetValue(animation) is float fpsFloat && fpsFloat > 0)
                return fpsFloat;
            if (fpsProp?.GetValue(animation) is double fpsDouble && fpsDouble > 0)
                return fpsDouble;

            return 60d;
        }

        private void DisposeAnimation()
        {
            _animation?.Dispose();
            _animation = null;
        }

        public void Play()
        {
            if (_animation == null || _isPlaying) return;

            _isPlaying = true;
            _lastUpdate = DateTime.Now;
            StartTimer();
        }

        public void Pause() => _isPlaying = false;

        public void Stop()
        {
            _isPlaying = false;
            Progress = 0;
        }

        private void StartTimer()
        {
            if (AnimationDuration <= 0) return;

            double timerInterval = 1000.0 / Math.Max(_frameRate, 60.0);
            
            Device.StartTimer(TimeSpan.FromMilliseconds(timerInterval), () =>
            {
                if (!_isPlaying || _animation == null) return false;

                var now = DateTime.Now;
                var elapsed = (now - _lastUpdate).TotalSeconds;
                _lastUpdate = now;

                double deltaProgress = (elapsed * _playbackSpeed) / AnimationDuration;
                Progress += deltaProgress;

                if (Progress >= 1)
                {
                    if (_isLooping)
                    {
                        Progress = 0;
                        return true;
                    }
                    Progress = 1;
                    _isPlaying = false;
                    return false;
                }

                return _isPlaying;
            });
        }

        public void StepFrame(int frames)
        {
            if (_animation == null || TotalFrames == 0) return;

            double frameProgress = frames / (double)TotalFrames;

            Progress = Math.Max(0, Math.Min(1, _progress + frameProgress));
        }

        public void SeekFrame(int frameIndex)
        {
            if (_animation == null || TotalFrames <= 1) return;

            int clamped = Math.Max(0, Math.Min(TotalFrames - 1, frameIndex));
            Progress = clamped / (double)(TotalFrames - 1);
        }

        public void Seek(double seconds)
        {
            if (_animation == null || AnimationDuration <= 0) return;
            double clamped = Math.Max(0, Math.Min(AnimationDuration, seconds));
            SeekProgress(clamped / AnimationDuration);
        }

        public void SeekProgress(double progress)
        {
            Progress = Math.Max(0, Math.Min(1, progress));
        }

        private void SeekToProgress(double progress)
        {
            if (_animation == null) return;

            double? targetFrame = null;
            if (_lastParsedMetadata.InPoint.HasValue &&
                _lastParsedMetadata.OutPoint.HasValue)
            {
                targetFrame = _lastParsedMetadata.InPoint.Value +
                              (progress * (_lastParsedMetadata.OutPoint.Value - _lastParsedMetadata.InPoint.Value));
            }

            bool seeked = false;
            if (targetFrame.HasValue)
            {
                var seekFrameMethod = _animation.GetType().GetMethod("SeekFrame", BindingFlags.Instance | BindingFlags.Public);
                if (seekFrameMethod != null)
                {
                    seekFrameMethod.Invoke(_animation, new object[] { (float)targetFrame.Value, null });
                    seeked = true;
                }
                else
                {
                    if (_lastParsedMetadata.Fps.HasValue && _lastParsedMetadata.Fps.Value > 0)
                    {
                        double time = targetFrame.Value / _lastParsedMetadata.Fps.Value;
                        _animation.Seek(time, null);
                        seeked = true;
                    }
                }
            }

            if (!seeked)
            {
                double time = progress * AnimationDuration;
                _animation.Seek(time, null);
            }

            Invalidate();
        }

        private int GetTotalFrames()
        {
            var frames = _lastParsedMetadata.TotalFrames;
            if (frames.HasValue)
                return Math.Max(1, (int)Math.Round(frames.Value));
            return Math.Max(1, (int)Math.Round(AnimationDuration * _frameRate));
        }

        private (double? DurationSeconds, double? Fps, double? TotalFrames, double? InPoint, double? OutPoint) ParseMetadata(byte[] bytes)
        {
            try
            {
                using var doc = JsonDocument.Parse(bytes);
                var root = doc.RootElement;
                double? fr = TryGetDouble(root, "fr");
                double? ip = TryGetDouble(root, "ip");
                double? op = TryGetDouble(root, "op");

                double? duration = null;
                double? totalFrames = null;
                if (fr.HasValue && ip.HasValue && op.HasValue && fr.Value > 0)
                {
                    duration = (op.Value - ip.Value) / fr.Value;
                    totalFrames = (op.Value - ip.Value);
                }

                _lastParsedMetadata = (duration, fr, totalFrames, ip, op);
                return _lastParsedMetadata;
            }
            catch
            {
                _lastParsedMetadata = (null, null, null, null, null);
                return _lastParsedMetadata;
            }
        }

        private double? TryGetDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            return prop.ValueKind switch
            {
                JsonValueKind.Number when prop.TryGetDouble(out var d) => d,
                _ => null
            };
        }

        private void Invalidate() => _canvasView.InvalidateSurface();

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (_animation == null)
            {
                using var paint = new SKPaint { Color = SKColors.LightGray };
                canvas.DrawRect(e.Info.Rect, paint);
                return;
            }

            var dstRect = CalculateDestinationRect(
                e.Info.Width, e.Info.Height,
                _animation.Size.Width, _animation.Size.Height);

            _animation.Render(canvas, dstRect);
        }

        private SKRect CalculateDestinationRect(int canvasW, int canvasH, float srcW, float srcH)
        {
            float scale = Math.Min(canvasW / srcW, canvasH / srcH);
            float w = srcW * scale;
            float h = srcH * scale;
            float left = (canvasW - w) / 2f;
            float top = (canvasH - h) / 2f;

            return new SKRect(left, top, left + w, top + h);
        }

        public void Dispose()
        {
            DisposeAnimation();
            _canvasView.PaintSurface -= OnPaintSurface;
        }
    }
}
