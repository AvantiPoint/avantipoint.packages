using System;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Singleton metadata row for external search index reconciliation.
/// </summary>
public class SearchIndexState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int SchemaVersion { get; set; }

    public DateTime? LastReconcileCompletedAt { get; set; }

    public bool ReconcileInProgress { get; set; }
}
