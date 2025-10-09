#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Centralized fiscal year business logic service
/// Provides GASB-compliant fiscal year calculations and queries
/// </summary>
public class FiscalYearService
{
    private readonly AppDbContext _context;
    private FiscalYearSettings? _cachedSettings;
    private DateTime _cacheExpiration = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public FiscalYearService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get fiscal year settings (with 30-minute cache)
    /// </summary>
    public async Task<FiscalYearSettings> GetSettingsAsync()
    {
        // Check cache validity
        if (_cachedSettings != null && DateTime.UtcNow < _cacheExpiration)
        {
            return _cachedSettings;
        }

        // Load from database (singleton pattern - Id = 1)
        _cachedSettings = await _context.FiscalYearSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == 1);

        // Create default settings if none exist
        if (_cachedSettings == null)
        {
            _cachedSettings = new FiscalYearSettings
            {
                Id = 1,
                FiscalYearStartMonth = 7,  // July
                FiscalYearStartDay = 1,
                LastModified = DateTime.UtcNow
            };

            // Save default settings to database
            _context.FiscalYearSettings.Add(_cachedSettings);
            await _context.SaveChangesAsync();
        }

        // Update cache expiration
        _cacheExpiration = DateTime.UtcNow.Add(CacheDuration);

        return _cachedSettings;
    }

    /// <summary>
    /// Get the current fiscal year start date
    /// </summary>
    public async Task<DateTime> GetCurrentFiscalYearStartAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.GetCurrentFiscalYearStart(DateTime.Now);
    }

    /// <summary>
    /// Get the current fiscal year end date
    /// </summary>
    public async Task<DateTime> GetCurrentFiscalYearEndAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.GetCurrentFiscalYearEnd(DateTime.Now);
    }

    /// <summary>
    /// Get the current fiscal year number (e.g., 2025 for FY2024-2025)
    /// Uses the year when the fiscal year ENDS
    /// </summary>
    public async Task<int> GetCurrentFiscalYearNumberAsync()
    {
        var start = await GetCurrentFiscalYearStartAsync();
        return start.Month >= 7 ? start.Year + 1 : start.Year;
    }

    /// <summary>
    /// Get fiscal year date range for a specific fiscal year number
    /// </summary>
    /// <param name="fiscalYear">Fiscal year number (e.g., 2025 for FY2024-2025)</param>
    /// <returns>Tuple of (Start Date, End Date)</returns>
    public async Task<(DateTime Start, DateTime End)> GetFiscalYearRangeAsync(int fiscalYear)
    {
        var settings = await GetSettingsAsync();
        
        // Calculate start year (fiscal year - 1 if FY starts mid-year)
        var startYear = settings.FiscalYearStartMonth >= 7 ? fiscalYear - 1 : fiscalYear;
        
        var start = new DateTime(startYear, settings.FiscalYearStartMonth, settings.FiscalYearStartDay);
        var end = start.AddYears(1).AddDays(-1);
        
        return (start, end);
    }

    /// <summary>
    /// Get fiscal year date range for a specific fiscal year number (synchronous)
    /// </summary>
    public (DateTime Start, DateTime End) GetFiscalYearRange(int fiscalYear)
    {
        // For synchronous contexts, use cached settings or defaults
        var settings = _cachedSettings ?? new FiscalYearSettings();
        
        var startYear = settings.FiscalYearStartMonth >= 7 ? fiscalYear - 1 : fiscalYear;
        var start = new DateTime(startYear, settings.FiscalYearStartMonth, settings.FiscalYearStartDay);
        var end = start.AddYears(1).AddDays(-1);
        
        return (start, end);
    }

    /// <summary>
    /// Check if a date is in the current fiscal year
    /// </summary>
    public async Task<bool> IsCurrentFiscalYearAsync(DateTime date)
    {
        var settings = await GetSettingsAsync();
        return settings.IsCurrentFiscalYear(date);
    }

    /// <summary>
    /// Get fiscal period classification for a date
    /// </summary>
    public async Task<FiscalPeriod> GetFiscalPeriodAsync(DateTime date)
    {
        var settings = await GetSettingsAsync();
        return settings.GetFiscalPeriod(date);
    }

    /// <summary>
    /// Get display name for fiscal year (e.g., "FY2024-2025")
    /// </summary>
    public async Task<string> GetFiscalYearDisplayNameAsync(int fiscalYear)
    {
        var (start, end) = await GetFiscalYearRangeAsync(fiscalYear);
        return $"FY{start.Year}-{end.Year}";
    }

    /// <summary>
    /// Get display name for current fiscal year
    /// </summary>
    public async Task<string> GetCurrentFiscalYearDisplayNameAsync()
    {
        var fiscalYear = await GetCurrentFiscalYearNumberAsync();
        return await GetFiscalYearDisplayNameAsync(fiscalYear);
    }

    /// <summary>
    /// Update fiscal year settings
    /// </summary>
    public async Task<FiscalYearSettings> UpdateSettingsAsync(int startMonth, int startDay)
    {
        // Validate inputs
        if (startMonth < 1 || startMonth > 12)
            throw new ArgumentException("Month must be between 1 and 12", nameof(startMonth));
        
        if (startDay < 1 || startDay > 31)
            throw new ArgumentException("Day must be between 1 and 31", nameof(startDay));

        var settings = await _context.FiscalYearSettings.FirstOrDefaultAsync(s => s.Id == 1);
        
        if (settings == null)
        {
            settings = new FiscalYearSettings
            {
                Id = 1,
                FiscalYearStartMonth = startMonth,
                FiscalYearStartDay = startDay,
                LastModified = DateTime.UtcNow
            };
            _context.FiscalYearSettings.Add(settings);
        }
        else
        {
            settings.FiscalYearStartMonth = startMonth;
            settings.FiscalYearStartDay = startDay;
            settings.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        _cachedSettings = null;
        _cacheExpiration = DateTime.MinValue;

        return settings;
    }

    /// <summary>
    /// Get list of fiscal year numbers for dropdown (current ± 3 years)
    /// </summary>
    public async Task<int[]> GetAvailableFiscalYearsAsync(int yearsBefore = 3, int yearsAfter = 1)
    {
        var currentFY = await GetCurrentFiscalYearNumberAsync();
        var years = new int[yearsBefore + yearsAfter + 1];
        
        for (int i = 0; i < years.Length; i++)
        {
            years[i] = currentFY - yearsBefore + i;
        }
        
        return years;
    }

    /// <summary>
    /// Validate if a budget period aligns with fiscal year boundaries
    /// </summary>
    public async Task<bool> ValidateBudgetPeriodAsync(DateTime startDate, DateTime endDate)
    {
        var settings = await GetSettingsAsync();
        var expectedStart = settings.GetCurrentFiscalYearStart(startDate);
        var expectedEnd = settings.GetCurrentFiscalYearEnd(startDate);
        
        return startDate == expectedStart && endDate == expectedEnd;
    }

    /// <summary>
    /// Calculate fiscal year number from a date
    /// </summary>
    public async Task<int> GetFiscalYearFromDateAsync(DateTime date)
    {
        var settings = await GetSettingsAsync();
        var fyStart = settings.GetCurrentFiscalYearStart(date);
        return fyStart.Month >= 7 ? fyStart.Year + 1 : fyStart.Year;
    }

    /// <summary>
    /// Get months remaining in current fiscal year
    /// </summary>
    public async Task<int> GetMonthsRemainingInFiscalYearAsync()
    {
        var fiscalEnd = await GetCurrentFiscalYearEndAsync();
        var now = DateTime.Now;
        
        if (now > fiscalEnd)
            return 0;
        
        var months = ((fiscalEnd.Year - now.Year) * 12) + fiscalEnd.Month - now.Month;
        return Math.Max(0, months);
    }

    /// <summary>
    /// Get days remaining in current fiscal year
    /// </summary>
    public async Task<int> GetDaysRemainingInFiscalYearAsync()
    {
        var fiscalEnd = await GetCurrentFiscalYearEndAsync();
        var now = DateTime.Now;
        
        if (now > fiscalEnd)
            return 0;
        
        return Math.Max(0, (int)(fiscalEnd - now).TotalDays);
    }

    /// <summary>
    /// Clear the settings cache (use after updating settings)
    /// </summary>
    public void ClearCache()
    {
        _cachedSettings = null;
        _cacheExpiration = DateTime.MinValue;
    }
}
