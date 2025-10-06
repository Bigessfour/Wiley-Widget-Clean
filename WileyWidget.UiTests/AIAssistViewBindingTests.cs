using System.Linq;
using System.Windows;
using Xunit;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

public class AIAssistViewBindingTests : UiTestApplication
{
    [StaFact, Trait("Category", "UI-Bindings")]
    public void Changing_Mode_Updates_FinancialInputs_Visibility()
    {
        WileyWidget.AIAssistView view = null;
        RunOnUIThread(() =>
        {
            view = new WileyWidget.AIAssistView();
            view.Show();
            view.UpdateLayout();
        });

        Assert.NotNull(view);

        // Initially in General mode - financial inputs should be hidden
        var doublesInitial = UiTestHelpers.FindVisualChildrenWithRetry<Syncfusion.Windows.Shared.DoubleTextBox>(view, expectedMin: 1);
        Assert.True(doublesInitial.Count > 0);
        Assert.True(doublesInitial.All(d => d.IsVisible == false));

        // Switch to ServiceCharge mode via command
        RunOnUIThread(() =>
        {
            var vm = (WileyWidget.ViewModels.AIAssistViewModel)view.DataContext;
            Assert.NotNull(vm);
            vm.SetConversationModeCommand.Execute("ServiceCharge");
        });

        // Allow UI to update and re-query
        UiTestHelpers.DoEvents();
        var doublesAfter = UiTestHelpers.FindVisualChildrenWithRetry<Syncfusion.Windows.Shared.DoubleTextBox>(view, expectedMin: 1);
        Assert.Contains(doublesAfter, d => d.IsVisible);

        // Cleanup
        RunOnUIThread(() => view.Close());
    }
}
