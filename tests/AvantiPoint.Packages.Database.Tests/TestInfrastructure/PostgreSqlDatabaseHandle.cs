using System;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;
namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed record PostgreSqlDatabaseHandle(string DatabaseName, string ConnectionString);
