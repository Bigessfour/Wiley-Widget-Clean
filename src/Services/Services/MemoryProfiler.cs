using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Memory profiler implementation using .NET diagnostics
/// </summary>
public class MemoryProfiler : IMemoryProfiler
{
    private readonly ILogger<MemoryProfiler> _logger;

    public MemoryProfiler(ILogger<MemoryProfiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current memory usage in MB
    /// </summary>
    public double GetCurrentMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();

            // Use WorkingSet64 for current memory usage (most relevant for WPF apps)
            var memoryBytes = process.WorkingSet64;
            var memoryMB = memoryBytes / (1024.0 * 1024.0);

            return Math.Round(memoryMB, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current memory usage");
            return 0.0;
        }
    }

    /// <summary>
    /// Gets detailed memory statistics
    /// </summary>
    public MemoryStats GetMemoryStats()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var gcMemory = GC.GetTotalMemory(false);

            return new MemoryStats
            {
                TotalMemoryMB = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                WorkingSetMB = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                PrivateMemoryMB = Math.Round(process.PrivateMemorySize64 / (1024.0 * 1024.0), 2),
                VirtualMemoryMB = Math.Round(process.VirtualMemorySize64 / (1024.0 * 1024.0), 2),
                GCMemoryMB = Math.Round(gcMemory / (1024.0 * 1024.0), 2),
                GCCollectionsGen0 = GC.CollectionCount(0),
                GCCollectionsGen1 = GC.CollectionCount(1),
                GCCollectionsGen2 = GC.CollectionCount(2)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed memory statistics");
            return new MemoryStats();
        }
    }
}