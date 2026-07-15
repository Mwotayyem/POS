namespace SmartApp.Domain.Exceptions;

/// <summary>
/// Thrown when an operation attempts to write or associate data across tenant boundaries
/// (e.g. an entity carrying a foreign <c>TenantId</c>). A tenant leak is a Sev-1 security
/// concern — see SmartApp-Architecture/09-Multi-Tenant.md §2 and 11-Security-Architecture.md §2.
/// </summary>
public sealed class TenantMismatchException : DomainException
{
    public TenantMismatchException(string message)
        : base(message)
    {
    }

    public TenantMismatchException()
        : base("عملية عبر حدود المستأجرين مرفوضة (Tenant mismatch).")
    {
    }

    public override string Code => "TENANT_MISMATCH";
}
