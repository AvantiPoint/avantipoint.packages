using System.Data;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Serializes canonical content commits and collection across host processes.
/// Client/upstream bytes are staged before acquiring this lock. Keep every
/// digest-store mutation within this transaction; upload staging is independent.
/// </summary>
internal static class OciMutationTransaction
{
    public static async Task<IDbContextTransaction> BeginAsync(IContext context, OciScope scope, CancellationToken token)
    {
        if (context.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("OCI content mutation requires its own transaction.");
        var segment = scope.OciSegment ?? string.Empty;
        if (!await context.OciMutationGates.AsNoTracking().AnyAsync(gate => gate.FeedId == scope.FeedId && gate.Segment == segment, token))
        {
            var gate = new OciMutationGate { FeedId = scope.FeedId, Segment = segment };
            context.OciMutationGates.Add(gate);
            try { await context.SaveChangesAsync(token); }
            catch (DbUpdateException exception) when (context.IsUniqueConstraintViolationException(exception))
            {
                // Another process initialized the same lock target. Detach our failed
                // insert before continuing with the now-existing row.
                context.OciMutationGates.Remove(gate);
            }
        }

        var isolation = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted;
        var transaction = await context.Database.BeginTransactionAsync(isolation, token);
        try
        {
            var revision = Guid.NewGuid().ToString("N");
            var locked = await context.OciMutationGates.Where(gate => gate.FeedId == scope.FeedId && gate.Segment == segment)
                .ExecuteUpdateAsync(setters => setters.SetProperty(gate => gate.Revision, revision), token);
            if (locked != 1) throw new InvalidOperationException("OCI mutation lock target is missing.");
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
