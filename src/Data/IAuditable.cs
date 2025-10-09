#nullable enable
using System;

namespace WileyWidget.Data;

/// <summary>
/// Interface for entities that track audit information (created/modified timestamps)
/// Implements compliance requirements for municipal data tracking
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Date and time when the entity was created (UTC)
    /// </summary>
    DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date and time when the entity was last modified (UTC)
    /// </summary>
    DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// User who created the entity
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// User who last modified the entity
    /// </summary>
    string? ModifiedBy { get; set; }
}
