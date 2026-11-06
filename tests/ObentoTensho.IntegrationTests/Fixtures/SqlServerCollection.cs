namespace ObentoTensho.IntegrationTests.Fixtures;

[CollectionDefinition(SqlServerFixture.CollectionName)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
