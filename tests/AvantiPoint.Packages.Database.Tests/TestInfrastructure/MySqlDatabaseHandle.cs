using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using MySqlConnector;
using Testcontainers.MySql;
using Testcontainers.Xunit;
using Xunit.Sdk;
namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed record MySqlDatabaseHandle(string DatabaseName, string ConnectionString);
