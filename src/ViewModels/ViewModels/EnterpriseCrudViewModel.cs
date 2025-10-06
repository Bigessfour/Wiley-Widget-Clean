using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing basic CRUD operations on enterprises
/// Handles adding, editing, saving, and deleting enterprises
/// </summary>
public partial class EnterpriseCrudViewModel : AsyncViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Currently selected enterprise in the UI
    /// </summary>
    private Enterprise? _selectedEnterprise;
    public Enterprise? SelectedEnterprise
    {
        get => _selectedEnterprise;
        set
        {
            if (_selectedEnterprise != value)
            {
                _selectedEnterprise = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedEnterprise));
                OnPropertyChanged(nameof(CanSaveEnterprise));
                OnPropertyChanged(nameof(CanDeleteEnterprise));
            }
        }
    }

    /// <summary>
    /// Whether an enterprise is currently selected
    /// </summary>
    public bool HasSelectedEnterprise => SelectedEnterprise != null;

    /// <summary>
    /// Whether the selected enterprise can be saved (has changes)
    /// </summary>
    public bool CanSaveEnterprise => SelectedEnterprise != null;

    /// <summary>
    /// Whether the selected enterprise can be deleted
    /// </summary>
    public bool CanDeleteEnterprise => SelectedEnterprise != null;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseCrudViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<EnterpriseViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    [RelayCommand]
    public async Task AddEnterpriseAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 0.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 0,
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);
            SelectedEnterprise = addedEnterprise;

            Logger.LogInformation("Added new enterprise: {EnterpriseName}", addedEnterprise.Name);
        }, statusMessage: "Adding new enterprise...");
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>
    [RelayCommand]
    public async Task SaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // MonthlyRevenue is now automatically calculated from CitizenCount * CurrentRate
            await _enterpriseRepository.UpdateAsync(SelectedEnterprise);
            Logger.LogInformation("Saved enterprise: {EnterpriseName}", SelectedEnterprise.Name);
        }, statusMessage: "Saving enterprise changes...");
    }

    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>
    [RelayCommand]
    public async Task DeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var success = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);
            if (success)
            {
                Logger.LogInformation("Deleted enterprise: {EnterpriseName}", SelectedEnterprise.Name);
                SelectedEnterprise = null;
            }
        }, statusMessage: "Deleting enterprise...");
    }

    /// <summary>
    /// Edits the selected enterprise
    /// </summary>
    [RelayCommand]
    public void EditEnterprise()
    {
        if (SelectedEnterprise == null)
        {
            Logger.LogWarning("No enterprise selected for editing");
            return;
        }

        // TODO: Implement edit enterprise dialog/logic
        Logger.LogInformation("Editing enterprise: {EnterpriseName}", SelectedEnterprise.Name);
    }

    /// <summary>
    /// Imports enterprise data from external sources
    /// </summary>
    [RelayCommand]
    public async Task ImportDataAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // TODO: Implement data import functionality
            // This could import from Excel, CSV, or other formats
            Logger.LogInformation("Import data functionality requested");

            // Placeholder for future implementation
            await Task.Delay(1000, cancellationToken);
        }, statusMessage: "Importing data...");
    }
}