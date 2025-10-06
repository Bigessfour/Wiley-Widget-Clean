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

public sealed class UtilityCustomerLifecycleTests : LifecycleTestBase
{
    [Fact]
    public async Task UtilityCustomerCrudLifecycle_PersistsThroughRepositoryAndViewModel()
    {
        await RunOnDispatcherAsync(async () =>
        {
            var repository = new UtilityCustomerRepository(DbContextFactory);
            var viewModel = new UtilityCustomerViewModel(repository, CreateDispatcherHelper(), CreateLogger<UtilityCustomerViewModel>());

            await viewModel.LoadCustomersCommand.ExecuteAsync(null);
            Assert.Empty(viewModel.Customers);

            await viewModel.AddCustomerCommand.ExecuteAsync(null);
            Assert.NotNull(viewModel.SelectedCustomer);

            viewModel.SelectedCustomer!.FirstName = "Jamie";
            viewModel.SelectedCustomer.LastName = "Rivera";
            viewModel.SelectedCustomer.ServiceAddress = "100 Main St";
            viewModel.SelectedCustomer.ServiceCity = "Wiley";
            viewModel.SelectedCustomer.ServiceState = "CO";
            viewModel.SelectedCustomer.ServiceZipCode = "81092";
            viewModel.SelectedCustomer.PhoneNumber = "555-1234";
            viewModel.SelectedCustomer.EmailAddress = "jamie.rivera@example.com";
            viewModel.SelectedCustomer.CustomerType = CustomerType.Residential;
            viewModel.SelectedCustomer.ServiceLocation = ServiceLocation.InsideCityLimits;
            viewModel.SelectedCustomer.Status = CustomerStatus.Active;
            viewModel.SelectedCustomer.CurrentBalance = 125.75m;

            await viewModel.SaveCustomerCommand.ExecuteAsync(null);

            var savedCustomer = await WithDbContextAsync(async context =>
                await context.UtilityCustomers.AsNoTracking().SingleAsync());

            Assert.Equal("Jamie", savedCustomer.FirstName);
            Assert.Equal("Rivera", savedCustomer.LastName);
            Assert.Equal("100 Main St", savedCustomer.ServiceAddress);
            Assert.Equal("Wiley", savedCustomer.ServiceCity);
            Assert.Equal(125.75m, savedCustomer.CurrentBalance);

            viewModel.SelectedCustomer.CurrentBalance = 210.00m;
            viewModel.SelectedCustomer.Notes = "Adjusted after audit";
            await viewModel.SaveCustomerCommand.ExecuteAsync(null);

            var updatedCustomer = await WithDbContextAsync(async context =>
                await context.UtilityCustomers.AsNoTracking().SingleAsync());
            Assert.Equal(210.00m, updatedCustomer.CurrentBalance);
            Assert.Equal("Adjusted after audit", updatedCustomer.Notes);

            await viewModel.DeleteCustomerCommand.ExecuteAsync(null);
            Assert.Empty(viewModel.Customers);

            var remaining = await WithDbContextAsync(context =>
                context.UtilityCustomers.AsNoTracking().CountAsync());
            Assert.Equal(0, remaining);
        });
    }

    [Fact]
    public async Task UtilityCustomerView_DisplaysFormattedGridWithInteractiveColumns()
    {
        await RunOnDispatcherAsync(async () =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            await WithDbContextAsync(async context =>
            {
                context.UtilityCustomers.RemoveRange(context.UtilityCustomers);
                await context.SaveChangesAsync();

                context.UtilityCustomers.AddRange(
                    new UtilityCustomer
                    {
                        AccountNumber = "ACC-1001",
                        FirstName = "Jordan",
                        LastName = "Parker",
                        ServiceAddress = "123 River Rd",
                        ServiceCity = "Wiley",
                        ServiceState = "CO",
                        ServiceZipCode = "81092",
                        PhoneNumber = "555-0101",
                        EmailAddress = "jordan.parker@example.com",
                        CustomerType = CustomerType.Commercial,
                        ServiceLocation = ServiceLocation.InsideCityLimits,
                        Status = CustomerStatus.Active,
                        CurrentBalance = 512.34m,
                        AccountOpenDate = DateTime.UtcNow.AddYears(-2)
                    },
                    new UtilityCustomer
                    {
                        AccountNumber = "ACC-1002",
                        FirstName = "Avery",
                        LastName = "Cole",
                        ServiceAddress = "200 Lake St",
                        ServiceCity = "Wiley",
                        ServiceState = "CO",
                        ServiceZipCode = "81092",
                        PhoneNumber = "555-0102",
                        EmailAddress = "avery.cole@example.com",
                        CustomerType = CustomerType.Residential,
                        ServiceLocation = ServiceLocation.OutsideCityLimits,
                        Status = CustomerStatus.Active,
                        CurrentBalance = 78.56m,
                        AccountOpenDate = DateTime.UtcNow.AddMonths(-6)
                    });

                await context.SaveChangesAsync();
            });

            var repository = new UtilityCustomerRepository(DbContextFactory);
            var viewModel = new UtilityCustomerViewModel(repository, CreateDispatcherHelper(), CreateLogger<UtilityCustomerViewModel>());

            await viewModel.LoadCustomersAsync();

            var view = new UtilityCustomerView
            {
                DataContext = viewModel
            };

            view.Width = 1400;
            view.Height = 800;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.Left = -32000;
            view.Top = -32000;
            view.ApplyTemplate();
            view.UpdateLayout();

            var dataGrid = view.FindName("CustomerGrid") as SfDataGrid;
            Assert.NotNull(dataGrid);

            dataGrid!.UpdateLayout();

            Assert.True(dataGrid.AllowSorting);
            Assert.True(dataGrid.AllowFiltering);
            Assert.True(dataGrid.AllowGrouping);

            Assert.Contains(dataGrid.Columns, column => column is GridTextColumn { MappingName: "AccountNumber" });
            Assert.Contains(dataGrid.Columns, column => column is GridTextColumn { MappingName: "DisplayName" });
            Assert.Contains(dataGrid.Columns, column => column is GridNumericColumn { MappingName: "CurrentBalance" });

            var items = dataGrid.ItemsSource as IEnumerable;
            Assert.NotNull(items);
            Assert.True(items!.Cast<object>().Any(), "CustomerGrid should show customer rows when data exists.");

            if (view.IsLoaded)
            {
                view.Close();
            }
        });
    }
}
