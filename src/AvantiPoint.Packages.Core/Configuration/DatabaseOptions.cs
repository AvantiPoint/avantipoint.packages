using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Core;

public class DatabaseOptions : IConnectionStringOptions
{
    public string Type { get; set; }

    /// <summary>
    /// The database connection string.
    /// </summary>
    /// <remarks>
    /// This may be supplied directly (for example via <c>Database__ConnectionString</c>), or
    /// resolved from a named connection string by setting <see cref="ConnectionStringName"/>.
    /// When both are supplied, this value takes precedence.
    /// </remarks>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The name of a connection string defined under the root <c>ConnectionStrings</c> section
    /// (for example <c>ConnectionStrings__Packages</c>) to use for <see cref="ConnectionString"/>.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="ConnectionString"/> is not otherwise set.
    /// </remarks>
    public string ConnectionStringName { get; set; } = string.Empty;
}
