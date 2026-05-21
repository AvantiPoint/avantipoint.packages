using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
namespace AvantiPoint.Packages.Core;

public class NuGetConfigSource
{
    public string Name { get; set; }
    public string SourceUrl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
