using Xunit;

namespace SIGIC.WebApi.Tests;

[CollectionDefinition("WebApiTestCollection")]
public class WebApiTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
