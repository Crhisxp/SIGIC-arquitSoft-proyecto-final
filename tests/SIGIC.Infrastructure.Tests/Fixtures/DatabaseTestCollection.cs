using Xunit;

namespace SIGIC.Infrastructure.Tests.Fixtures;

[CollectionDefinition("PostgreSqlDatabase")]
public class DatabaseTestCollection : ICollectionFixture<PostgreSqlTestFixture>
{
}
