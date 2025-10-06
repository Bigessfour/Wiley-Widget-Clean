using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WileyWidget.Data;
using WileyWidget.Models;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Collections.Generic;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for municipal account hierarchical relationships
/// Handles building and navigating account hierarchies
/// </summary>
public partial class MunicipalAccountHierarchyViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;

    /// <summary>
    /// Collection of root-level accounts (hierarchical structure)
    /// </summary>
    public ObservableCollection<MunicipalAccount> RootAccounts { get; } = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public MunicipalAccountHierarchyViewModel(
        IMunicipalAccountRepository accountRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<MunicipalAccountHierarchyViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    /// <summary>
    /// Build hierarchical structure from flat account list
    /// </summary>
    public async Task BuildHierarchyAsync(IEnumerable<MunicipalAccount> allAccounts)
    {
        await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
        {
            RootAccounts.Clear();
            var rootAccounts = allAccounts
                .Where(a => a.ParentAccountId == null)
                .OrderBy(a => a.AccountNumber.Value);

            foreach (var account in rootAccounts)
            {
                RootAccounts.Add(account);
            }
        });
    }

    /// <summary>
    /// Gets child accounts for a given parent account
    /// </summary>
    public IEnumerable<MunicipalAccount> GetChildAccounts(MunicipalAccount parentAccount, IEnumerable<MunicipalAccount> allAccounts)
    {
        return allAccounts
            .Where(a => a.ParentAccountId == parentAccount.Id)
            .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Gets accounts for a specific department
    /// </summary>
    public IEnumerable<MunicipalAccount> GetAccountsForDepartment(Department department, IEnumerable<MunicipalAccount> allAccounts)
    {
        return allAccounts
            .Where(a => a.DepartmentId == department.Id)
            .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Gets root accounts for a specific department
    /// </summary>
    public IEnumerable<MunicipalAccount> GetRootAccountsForDepartment(Department department)
    {
        return RootAccounts
            .Where(a => a.DepartmentId == department.Id)
            .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Find account path from root to specified account
    /// </summary>
    public List<MunicipalAccount> GetAccountPath(MunicipalAccount targetAccount, IEnumerable<MunicipalAccount> allAccounts)
    {
        var path = new List<MunicipalAccount>();
        var current = targetAccount;

        while (current != null)
        {
            path.Insert(0, current);
            current = allAccounts.FirstOrDefault(a => a.Id == current.ParentAccountId);
        }

        return path;
    }
}