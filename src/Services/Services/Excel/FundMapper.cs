using System;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Static class for mapping worksheet names to fund types.
/// </summary>
public static class FundMapper
{
    /// <summary>
    /// Maps a worksheet name to the appropriate fund type.
    /// </summary>
    /// <param name="worksheetName">Name of the worksheet.</param>
    /// <returns>The corresponding FundType.</returns>
    public static FundType MapWorksheetName(string worksheetName)
    {
        if (string.IsNullOrWhiteSpace(worksheetName))
            return FundType.General;

        var name = worksheetName.ToUpperInvariant().Trim();

        // Town of Wiley specific mappings
        if (name.Contains("GF SUMM") || name.Contains("GENERAL FUND SUMMARY"))
            return FundType.General;

        if (name.Contains("WATER") && name.Contains("ADM"))
            return FundType.Enterprise;

        if (name.Contains("ENTERPRISE"))
            return FundType.Enterprise;

        if (name.Contains("CON SUMM") || name.Contains("CONSERVATION"))
            return FundType.ConservationTrust;

        if (name.Contains("REC"))
            return FundType.Recreation;

        // Wiley Sanitation District specific mappings
        if (name.Contains("WSD") || name.Contains("SANITATION"))
            return FundType.Utility;

        // Generic mappings for other municipalities
        if (name.Contains("GENERAL") || name.Contains("GEN"))
            return FundType.General;

        if (name.Contains("SPECIAL") || name.Contains("REVENUE"))
            return FundType.SpecialRevenue;

        if (name.Contains("CAPITAL") || name.Contains("PROJECTS"))
            return FundType.CapitalProjects;

        if (name.Contains("DEBT") || name.Contains("SERVICE"))
            return FundType.DebtService;

        if (name.Contains("ENTERPRISE"))
            return FundType.Enterprise;

        if (name.Contains("INTERNAL") || name.Contains("SERVICE"))
            return FundType.InternalService;

        if (name.Contains("TRUST"))
            return FundType.Trust;

        if (name.Contains("AGENCY"))
            return FundType.Agency;

        if (name.Contains("CONSERVATION"))
            return FundType.ConservationTrust;

        if (name.Contains("RECREATION"))
            return FundType.Recreation;

        if (name.Contains("UTILITY"))
            return FundType.Utility;

        // Default to General fund for unrecognized worksheets
        return FundType.General;
    }

    /// <summary>
    /// Gets the fund class for a given fund type.
    /// </summary>
    /// <param name="fundType">The fund type.</param>
    /// <returns>The corresponding FundClass.</returns>
    public static FundClass GetFundClass(FundType fundType)
    {
        return fundType switch
        {
            FundType.General or FundType.SpecialRevenue or FundType.CapitalProjects or FundType.DebtService => FundClass.Governmental,
            FundType.Enterprise or FundType.InternalService or FundType.Utility => FundClass.Proprietary,
            FundType.Trust or FundType.ConservationTrust or FundType.Agency => FundClass.Fiduciary,
            FundType.Recreation => FundClass.Proprietary, // Typically proprietary
            _ => FundClass.Governmental
        };
    }

    /// <summary>
    /// Determines if a fund type is governmental.
    /// </summary>
    /// <param name="fundType">The fund type to check.</param>
    /// <returns>True if the fund is governmental, false otherwise.</returns>
    public static bool IsGovernmentalFund(FundType fundType)
    {
        return GetFundClass(fundType) == FundClass.Governmental;
    }

    /// <summary>
    /// Determines if a fund type is proprietary.
    /// </summary>
    /// <param name="fundType">The fund type to check.</param>
    /// <returns>True if the fund is proprietary, false otherwise.</returns>
    public static bool IsProprietaryFund(FundType fundType)
    {
        return GetFundClass(fundType) == FundClass.Proprietary;
    }

    /// <summary>
    /// Determines if a fund type is fiduciary.
    /// </summary>
    /// <param name="fundType">The fund type to check.</param>
    /// <returns>True if the fund is fiduciary, false otherwise.</returns>
    public static bool IsFiduciaryFund(FundType fundType)
    {
        return GetFundClass(fundType) == FundClass.Fiduciary;
    }
}