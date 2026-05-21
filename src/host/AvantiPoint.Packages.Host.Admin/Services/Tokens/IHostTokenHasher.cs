using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Options;
namespace AvantiPoint.Packages.Host.Admin.Services.Tokens;

public interface IHostTokenHasher
{
    (string Plaintext, string Prefix, string Hash) GenerateToken();

    bool Verify(string plaintext, string hash);
}

