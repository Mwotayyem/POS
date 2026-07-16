using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Sequences;

namespace SmartApp.Application.Common.Services;

/// <summary>
/// Default <see cref="IDocumentNumberService"/>. Loads (or creates) the tenant's sequence row for the
/// document type, increments it, and formats <c>{Prefix}{number:000000}</c>. The row's concurrency
/// stamp plus the enclosing transaction guard against duplicate numbers under concurrent posting.
/// Staged only — the caller owns SaveChanges.
/// </summary>
public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public DocumentNumberService(IApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<string> NextAsync(DocumentType documentType, CancellationToken cancellationToken)
    {
        DocumentSequence? sequence = await _db.DocumentSequences
            .FirstOrDefaultAsync(s => s.DocumentType == documentType, cancellationToken);

        if (sequence is null)
        {
            sequence = new DocumentSequence
            {
                TenantId = _tenant.CurrentTenantId,
                DocumentType = documentType,
                Prefix = DefaultPrefix(documentType),
                LastNumber = 0,
            };
            _db.DocumentSequences.Add(sequence);
        }

        sequence.LastNumber += 1;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{sequence.Prefix}{sequence.LastNumber:000000}");
    }

    private static string DefaultPrefix(DocumentType documentType) => documentType switch
    {
        DocumentType.PurchaseOrder => "PO-",
        DocumentType.PurchaseInvoice => "PINV-",
        DocumentType.PurchaseReturn => "PRET-",
        DocumentType.SalesInvoice => "SINV-",
        DocumentType.SalesReturn => "SRET-",
        DocumentType.StockAdjustment => "ADJ-",
        DocumentType.Payment => "PAY-",
        _ => "DOC-",
    };
}
