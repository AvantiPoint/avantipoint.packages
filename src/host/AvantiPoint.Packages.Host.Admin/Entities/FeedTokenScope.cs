namespace AvantiPoint.Packages.Host.Admin.Entities;

[Flags]
public enum FeedTokenScope
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
}
