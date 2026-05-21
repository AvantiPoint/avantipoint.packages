using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Xunit;
using Xunit.Sdk;
namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed record SqlServerDatabaseHandle(string DatabaseName, string ConnectionString);
