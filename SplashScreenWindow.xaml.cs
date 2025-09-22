using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Syncfusion.SfSkinManager;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;

namespace WileyWidget;

/// <summary>
/// Custom splash screen window with progress tracking
/// </summary>
public partial class SplashScreenWindow : Window, INotifyPropertyChanged
{
    private string _statusText = "Initializing application...";
    private double _progressValue = 0;
    private bool _isIndeterminate = false;
    private DateTime _startTime;
    private double _estimatedTotalTime = 3000; // 3 seconds default
    private readonly Dictionary<double, string> _progressMessages = new()
    {
        [0] = "Initializing core systems...",
        [20] = "Loading configuration...",
        [40] = "Connecting to database...",
        [60] = "Loading user interface...",
        [80] = "Finalizing setup...",
        [100] = "Ready to launch!"
    };

    public event PropertyChangedEventHandler PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
            // Update UI on UI thread
            Dispatcher.Invoke(() =>
            {
                if (FindName("StatusTextBlock") is TextBlock tb)
                {
                    tb.Text = value;
                }
            });
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (_progressValue != value)
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));

                // Update UI on UI thread with smooth transition
                Dispatcher.Invoke(() =>
                {
                    var pb = FindName("ProgressBar") as System.Windows.Controls.ProgressBar;
                    if (pb != null)
                    {
                        var animation = new DoubleAnimation(
                            pb.Value,
                            Math.Min(100, Math.Max(0, value)),
                            TimeSpan.FromMilliseconds(300))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        pb.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
                    }
                });
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            _isIndeterminate = value;
            OnPropertyChanged(nameof(IsIndeterminate));
            // Update UI on UI thread
            Dispatcher.Invoke(() =>
            {
                var pb = FindName("ProgressBar") as System.Windows.Controls.ProgressBar;
                if (pb != null)
                {
                    pb.IsIndeterminate = value;
                }
            });
        }
    }

    public SplashScreenWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Apply advanced Fluent Dark theme features
        ApplyAdvancedThemeFeatures();

        // Start with indeterminate progress
        IsIndeterminate = true;
        StatusText = "Starting Wiley Widget...";
        
        // Initialize progress tracking
        StartProgressTracking();
        
        // Handle DPI scaling
        var dpiScale = VisualTreeHelper.GetDpi(this);
        if (dpiScale.DpiScaleX > 1 || dpiScale.DpiScaleY > 1)
        {
            Width *= dpiScale.DpiScaleX;
            Height *= dpiScale.DpiScaleY;
            // Adjust font sizes proportionally
            ScaleFonts(dpiScale);
        }
    }

    /// <summary>
    /// Scales fonts for high-DPI displays
    /// </summary>
    private void ScaleFonts(DpiScale dpiScale)
    {
        var scale = Math.Max(dpiScale.DpiScaleX, dpiScale.DpiScaleY);
        
        // Find and scale text elements
        var titleTextBlock = (TextBlock)FindName("TitleTextBlock");
        if (titleTextBlock != null)
            titleTextBlock.FontSize *= scale;
            
        var statusTextBlock = (TextBlock)FindName("StatusTextBlock");
        if (statusTextBlock != null)
            statusTextBlock.FontSize *= scale;
            
        // Scale other text elements
        var progressTextBlock = (TextBlock)FindName("ProgressTextBlock");
        if (progressTextBlock != null)
            progressTextBlock.FontSize *= scale;
    }

    // Image-based background removed; XAML now provides gradient background directly.

    /// <summary>
    /// Starts progress tracking for better time estimation
    /// </summary>
    public void StartProgressTracking()
    {
        _startTime = DateTime.Now;
        IsIndeterminate = true;
        StatusText = "Preparing application...";
    }

    /// <summary>
    /// Updates progress with smart time estimation
    /// </summary>
    public void UpdateProgress(double actualProgress)
    {
        var elapsed = DateTime.Now - _startTime;
        var estimatedTotal = elapsed.TotalMilliseconds / Math.Max(actualProgress / 100.0, 0.01);
        _estimatedTotalTime = estimatedTotal;
        
        ProgressValue = actualProgress;
        IsIndeterminate = false;
    }

    /// <summary>
    /// Updates the progress and status text with enhanced messaging
    /// </summary>
    public void UpdateProgress(double progress, string statusText)
    {
        // Use smart progress estimation
        UpdateProgress(progress);
        
        // Use contextual message if available
        if (_progressMessages.TryGetValue(Math.Round(progress / 10) * 10, out var contextualMessage))
        {
            StatusText = contextualMessage;
        }
        else
        {
            StatusText = GetEnhancedStatusText(statusText);
        }
        
        IsIndeterminate = progress < 10; // Keep indeterminate for first 10%
    }

    /// <summary>
    /// Gets enhanced status text with engaging messages
    /// </summary>
    private string GetEnhancedStatusText(string baseText)
    {
        var enhancements = new[]
        {
            "Loading modules...",
            "Connecting to services...",
            "Initializing components...",
            "Preparing workspace...",
            "Almost ready..."
        };

        // Add some variety to status messages
        if (baseText.Contains("Loading") || baseText.Contains("Initializing"))
        {
            var random = new Random();
            return enhancements[random.Next(enhancements.Length)];
        }

        return baseText;
    }

    /// <summary>
    /// Completes the loading process with enhanced messaging and celebration
    /// </summary>
    public async void Complete()
    {
        ProgressValue = 100;
        StatusText = "Welcome to Wiley Widget!";
        IsIndeterminate = false;

        // Success animation sequence
        await Task.Delay(300);
        
        // Quick success flash
        var successFlash = new DoubleAnimation(1, 1.1, TimeSpan.FromMilliseconds(150))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        var transform = RenderTransform as ScaleTransform ?? new ScaleTransform();
        if (RenderTransform == null)
        {
            RenderTransform = transform;
        }
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, successFlash);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, successFlash);
        
        await Task.Delay(500);
        StatusText = "Application ready for use";
    }

    /// <summary>
    /// Gets system information for display
    /// </summary>
    private string GetSystemInfo()
    {
        try
        {
            var osVersion = Environment.OSVersion;
            var framework = RuntimeInformation.FrameworkDescription;
            var processorCount = Environment.ProcessorCount;

            return $"{framework} • Windows {osVersion.Version.Major}.{osVersion.Version.Minor} • {processorCount} CPU cores";
        }
        catch
        {
            return ".NET 9.0 • Windows 11 • Enterprise Edition";
        }
    }

    /// <summary>
    /// Applies advanced Syncfusion Fluent Dark theme features with enhanced animations
    /// </summary>
    private void ApplyAdvancedThemeFeatures()
    {
        // Apply acrylic background effect
        using (var fluentTheme = new FluentTheme
        {
            ThemeName = "FluentDark",
            // Reduce transparency for better readability on splash
            // Disable acrylic to avoid see-through background on some displays
            ShowAcrylicBackground = false,
            HoverEffectMode = HoverEffect.BackgroundAndBorder,
            PressedEffectMode = PressedEffect.Reveal
        })
        {
            SfSkinManager.SetTheme(this, fluentTheme);
        }

        // Enhanced fade-in with smooth, faster transition
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase 
            { 
                EasingMode = EasingMode.EaseOut
            }
        };
        BeginAnimation(OpacityProperty, fadeIn);
        
        // Add subtle scale animation with faster, smoother timing
        var scaleX = new DoubleAnimation(0.98, 1, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleY = new DoubleAnimation(0.98, 1, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        var transform = new ScaleTransform();
        RenderTransform = transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    /// <summary>
    /// Shows the splash screen and starts the loading animation
    /// </summary>
    public new void Show()
    {
        base.Show();

        // Start the loading animation if available
        var animation = (Storyboard)Resources["LoadingAnimation"];
        if (animation != null)
        {
            animation.Begin();
        }

        // Start loading dots animation
        var dotsAnimation = (Storyboard)Resources["LoadingDotsAnimation"];
        if (dotsAnimation != null)
        {
            dotsAnimation.Begin();
        }
    }

    /// <summary>
    /// Hides the splash screen with a fade effect
    /// </summary>
    public async Task HideAsync()
    {
        // Kept for backward-compatibility; prefer FadeOutAndCloseAsync()
        await FadeOutAndCloseAsync();
    }

    /// <summary>
    /// Fades out the splash and closes the window on the UI thread
    /// </summary>
    public async Task FadeOutAndCloseAsync()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
            BeginAnimation(OpacityProperty, fadeOut);
        });

        await Task.Delay(500);

        await Dispatcher.InvokeAsync(() =>
        {
            try
            {
                Close();
            }
            catch
            {
                // Fallback to Hide if Close throws due to lifecycle edge cases
                try { Hide(); } catch { /* no-op */ }
            }
        });
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Handles keyboard navigation for accessibility
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == Key.Escape)
        {
            // Allow user to skip splash if needed
            OnSplashCompleted();
        }
        else if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            // Speed up loading if user is impatient
            if (ProgressValue < 90)
            {
                ProgressValue = Math.Min(90, ProgressValue + 10);
            }
        }
    }

    /// <summary>
    /// Called when splash screen should be completed (either naturally or via user interaction)
    /// </summary>
    private void OnSplashCompleted()
    {
        ProgressValue = 100;
        StatusText = "Completing setup...";
        IsIndeterminate = false;
        
        // Trigger completion after a short delay
        Dispatcher.Invoke(async () =>
        {
            await Task.Delay(200);
            Complete();
        });
    }
}