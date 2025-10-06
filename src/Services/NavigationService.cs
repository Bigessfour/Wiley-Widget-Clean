using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of INavigationService that manages navigation between views using a Frame control.
/// Maintains navigation history and supports dependency injection for view model resolution.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly Frame _frame;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NavigationService>? _logger;
    private readonly Stack<object> _navigationHistory = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="frame">The Frame control to use for navigation.</param>
    /// <param name="serviceProvider">The service provider for resolving view models.</param>
    /// <param name="logger">Optional logger for navigation events.</param>
    public NavigationService(Frame frame, IServiceProvider serviceProvider, ILogger<NavigationService>? logger = null)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;

        // Subscribe to frame navigation events
        _frame.Navigated += OnFrameNavigated;
    }

    /// <summary>
    /// Gets a value indicating whether the navigation service can navigate back.
    /// </summary>
    public bool CanGoBack => _navigationHistory.Count > 0;

    /// <summary>
    /// Gets the current content being displayed.
    /// </summary>
    public object? CurrentContent => _frame.Content;

    /// <summary>
    /// Navigates to the specified view model or content.
    /// </summary>
    /// <param name="content">The view model or content to navigate to.</param>
    /// <param name="parameter">Optional navigation parameter.</param>
    public void Navigate(object content, object? parameter = null)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        // Store current content in history if it exists
        if (_frame.Content != null)
        {
            _navigationHistory.Push(_frame.Content);
        }

        // Set the new content
        _frame.Content = content;

        _logger?.LogInformation("Navigated to {ContentType}: {Content}",
            content.GetType().Name, content);

        // Raise navigation event
        Navigated?.Invoke(this, new NavigationEventArgs(content, parameter, _navigationHistory.Count > 0 ? _navigationHistory.Peek() : null));
    }

    /// <summary>
    /// Navigates to a view model of the specified type.
    /// </summary>
    /// <typeparam name="TViewModel">The type of view model to navigate to.</typeparam>
    /// <param name="parameter">Optional navigation parameter.</param>
    public void Navigate<TViewModel>(object? parameter = null) where TViewModel : class
    {
        try
        {
            // Resolve the view model from the service provider
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            Navigate(viewModel, parameter);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to navigate to {ViewModelType}", typeof(TViewModel).Name);
            throw;
        }
    }

    /// <summary>
    /// Navigates back to the previous view in the navigation history.
    /// </summary>
    public void GoBack()
    {
        if (!CanGoBack)
        {
            throw new InvalidOperationException("Cannot go back - no navigation history available.");
        }

        var previousContent = _navigationHistory.Pop();
        var currentContent = _frame.Content;

        _frame.Content = previousContent;

        _logger?.LogInformation("Navigated back from {FromContent} to {ToContent}",
            currentContent?.GetType().Name ?? "null", previousContent.GetType().Name);

        // Raise navigation event
        Navigated?.Invoke(this, new NavigationEventArgs(previousContent, null, currentContent));
    }

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    public void ClearHistory()
    {
        _navigationHistory.Clear();
        _logger?.LogInformation("Navigation history cleared");
    }

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<NavigationEventArgs>? Navigated;

    /// <summary>
    /// Handles the Frame.Navigated event.
    /// </summary>
    private void OnFrameNavigated(object? sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        // Additional handling if needed for Frame-specific navigation events
        _logger?.LogDebug("Frame navigation completed to {ContentType}",
            e.Content?.GetType().Name ?? "null");
    }
}