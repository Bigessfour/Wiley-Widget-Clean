using System;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.Tests;

namespace WileyWidget.UiTests;

public class FlaUISmokeTests : UiTestApplication
{
    [StaFact]
    public void Can_Attach_To_Desktop_And_Query()
    {
        using var automation = new UIA3Automation();
        var desktop = automation.GetDesktop();
        Assert.NotNull(desktop);

        // Just query for the desktop window pattern to validate pipeline works
        var firstWindow = desktop.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
        Assert.NotNull(firstWindow);
    }
}
