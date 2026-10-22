using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Feed.Platform.Authentication;

public sealed record FeedAuthenticationRequest(
    SurfaceContext Surface,
    HttpContext HttpContext,
    FeedOperation Operation);
