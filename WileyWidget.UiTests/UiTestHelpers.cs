using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace WileyWidget.UiTests;

public static class UiTestHelpers
{
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    public static IReadOnlyList<T> FindVisualChildrenWithRetry<T>(DependencyObject parent, int expectedMin = 1, int timeoutMs = 3000, int pollIntervalMs = 100) where T : DependencyObject
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<T> last = new();
        while (DateTime.UtcNow < deadline)
        {
            if (Application.Current?.Dispatcher != null)
            {
                last = Application.Current.Dispatcher.Invoke(() => FindVisualChildren<T>(parent).ToList());
            }
            else
            {
                last = FindVisualChildren<T>(parent).ToList();
            }
            if (last.Count >= expectedMin)
            {
                return last;
            }
            Thread.Sleep(pollIntervalMs);
            DoEvents();
        }
        return last;
    }

    // Simple message pump spin for WPF UI tests
    public static void DoEvents()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
