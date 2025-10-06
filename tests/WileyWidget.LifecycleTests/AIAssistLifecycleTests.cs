using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.LifecycleTests;

public sealed class AIAssistLifecycleTests : LifecycleTestBase
{
    [Fact]
    public async Task CalculateServiceCharge_UsesRepositoryBackedDataAndCachesAnalytics()
    {
        await RunOnDispatcherAsync(async () =>
        {
            var enterpriseIds = new List<int>();

            await WithDbContextAsync(async context =>
            {
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();

                var water = new Enterprise
                {
                    Name = "AI Waterworks",
                    Type = "Water",
                    CurrentRate = 30.00m,
                    MonthlyExpenses = 22000m,
                    CitizenCount = 1500,
                    TotalBudget = 600000m
                };

                var sanitation = new Enterprise
                {
                    Name = "AI Sanitation",
                    Type = "Sanitation",
                    CurrentRate = 18.50m,
                    MonthlyExpenses = 12500m,
                    CitizenCount = 900,
                    TotalBudget = 280000m
                };

                context.Enterprises.AddRange(water, sanitation);
                await context.SaveChangesAsync();

                enterpriseIds.Add(water.Id);
                enterpriseIds.Add(sanitation.Id);
            });

            var primaryEnterprise = await WithDbContextAsync(async context =>
                await context.Enterprises.AsNoTracking().FirstAsync(e => e.Id == enterpriseIds[0]));

            var aiService = new TestAIService();
            var chargeCalculator = new TestChargeCalculatorService
            {
                Recommendation = new ServiceChargeRecommendation
                {
                    EnterpriseName = primaryEnterprise.Name,
                    CurrentRate = primaryEnterprise.CurrentRate,
                    RecommendedRate = primaryEnterprise.CurrentRate + 2.40m,
                    TotalMonthlyExpenses = primaryEnterprise.MonthlyExpenses,
                    MonthlyRevenueAtRecommended = primaryEnterprise.CitizenCount * (primaryEnterprise.CurrentRate + 2.40m),
                    MonthlySurplus = 1750m,
                    ReserveAllocation = 480m,
                    BreakEvenAnalysis = new BreakEvenAnalysis
                    {
                        BreakEvenRate = primaryEnterprise.MonthlyExpenses / primaryEnterprise.CitizenCount,
                        CurrentSurplusDeficit = (primaryEnterprise.CitizenCount * primaryEnterprise.CurrentRate) - primaryEnterprise.MonthlyExpenses,
                        RequiredRateIncrease = 1.65m,
                        CoverageRatio = 1.12m
                    },
                    CalculationDate = DateTime.UtcNow
                }
            };
            var scenarioEngine = new TestScenarioEngine();
            var repository = new EnterpriseRepository(DbContextFactory);
            var grok = new GrokSupercomputer(aiService, repository, CreateLogger<GrokSupercomputer>());
            var dispatcher = CreateDispatcherHelper();
            var viewModel = new AIAssistViewModel(
                aiService,
                chargeCalculator,
                scenarioEngine,
                grok,
                repository,
                dispatcher,
                CreateLogger<AIAssistViewModel>())
            {
                EnterpriseIdForAnalysis = primaryEnterprise.Id
            };

            viewModel.SetConversationModeCommand.Execute("ServiceCharge");

            await viewModel.CalculateServiceChargeCommand.ExecuteAsync(null);

            Assert.Equal(primaryEnterprise.Id, chargeCalculator.LastRequestedEnterpriseId);
            Assert.NotEmpty(viewModel.EnterpriseAnalyticsCache);
            Assert.Contains(viewModel.EnterpriseAnalyticsCache, e => e.Id == primaryEnterprise.Id);
            Assert.Equal(enterpriseIds.Count, viewModel.EnterpriseAnalyticsCache.Count);

            var grokCall = Assert.Single(aiService.InsightsRequests);
            Assert.Equal("Financial calculation: service_charge_analysis", grokCall.Context);
            Assert.Contains(primaryEnterprise.MonthlyExpenses.ToString("N2"), grokCall.Question);

            Assert.True(viewModel.ChatMessages.Count > 0);
        });
    }

    private sealed class TestAIService : IAIService
    {
        public List<(string Context, string Question)> InsightsRequests { get; } = new();

        public Task<string> AnalyzeDataAsync(string data, string analysisType) => Task.FromResult("analysis");

        public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements) => Task.FromResult("mock");

        public Task<string> GetInsightsAsync(string context, string question)
        {
            InsightsRequests.Add((context, question));
            return Task.FromResult("insights");
        }

        public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState) => Task.FromResult("review");
    }

    private sealed class TestChargeCalculatorService : IChargeCalculatorService
    {
        public int LastRequestedEnterpriseId { get; private set; } = -1;

        public ServiceChargeRecommendation Recommendation { get; set; } = new();

        public Task<ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId)
        {
            LastRequestedEnterpriseId = enterpriseId;
            Recommendation.EnterpriseId = enterpriseId;
            return Task.FromResult(Recommendation);
        }

        public Task<WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0)
        {
            return Task.FromResult(new WhatIfScenario
            {
                ScenarioName = "Test Scenario",
                CurrentRate = Recommendation.CurrentRate,
                ProposedRate = Recommendation.RecommendedRate
            });
        }
    }

    private sealed class TestScenarioEngine : IWhatIfScenarioEngine
    {
        public Task<ComprehensiveScenario> GenerateComprehensiveScenarioAsync(int enterpriseId, ScenarioParameters parameters)
        {
            return Task.FromResult(new ComprehensiveScenario
            {
                ScenarioName = "Stub",
                TotalImpact = new TotalImpact(),
                RiskAssessment = new RiskAssessment { RiskLevel = "Low" }
            });
        }
    }
}
