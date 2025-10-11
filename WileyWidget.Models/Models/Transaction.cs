#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents a financial transaction against a municipal account
/// </summary>
public class Transaction
{
    /// <summary>
    /// Unique identifier for the transaction
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The municipal account this transaction belongs to
    /// </summary>
    [Required]
    public int MunicipalAccountId { get; set; }
    public MunicipalAccount? MunicipalAccount { get; set; }

    /// <summary>
    /// Transaction amount
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction date
    /// </summary>
    [Required]
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transaction type
    /// </summary>
    [Required]
    public TransactionType Type { get; set; } = TransactionType.Debit;
}

/// <summary>
/// Transaction types
/// </summary>
public enum TransactionType
{
    Debit,
    Credit
}