using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;
using Xunit;
using Syncfusion.UI.Xaml.Grid;

namespace WileyWidget.LifecycleTests;

public sealed class BudgetViewLifecycleTests : LifecycleTestBase
{
    [Fact]
    public async Task RefreshBudgetData_UsesDatabaseValuesWhenPresent()
    {
        await RunOnDispatcherAsync(async () =>
        {
            await WithDbContextAsync(async context =>
            {
                context.BudgetInteractions.RemoveRange(context.BudgetInteractions);
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();
            });

            await WithDbContextAsync(async context =>
            {
                context.Enterprises.AddRange(
                    new Enterprise
                    {
                        Name = "Test Water",
                        Type = "Water",
                        CurrentRate = 30m,
                        MonthlyExpenses = 21000m,
                        CitizenCount = 1500,
                        TotalBudget = 500000m
                    },
                    new Enterprise
                    {
                        Name = "Test Sewer",
                        Type = "Sewer",
                        CurrentRate = 28m,
                        MonthlyExpenses = 18000m,
                        CitizenCount = 1200,
                        TotalBudget = 420000m
                    });
                await context.SaveChangesAsync();
            });

            var repository = new EnterpriseRepository(DbContextFactory);
            var viewModel = new BudgetViewModel(repository, CreateDispatcherHelper(), CreateLogger<BudgetViewModel>());

            await viewModel.RefreshBudgetDataAsync();

            Assert.Equal(2, viewModel.BudgetDetails.Count);
            Assert.Equal(2, viewModel.BudgetDetails.Count(item => item.Status == "Surplus" || item.Status == "Deficit"));

            var expectedRevenue = viewModel.BudgetDetails.Sum(item => item.MonthlyRevenue);
            Assert.Equal(expectedRevenue, viewModel.TotalRevenue);
            Assert.True(viewModel.TotalRevenue > 0);
            Assert.True(viewModel.TotalExpenses > 0);
        });
    }

    [Fact]
    public async Task RefreshBudgetData_FallsBackToSampleWhenDatabaseEmpty()
    {
        await RunOnDispatcherAsync(async () =>
        {
            await WithDbContextAsync(async context =>
            {
                context.BudgetInteractions.RemoveRange(context.BudgetInteractions);
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();
            });

            var repository = new EnterpriseRepository(DbContextFactory);
            var viewModel = new BudgetViewModel(repository, CreateDispatcherHelper(), CreateLogger<BudgetViewModel>());

            await viewModel.RefreshBudgetDataAsync();

            Assert.NotEmpty(viewModel.BudgetDetails);
            Assert.True(viewModel.TotalRevenue > 0);
            Assert.True(viewModel.TotalExpenses > 0);
            Assert.Equal(viewModel.BudgetDetails.Sum(item => item.MonthlyRevenue), viewModel.TotalRevenue);
        });
    }

    [Fact]
    public async Task BudgetView_DataGridConfigurationAlignsWithBudgetViewModelTotals()
    {
        await RunOnDispatcherAsync(async () =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            await WithDbContextAsync(async context =>
            {
                context.BudgetInteractions.RemoveRange(context.BudgetInteractions);
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();

                context.Enterprises.AddRange(
                    new Enterprise
                    {
                        Name = "Grid Water",
                        Type = "Water",
                        CurrentRate = 28.50m,
                        MonthlyExpenses = 19500m,
                        CitizenCount = 1450,
                        TotalBudget = 520000m
                    },
                    new Enterprise
                    {
                        Name = "Grid Sewer",
                        Type = "Sewer",
                        CurrentRate = 27.10m,
                        MonthlyExpenses = 18200m,
                        CitizenCount = 1300,
                        TotalBudget = 470000m
                    },
                    new Enterprise
                    {
                        Name = "Grid Sanitation",
                        Type = "Sanitation",
                        CurrentRate = 18.75m,
                        MonthlyExpenses = 12500m,
                        CitizenCount = 950,
                        TotalBudget = 310000m
                    });

                await context.SaveChangesAsync();
            });

            var repository = new EnterpriseRepository(DbContextFactory);
            var viewModel = new BudgetViewModel(repository, CreateDispatcherHelper(), CreateLogger<BudgetViewModel>());

            await viewModel.RefreshBudgetDataAsync();

            var view = new BudgetView(viewModel: viewModel)
            {
                Width = 1000,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000
            };

            view.ApplyTemplate();
            view.UpdateLayout();

            var dataGrid = view.FindName("BudgetDetailsGrid") as SfDataGrid;
            Assert.NotNull(dataGrid);

            dataGrid!.UpdateLayout();

            Assert.True(dataGrid.AllowGrouping);
            Assert.True(dataGrid.AllowSorting);
            Assert.True(dataGrid.AllowFiltering);
            Assert.True(dataGrid.ShowGroupDropArea);

            Assert.Contains(dataGrid.Columns, column => column is GridNumericColumn { MappingName: nameof(BudgetDetailItem.MonthlyRevenue) });
            Assert.Contains(dataGrid.Columns, column => column is GridNumericColumn { MappingName: nameof(BudgetDetailItem.MonthlyExpenses) });
            Assert.Contains(dataGrid.Columns, column => column is GridNumericColumn { MappingName: nameof(BudgetDetailItem.MonthlyBalance) });
            Assert.Contains(dataGrid.Columns, column => column is GridNumericColumn { MappingName: nameof(BudgetDetailItem.BreakEvenRate) });

            var items = dataGrid.ItemsSource as IEnumerable;
            Assert.NotNull(items);
            Assert.True(items!.Cast<object>().Any(), "BudgetDetailsGrid should show detail rows after refresh.");

            Assert.Equal(viewModel.BudgetDetails.Sum(item => item.MonthlyRevenue), viewModel.TotalRevenue);
            Assert.Equal(viewModel.BudgetDetails.Sum(item => item.MonthlyExpenses), viewModel.TotalExpenses);

            if (view.IsLoaded)
            {
                view.Close();
            }
        });
    }
}
