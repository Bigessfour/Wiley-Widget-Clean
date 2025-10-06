using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;
using Xunit;
using Syncfusion.UI.Xaml.TreeGrid;

namespace WileyWidget.LifecycleTests;

public sealed class EnterpriseLifecycleTests : LifecycleTestBase
{
    [Fact]
    public async Task EnterpriseCrudLifecycle_PersistsThroughRepositoryAndViewModel()
    {
        await RunOnDispatcherAsync(async () =>
        {
            var repository = new EnterpriseRepository(DbContextFactory);
            var viewModel = new EnterpriseViewModel(repository, CreateDispatcherHelper(), CreateLogger<EnterpriseViewModel>());

            // Ensure a clean starting point since the production model seeds baseline data.
            await WithDbContextAsync(async context =>
            {
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();
            });

            // Initial load should reflect empty database.
            await viewModel.LoadEnterprisesCommand.ExecuteAsync(null);
            Assert.Empty(viewModel.Enterprises);

            // Create via command (simulates "Add" button in the view).
            await viewModel.AddEnterpriseCommand.ExecuteAsync(null);
            Assert.NotNull(viewModel.SelectedEnterprise);

            viewModel.SelectedEnterprise!.Name = "Integration Water";
            viewModel.SelectedEnterprise.CurrentRate = 27.50m;
            viewModel.SelectedEnterprise.MonthlyExpenses = 12500m;
            viewModel.SelectedEnterprise.CitizenCount = 1400;
            viewModel.SelectedEnterprise.Notes = "Created by lifecycle test";
            viewModel.SelectedEnterprise.Type = "Water";

            await viewModel.SaveEnterpriseCommand.ExecuteAsync(null);

            // Validate persistence to the database.
            var savedEntity = await WithDbContextAsync(async context =>
                await context.Enterprises.AsNoTracking().SingleAsync());

            Assert.Equal("Integration Water", savedEntity.Name);
            Assert.Equal(27.50m, savedEntity.CurrentRate);
            Assert.Equal(12500m, savedEntity.MonthlyExpenses);
            Assert.Equal(1400, savedEntity.CitizenCount);
            Assert.Equal("Water", savedEntity.Type);

            // Update lifecycle through the view model and confirm storage.
            viewModel.SelectedEnterprise.MonthlyExpenses = 13100m;
            viewModel.SelectedEnterprise.Notes = "Updated lifecycle";
            await viewModel.SaveEnterpriseCommand.ExecuteAsync(null);

            var updated = await WithDbContextAsync(async context =>
                await context.Enterprises.AsNoTracking().SingleAsync());
            Assert.Equal(13100m, updated.MonthlyExpenses);
            Assert.Equal("Updated lifecycle", updated.Notes);

            // Delete through the view model and confirm cascade to database.
            await viewModel.DeleteEnterpriseCommand.ExecuteAsync(null);
            Assert.Empty(viewModel.Enterprises);

            var remainingCount = await WithDbContextAsync(context =>
                context.Enterprises.AsNoTracking().CountAsync());
            Assert.Equal(0, remainingCount);
        });
    }

    [Fact]
    public async Task EnterpriseView_DisplayConfigurationReflectsRepositoryData()
    {
        await RunOnDispatcherAsync(async () =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            await WithDbContextAsync(async context =>
            {
                context.Enterprises.RemoveRange(context.Enterprises);
                await context.SaveChangesAsync();

                context.Enterprises.AddRange(
                    new Enterprise
                    {
                        Name = "Lifecycle Water",
                        Type = "Water",
                        Status = EnterpriseStatus.Active,
                        CurrentRate = 32.75m,
                        MonthlyExpenses = 21500m,
                        CitizenCount = 1600,
                        Notes = "Lifecycle display test",
                        TotalBudget = 450000m
                    },
                    new Enterprise
                    {
                        Name = "Lifecycle Sewer",
                        Type = "Sewer",
                        Status = EnterpriseStatus.Active,
                        CurrentRate = 29.15m,
                        MonthlyExpenses = 19800m,
                        CitizenCount = 1320,
                        Notes = "Lifecycle display test",
                        TotalBudget = 390000m
                    });

                await context.SaveChangesAsync();
            });

            var repository = new EnterpriseRepository(DbContextFactory);
            var viewModel = new EnterpriseViewModel(repository, CreateDispatcherHelper(), CreateLogger<EnterpriseViewModel>());

            await viewModel.LoadEnterprisesAsync();

            var totalNodes = Math.Max(1, viewModel.HierarchicalEnterprises.Sum(n => 1 + n.Children.Count));
            viewModel.PageSize = Math.Max(1, totalNodes);

            var view = new EnterpriseView
            {
                DataContext = viewModel
            };

            view.Width = 1200;
            view.Height = 700;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.Left = -32000;
            view.Top = -32000;
            view.ApplyTemplate();
            view.UpdateLayout();

            var treeGrid = view.FindName("EnterpriseTreeGrid") as SfTreeGrid;
            Assert.NotNull(treeGrid);

            treeGrid!.UpdateLayout();

            Assert.True(treeGrid.AllowSorting);
            Assert.True(treeGrid.AllowEditing);
            Assert.True(treeGrid.AllowResizingColumns);

            Assert.Contains(treeGrid.Columns, column => column is TreeGridCurrencyColumn { MappingName: "Enterprise.CurrentRate" });
            Assert.Contains(treeGrid.Columns, column => column is TreeGridCurrencyColumn { MappingName: "Enterprise.MonthlyRevenue" });
            Assert.Contains(treeGrid.Columns, column => column is TreeGridCurrencyColumn { MappingName: "Enterprise.MonthlyExpenses" });
            Assert.Contains(treeGrid.Columns, column => column is TreeGridCurrencyColumn { MappingName: "Enterprise.MonthlyBalance" });
            Assert.Contains(treeGrid.Columns, column => column is TreeGridDateTimeColumn { MappingName: "Enterprise.LastUpdated" });

            var items = treeGrid.ItemsSource as IEnumerable;
            Assert.NotNull(items);
            Assert.True(items!.Cast<object>().Any(), "EnterpriseTreeGrid should show at least one record when data exists.");

            var revenueColumn = treeGrid.Columns.OfType<TreeGridCurrencyColumn>().First(c => c.MappingName == "Enterprise.MonthlyRevenue");
            Assert.Equal("Enterprise.MonthlyRevenue", revenueColumn.MappingName);

            if (view.IsLoaded)
            {
                view.Close();
            }
        });
    }
}
