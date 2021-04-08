# AvantiPoint Packages

The AvantiPoint packages is largely based on the BaGet project, but with a few changes. The AvantiPoint packages library comes from some modifications that have been needed to provide custom authenticated feeds for [SponsorConnect](https://sponsorconnect.dev), and our own in-house NuGet feeds. This projects aims to make it ridiculously simple to create a new NuGet feed while plugging in your own logic for how to authenticate your users.

## How is this different from BaGet

AvantiPoint Packages largely comes from the work done for Sponsor Connect. While this started with the BaGet codebase, several updates have been made.

- Upgraded from netcoreapp3.1 to net5.0
- Updated / Upgraded packages... removed deprecated packages
- More advanced User Authentication
  - Authentication is separate from AspNetCore authentication meaning it will work with your existing site regardless of what you're doing and requires very little configuration on your part besides registering a single interface.
  - Allows you to define more complex requirements for authenticating package publishing allowing you to provide different tokens for different users.
  - Allows you to secure your NuGet feed so that only authenticated users can access the feed.
- Provides hooks so that you can respond to Upload / Download events
  - Allows you to filter users access to specific packages
  - Allows you to collect metrics on Package and/or Symbols downloads
  - Allows you to email confirmations when Packages or Symbols are uploaded

## Authentication

To authenticate your users you will need to implement `IPackageAuthenticationService`. If you're new to creating your own NuGet feed then it's important that you understand a few basics here on authentication with NuGet feeds.

While there are indeed custom authentication brokers that you could provide the NuGet client this can provide a lot more complexity than should otherwise be needed. For this reason we try to stick to some standards for what the NuGet client expects out of box. With that said we recognize 2 user roles currently.

1) Package Consumer
2) Package Publisher

> **NOTE:** When we say Roles, we do not use AspNetCore Authentication mechanisms, and thus do not care in any shape, way, or form about the Role claims of the ClaimsPrincipal.

While what each role can do should be pretty self explanatory how they are authenticated probably isn't.

As a standard for user authentication we expect a Basic authentication scheme for PackageConsumers. For package publishing however the NuGet client has a limitation that it only supports passing the Api Key via a special header which it handles. For this reason the `IPackageAuthenticationService` provides two methods. When simply validating a user based on the ApiToken it should be assumed that you are publishing and therefore if the user that the token belongs to does not have publishing rights you should return a failed result.

## Callbacks

In addition to User Authentication, AvantiPoint Packages offers a Callback API to allow you to handle custom logic such as sending emails or additional context tracking. To hook into these event you just need to register a delegate for `INuGetFeedActionHandler`.

## Samples

While we have a basic implementation of the IPackageAuthenticationService in the AuthenticatedFeed sample... below is a sample to give you a little more complexity and understanding of how you might use this to authenticate your users.

```c#
public class MyAuthService : IPackageAuthenticationService
{
    private MyDbContext _db { get; }

    public MyAuthService(MyDbContext _db)
    {
        _db = db;
    }

    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var token = await _db.PackageTokens
                            .Include(x => x.User)
                            .ThenInclude(x => x.Permissions)
                            .FirstOrDefaultAsync(x => x.Token == apiKey);

        if (token is null || token.IsExpiredOrRevoked())
        {
            return NuGetAuthenticationResult.Fail("Unknown user or Invalid Api Token.", "Contoso Corp Feed");
        }

        if (!token.User.Permissions.Any(x => x.Name == "PackagePublisher"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized to publish packages.", "Contoso Corp Feed");
        }

        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));

        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }

    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
    {
        var token = await _db.PackageTokens
                            .Include(x => x.User)
                            .ThenInclude(x => x.Permissions)
                            .FirstOrDefaultAsync(x => x.Token == token && x.User.Email == username);

        if (token is null || token.IsExpiredOrRevoked())
        {
            return NuGetAuthenticationResult.Fail("Unknown user or Invalid Api Token.", "Contoso Corp Feed");
        }

        if (!token.User.Permissions.Any(x => x.Name == "PackageConsumer"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized.", "Contoso Corp Feed");
        }

        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));

        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }
}
```

It's worth noting here that AvantiPoint Packages itself does not care at all about the ClaimsPrincipal, however if you provide one to the NuGetAuthenticationResult it will set this to the HttpContext so that it is available to you in your callbacks.
