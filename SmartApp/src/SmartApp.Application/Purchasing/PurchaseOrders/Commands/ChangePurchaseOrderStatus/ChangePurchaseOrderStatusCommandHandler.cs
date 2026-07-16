using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Domain.Purchasing;
using SmartApp.Domain.Purchasing.Enums;
using SmartApp.Shared.Results;

namespace SmartApp.Application.Purchasing.PurchaseOrders.Commands.ChangePurchaseOrderStatus;

/// <summary>
/// Applies a purchase-order status transition with validity guards: Confirm requires Draft; Cancel
/// requires Draft or Confirmed. A Received or already-Cancelled order cannot transition here.
/// </summary>
public sealed class ChangePurchaseOrderStatusCommandHandler
    : IRequestHandler<ChangePurchaseOrderStatusCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public ChangePurchaseOrderStatusCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(ChangePurchaseOrderStatusCommand request, CancellationToken cancellationToken)
    {
        PurchaseOrder? order = await _db.PurchaseOrders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("أمر الشراء غير موجود."));
        }

        switch (request.Action)
        {
            case PurchaseOrderAction.Confirm:
                if (order.Status != PurchaseOrderStatus.Draft)
                {
                    return Result.Failure(Error.Conflict("لا يمكن تأكيد إلا أمر شراء في حالة مسودّة."));
                }
                order.Status = PurchaseOrderStatus.Confirmed;
                break;

            case PurchaseOrderAction.Cancel:
                if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Confirmed))
                {
                    return Result.Failure(Error.Conflict("لا يمكن إلغاء أمر شراء مُستلَم أو مُلغى."));
                }
                order.Status = PurchaseOrderStatus.Cancelled;
                break;

            default:
                return Result.Failure(Error.Validation(
                    "إجراء غير معروف.", [new FieldError("action", "إجراء غير صالح.")]));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
