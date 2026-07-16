using SmartApp.Domain.Sequences;

namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Generates sequential, per-tenant document numbers (e.g. "PINV-000123") from the
/// <c>DocumentSequences</c> table. The increment is staged inside the caller's transaction so a
/// document and its number commit atomically. See SmartApp-Architecture/05-Database-Design.md §10 and
/// 13-Development-Rules.md §6.4.
/// </summary>
public interface IDocumentNumberService
{
    /// <summary>
    /// Reserves and returns the next formatted number for the given document type in the current
    /// tenant, creating the sequence row on first use. Does not call SaveChanges — the caller persists
    /// it together with the document.
    /// </summary>
    Task<string> NextAsync(DocumentType documentType, CancellationToken cancellationToken);
}
