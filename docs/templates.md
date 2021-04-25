To make it even easier to get started you can use our full Package Feed template. The template is a full secured feed out of the box that integrates with your Azure Active Directory domain and uses Send Grid to send email notifications.

```powershell
dotnet new --install AvantiPoint.Packages.Templates::1.0.19
```

This pre-built solution will provide a full package feed list when you log in and provide ApiToken management similar to what you will find on NuGet.org. When creating, revoking, or regenerating an ApiToken it will email the user to notify them of the account activity. Additionally it will track usage of the Api Tokens and notify the user any time the Token is used from a new IP Address.
