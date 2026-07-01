namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Options that can source their connection string either inline (<see cref="ConnectionString"/>)
    /// or by referencing a named entry under the root <c>ConnectionStrings</c> configuration section
    /// (<see cref="ConnectionStringName"/>). The name is resolved by
    /// <see cref="ResolveConnectionStringName{TOptions}"/> before validation, so consumers only ever
    /// need to read <see cref="ConnectionString"/>.
    /// </summary>
    public interface IConnectionStringOptions
    {
        /// <summary>
        /// The connection string. When both this and <see cref="ConnectionStringName"/> are supplied,
        /// this value takes precedence.
        /// </summary>
        string ConnectionString { get; set; }

        /// <summary>
        /// The name of a connection string defined under the root <c>ConnectionStrings</c> section
        /// (for example <c>ConnectionStrings__Packages</c>). Only used when <see cref="ConnectionString"/>
        /// is not otherwise set.
        /// </summary>
        string ConnectionStringName { get; set; }
    }
}
