namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

[CollectionDefinition("MySqlDatabase", DisableParallelization = true)]
public sealed class MySqlDatabaseCollection : ICollectionFixture<MySqlTestcontainerFixture>;
