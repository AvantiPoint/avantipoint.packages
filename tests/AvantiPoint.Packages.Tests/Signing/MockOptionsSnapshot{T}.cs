using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Xunit;
namespace AvantiPoint.Packages.Tests.Signing;

internal class MockOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
{
    public T Value { get; } = value;
    public T Get(string? name) => Value;
}

