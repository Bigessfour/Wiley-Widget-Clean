using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.LifecycleTests;

public sealed class AnalyticsViewModelTests
{
    private readonly Mock<IGrokSupercomputer> _grokSupercomputerMock;
    private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
    private readonly AnalyticsViewModel _sut;

    public AnalyticsViewModelTests()
    {
        _grokSupercomputerMock = new Mock<IGrokSupercomputer>();
        _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();

        // Setup default mock behaviors
        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        _sut = new AnalyticsViewModel(
            _grokSupercomputerMock.Object,
            _enterpriseRepositoryMock.Object,
            new TestDispatcherHelper(),
            NullLogger<AnalyticsViewModel>.Instance);
    }

    [Fact]
    public async Task RefreshAnalyticsAsync_LoadsDataWithoutCrashing()
    {
        // Act
        await _sut.RefreshAnalyticsCommand.ExecuteAsync(null);

        // Assert - Should not throw exception
        _enterpriseRepositoryMock.Verify(repo => repo.GetAllAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void ChartSeriesCollection_InitializedAsEmptyCollection()
    {
        // Assert
        Assert.NotNull(_sut.ChartSeriesCollection);
        Assert.Empty(_sut.ChartSeriesCollection);
    }

    [Fact]
    public void GaugeCollection_InitializedAsEmptyCollection()
    {
        // Assert
        Assert.NotNull(_sut.GaugeCollection);
        Assert.Empty(_sut.GaugeCollection);
    }

    [Fact]
    public void Enterprises_InitializedAsEmptyCollection()
    {
        // Assert
        Assert.NotNull(_sut.Enterprises);
        Assert.Empty(_sut.Enterprises);
    }

    [Fact]
    public void FilterOptions_ContainsExpectedOptions()
    {
        // Assert
        Assert.NotNull(_sut.FilterOptions);
        Assert.Contains("All Data", _sut.FilterOptions);
    }

    [Fact]
    public void StartDate_DefaultsToThreeMonthsAgo()
    {
        // Assert
        var expected = DateTime.Today.AddMonths(-3);
        Assert.Equal(expected.Date, _sut.StartDate.Date);
    }

    [Fact]
    public void EndDate_DefaultsToToday()
    {
        // Assert
        Assert.Equal(DateTime.Today, _sut.EndDate.Date);
    }

    [Fact]
    public void RefreshAnalyticsCommand_CanExecuteWhenNotLoading()
    {
        // Assert
        Assert.True(_sut.RefreshAnalyticsCommand.CanExecute(null));
    }

    [Fact]
    public void DrillDownCommand_IsAvailable()
    {
        // Assert
        Assert.NotNull(_sut.DrillDownCommand);
    }
}