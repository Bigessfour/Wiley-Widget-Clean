using System.Linq;
using System.Windows;
using Xunit;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

public class AIAssistViewParameterizedTests : UiTestApplication
{
    [StaFact, Trait("Category", "UI-Themes")]
    public void AIAssistView_Loads_With_Themes()
    {
        foreach (var theme in new[] { "FluentDark", "FluentLight" })
        {
            WileyWidget.Services.SettingsService.Instance.Current.Theme = theme;

            WileyWidget.AIAssistView view = null;
            Window window = null;
            RunOnUIThread(() =>
            {
                view = new WileyWidget.AIAssistView();
                window = new Window { Content = view };
                window.Show();
                window.UpdateLayout();
            });

            Assert.NotNull(view);
            var doubles = UiTestHelpers.FindVisualChildrenWithRetry<System.Windows.Controls.Control>(window, expectedMin: 1);
            Assert.True(doubles.Count > 0);

            RunOnUIThread(() => window.Close());
        }
    }
}
