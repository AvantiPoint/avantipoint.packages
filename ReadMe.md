# AvantiPoint Packages

The AvantiPoint packages is largely based on the BaGet project, but with a few changes. The AvantiPoint packages library comes from some modifications that have been needed to provide custom authenticated feeds for SponsorConnect, and our own in-house NuGet feeds. This projects aims to make it ridiculously simple to create a new NuGet feed while plugging in your own logic for how to authenticate your users.

## Authentication

To authenticate your users you will need to implement `IPackageAuthenticationService`. If you're new to creating your own NuGet feed then it's important that you understand a few basics here on authentication with NuGet feeds.

While there are indeed custom authentication brokers that you could provide the NuGet client this can provide a lot more complexity than should otherwise be needed. For this reason we try to stick to some standards for what the NuGet client expects out of box. With that said we recognize 2 user roles currently.

1) Package Consumer
2) Package Publisher

While what each role can do should be pretty self explanatory how they are authenticated probably isn't.

As a standard for user authentication we expect a Basic authentication scheme for PackageConsumers. For package publishing however the NuGet client has a limitation that it only supports passing the Api Key via a special header which it handles. For this reason the `IPackageAuthenticationService` provides two methods. When simply validating a user based on the ApiToken it should be assumed that you are publishing and therefore if the user that the token belongs to does not have publishing rights you should return a failed result.

## Callbacks

In addition to User Authentication, AvantiPoint Packages offers a Callback API to allow you to handle custom logic such as sending emails or additional context tracking. To hook into these event you just need to register a delegate for `INuGetFeedActionHandler`.