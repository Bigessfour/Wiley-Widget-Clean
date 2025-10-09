#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Service for importing budget data from various file formats
/// </summary>
public class BudgetImportService
{
    private readonly IEnumerable<IBudgetImporter> _importers;
    private readonly GASBValidator _gasbValidator;

    /// <summary>
    /// Initializes a new instance of the BudgetImportService class
    /// </summary>
    /// <param name="importers">The collection of budget importers</param>
    /// <param name="gasbValidator">The GASB validator</param>
    public BudgetImportService(IEnumerable<IBudgetImporter> importers, GASBValidator gasbValidator)
    {
        _importers = importers ?? throw new ArgumentNullException(nameof(importers));
        _gasbValidator = gasbValidator ?? throw new ArgumentNullException(nameof(gasbValidator));
    }

    /// <summary>
    /// Imports budget data from a file using the appropriate importer
    /// </summary>
    /// <param name="filePath">The path to the file to import</param>
    /// <returns>A collection of imported and validated budget entries</returns>
    public async Task<IEnumerable<BudgetEntry>> ImportBudgetAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var importer = GetImporterForFile(filePath);
        if (importer == null)
            throw new InvalidOperationException($"No importer found for file: {filePath}");

        // Validate the file first
        var validationErrors = await importer.ValidateImportFileAsync(filePath);
        if (validationErrors.Any())
            throw new InvalidOperationException($"File validation failed: {string.Join(", ", validationErrors)}");

        // Import the data
        var entries = await importer.ImportBudgetAsync(filePath);

        // Validate GASB compliance
        var gasbErrors = _gasbValidator.ValidateGASBCompliance(entries);
        if (gasbErrors.Any())
            throw new InvalidOperationException($"GASB compliance validation failed: {string.Join(", ", gasbErrors)}");

        return entries;
    }

    /// <summary>
    /// Validates a budget import file
    /// </summary>
    /// <param name="filePath">The path to the file to validate</param>
    /// <returns>A list of validation errors, empty if valid</returns>
    public async Task<List<string>> ValidateImportFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var errors = new List<string>();

        var importer = GetImporterForFile(filePath);
        if (importer == null)
        {
            errors.Add($"No importer found for file extension: {System.IO.Path.GetExtension(filePath)}");
            return errors;
        }

        // File format validation
        var fileErrors = await importer.ValidateImportFileAsync(filePath);
        errors.AddRange(fileErrors);

        // If file format is valid, try to import and validate GASB compliance
        if (!fileErrors.Any())
        {
            try
            {
                var entries = await importer.ImportBudgetAsync(filePath);
                var gasbErrors = _gasbValidator.ValidateGASBCompliance(entries);
                errors.AddRange(gasbErrors);
            }
            catch (Exception ex)
            {
                errors.Add($"Error during import validation: {ex.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Gets all supported file extensions
    /// </summary>
    public IEnumerable<string> GetSupportedExtensions()
    {
        return _importers.SelectMany(i => i.SupportedExtensions).Distinct();
    }

    /// <summary>
    /// Gets the importer for a specific file
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>The appropriate importer, or null if none found</returns>
    private IBudgetImporter? GetImporterForFile(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return _importers.FirstOrDefault(i => i.SupportedExtensions.Contains(extension));
    }
}