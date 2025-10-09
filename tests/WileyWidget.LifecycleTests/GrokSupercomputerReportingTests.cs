 using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.LifecycleTests;

public sealed class GrokSupercomputerReportingTests
{
    private readonly Mock<IAIService> _aiServiceMock;
    private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
    private readonly GrokSupercomputer _sut;
    private readonly List<Enterprise> _sampleEnterprises;

    public GrokSupercomputerReportingTests()
    {
        _aiServiceMock = new Mock<IAIService>();
        _aiServiceMock
            .Setup(service => service.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("analysis");

        _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();

        _sampleEnterprises = CreateSampleEnterprises();

        _enterpriseRepositoryMock
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(_sampleEnterprises);

        _enterpriseRepositoryMock
            .Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => _sampleEnterprises.FirstOrDefault(e => e.Id == id));

        _sut = new GrokSupercomputer(
            _aiServiceMock.Object,
            _enterpriseRepositoryMock.Object,
            NullLogger<GrokSupercomputer>.Instance);
    }

    [Fact]
    public async Task FetchEnterpriseDataAsync_ReturnsAllEnterprisesAndCalculatesMetrics()
    {
        var result = await _sut.FetchEnterpriseDataAsync();

        Assert.Null(result.EnterpriseId);
        Assert.Equal(5, result.Enterprises.Count);

        var firstMetric = Assert.Single(result.Enterprises.Where(m => m.Id == 1));
        Assert.Equal(4000m, firstMetric.Revenue);
        Assert.Equal(3000m, firstMetric.Expenses);
        Assert.Equal(33.33m, firstMetric.RoiPercentage);
        Assert.Equal(25m, firstMetric.ProfitMarginPercentage);
    }

    [Fact]
    public async Task RunReportCalcsAsync_ComputesAggregatesAndReturnsVisualizationData()
    {
        var reportData = await _sut.FetchEnterpriseDataAsync();

        var analytics = await _sut.RunReportCalcsAsync(reportData);

        Assert.Equal(3, analytics.ChartData.Count);
        Assert.Contains(analytics.ChartData, series =>
            series.Name == "Revenue" &&
            series.Values.SequenceEqual(reportData.Enterprises.Select(e => e.Revenue)));

        Assert.Contains(analytics.GaugeData, kpi =>
            kpi.Name == "Average Revenue" && kpi.Value == 5000m);
        Assert.Contains(analytics.GaugeData, kpi =>
            kpi.Name == "Average ROI" && kpi.Value == 40.92m);

        Assert.NotNull(analytics.StatisticalSummary);
        Assert.NotNull(analytics.FinancialProjection);
        Assert.Equal("compound_growth_projection", analytics.FinancialProjection?.CalculationType);
    }

    private static List<Enterprise> CreateSampleEnterprises()
    {
        var now = DateTime.UtcNow;

        return new List<Enterprise>
        {
            new()
            {
                Id = 1,
                Name = "Aqua Dynamics",
                Type = "Water",
                CitizenCount = 200,
                CurrentRate = 20m,
                MonthlyExpenses = 3000m,
                LastModified = now.AddDays(-5)
            },
            new()
            {
                Id = 2,
                Name = "Bright Sanitation",
                Type = "Sanitation",
                CitizenCount = 100,
                CurrentRate = 50m,
                MonthlyExpenses = 3600m,
                LastModified = now.AddDays(-4)
            },
            new()
            {
                Id = 3,
                Name = "Compost Collective",
                Type = "Recycling",
                CitizenCount = 120,
                CurrentRate = 50m,
                MonthlyExpenses = 4200m,
                LastModified = now.AddDays(-3)
            },
            new()
            {
                Id = 4,
                Name = "Downtown Lighting",
                Type = "Utilities",
                CitizenCount = 125,
                CurrentRate = 40m,
                MonthlyExpenses = 3100m,
                LastModified = now.AddDays(-2)
            },
            new()
            {
                Id = 5,
                Name = "Evergreen Parks",
                Type = "Recreation",
                CitizenCount = 100,
                CurrentRate = 50m,
                MonthlyExpenses = 3900m,
                LastModified = now.AddDays(-1)
            }
        };
    }
}
