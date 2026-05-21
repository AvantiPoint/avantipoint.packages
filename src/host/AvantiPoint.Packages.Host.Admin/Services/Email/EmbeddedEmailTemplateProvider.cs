using System.Reflection;

namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public sealed class EmbeddedEmailTemplateProvider : IEmailTemplateProvider
{
    private static readonly Assembly Assembly = typeof(EmbeddedEmailTemplateProvider).Assembly;

    public string ReadTemplate(string templateFileName)
    {
        var resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(templateFileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Email template '{templateFileName}' was not found.");

        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Email template '{templateFileName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

