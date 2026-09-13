using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Application;

public abstract partial class UseCaseBase<TRequest, TResult>
{
    /// <summary>
    /// A write needs a tenant. Reads degrade safely when none is resolved - the query filter simply
    /// matches nothing - but a write has to stamp a tenant onto the row it creates, and guessing one
    /// is how rows end up in the wrong place.
    /// </summary>
    protected static Guid RequireTenant(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.TenantId
            ?? throw new UseCaseValidationException("No tenant is selected for this operation.");
    }
}
