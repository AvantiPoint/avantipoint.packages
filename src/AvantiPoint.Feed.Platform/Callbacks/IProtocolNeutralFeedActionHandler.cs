namespace AvantiPoint.Feed.Platform.Callbacks;

/// <summary>
/// Marker for an <see cref="IFeedActionHandler"/> that applies to every protocol (NuGet, npm, OCI, ...),
/// as opposed to <c>NuGetFeedActionHandlerAdapter</c> which only forwards NuGet artifact events.
/// Registered instances are combined into the composite <see cref="IFeedActionHandler"/> alongside the
/// NuGet-specific adapter, so protocol-agnostic concerns (audit logging, webhooks) fire for every registry.
/// </summary>
public interface IProtocolNeutralFeedActionHandler : IFeedActionHandler
{
}
