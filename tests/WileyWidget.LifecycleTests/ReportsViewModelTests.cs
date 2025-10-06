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

public sealed class ReportsViewModelTests
{
    private readonly Mock<IGrokSupercomputer> _grokSupercomputerMock;
    private readonly Mock<IReportExportService> _reportExportServiceMock;
    private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
    private readonly ReportsViewModel _sut;

    public ReportsViewModelTests()
    {
        _grokSupercomputerMock = new Mock<IGrokSupercomputer>();
        _reportExportServiceMock = new Mock<IReportExportService>();
        _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();

        // Setup default mock behaviors
        var sampleReportData = new ReportDataModel(1, DateTime.Today.AddMonths(-1), DateTime.Today, "Test", new List<EnterpriseMetric>());
        _grokSupercomputerMock
            .Setup(grok => grok.FetchEnterpriseDataAsync(It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>()))
            .ReturnsAsync(sampleReportData);

        var sampleAnalytics = new AnalyticsResult(sampleReportData, new List<ChartSeries>(), new List<KpiMetric>(), null, null);
        _grokSupercomputerMock
            .Setup(grok => grok.RunReportCalcsAsync(It.IsAny<ReportDataModel>()))
            .ReturnsAsync(sampleAnalytics);

        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        _sut = new ReportsViewModel(
            _grokSupercomputerMock.Object,
            _reportExportServiceMock.Object,
            _enterpriseRepositoryMock.Object,
            new TestDispatcherHelper(),
            NullLogger<ReportsViewModel>.Instance);
    }

    [Fact]
    public async Task GenerateReportAsync_LoadsDataWithoutCrashing()
    {
        // Act
        await _sut.GenerateReportCommand.ExecuteAsync(null);

        // Assert - Should not throw exception
        Assert.NotNull(_sut.ReportItems);
        _grokSupercomputerMock.Verify(grok => grok.FetchEnterpriseDataAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), ""), Times.Once);
    }

    [Fact]
    public async Task ExportToPdfAsync_ExportsWithCorrectData()
    {
        // Arrange
        await _sut.GenerateReportCommand.ExecuteAsync(null); // Generate report first

        // Act
        await _sut.ExportCommand.ExecuteAsync("pdf");

        // Assert
        _reportExportServiceMock.Verify(service => service.ExportToPdfAsync(It.IsAny<ReportDataModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportToExcelAsync_ExportsWithCorrectData()
    {
        // Arrange
        await _sut.GenerateReportCommand.ExecuteAsync(null); // Generate report first

        // Act
        await _sut.ExportCommand.ExecuteAsync("excel");

        // Assert
        _reportExportServiceMock.Verify(service => service.ExportToExcelAsync(It.IsAny<ReportDataModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ReportItems_InitializedAsEmptyCollection()
    {
        // Assert
        Assert.NotNull(_sut.ReportItems);
        Assert.Empty(_sut.ReportItems);
    }

    [Fact]
    public void Enterprises_InitializedAsEmptyCollection()
    {
        // Assert
        Assert.NotNull(_sut.Enterprises);
        Assert.Empty(_sut.Enterprises);
    }

    [Fact]
    public void StartDate_DefaultsToOneMonthAgo()
    {
        // Assert
        var expected = DateTime.Today.AddMonths(-1);
        Assert.Equal(expected.Date, _sut.StartDate.Date);
    }

    [Fact]
    public void EndDate_DefaultsToToday()
    {
        // Assert
        Assert.Equal(DateTime.Today, _sut.EndDate.Date);
    }

    [Fact]
    public void GenerateReportCommand_CanExecuteWhenNotLoading()
    {
        // Assert
        Assert.True(_sut.GenerateReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportCommand_CannotExecuteWhenNoReportGenerated()
    {
        // Assert
        Assert.False(_sut.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportCommand_CanExecuteAfterReportGenerated()
    {
        // Arrange
        await _sut.GenerateReportCommand.ExecuteAsync(null);

        // Assert
        Assert.True(_sut.ExportCommand.CanExecute(null));
    }
}