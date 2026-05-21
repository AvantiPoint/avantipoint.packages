using System.Reflection;
namespace AvantiPoint.Packages.Host.Admin.Services.Email;

public interface IEmailTemplateProvider
{
    string ReadTemplate(string templateFileName);
}
