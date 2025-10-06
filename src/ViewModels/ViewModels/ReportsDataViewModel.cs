using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WileyWidget.Data;
using WileyWidget.Models;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Collections.Generic;
using WileyWidget.Services;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for reports data management
/// Handles loading enterprise references and managing report data collections
/// </summary>
public partial class ReportsDataViewModel : ValidatableViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Collection of report items for display
    /// </summary>
    public ObservableCollection<ReportsViewModel.ReportItem> ReportItems { get; } = new();

    /// <summary>
    /// Collection of enterprise references for filtering
    /// </summary>
    public ObservableCollection<ReportsViewModel.EnterpriseReference> Enterprises { get; } = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public ReportsDataViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<ReportsDataViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Load enterprise references for reporting filters
    /// </summary>
    public async Task LoadEnterpriseReferencesAsync()
    {
        try
        {
            var enterprises = await _enterpriseRepository.GetAllAsync().ConfigureAwait(false);
            await DispatcherHelper.InvokeAsync(() =>
            {
                Enterprises.Clear();
                foreach (var enterprise in enterprises.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    Enterprises.Add(new ReportsViewModel.EnterpriseReference(enterprise.Id, enterprise.Name));
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load enterprise references for reporting filters");
        }
    }

    /// <summary>
    /// Update report items from report data
    /// </summary>
    public async Task UpdateReportItemsAsync(ReportDataModel reportData)
    {
        await DispatcherHelper.InvokeAsync(() =>
        {
            ReportItems.Clear();
            foreach (var metric in reportData.Enterprises)
            {
                ReportItems.Add(new ReportsViewModel.ReportItem(
                    metric.Id,
                    metric.Name,
                    metric.Revenue,
                    metric.Expenses,
                    metric.RoiPercentage,
                    metric.ProfitMarginPercentage,
                    metric.LastModified));
            }
        });
    }

    /// <summary>
    /// Clear all report data
    /// </summary>
    public void ClearReportData()
    {
        ReportItems.Clear();
    }
}