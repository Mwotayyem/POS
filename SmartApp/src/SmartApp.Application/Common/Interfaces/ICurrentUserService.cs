namespace SmartApp.Application.Common.Interfaces;

/// <summary>
/// Provides the identity of the current authenticated user, used to populate audit fields
/// (CreatedBy / ModifiedBy / DeletedBy). Implemented in the Infrastructure layer.
/// See SmartApp-Architecture/03-Project-Structure.md §4.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>The current user id, or <c>null</c> for unauthenticated/system contexts.</summary>
    long? UserId { get; }

    /// <summary>True when there is an authenticated user on the request.</summary>
    bool IsAuthenticated { get; }
}
