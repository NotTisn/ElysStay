using Tests.Integration.Fixtures;
using Xunit;
using FluentAssertions;

namespace Tests.Integration
{
    public class BackgroundServiceTests : IClassFixture<AuthenticatedWebApplicationFactory>
    {
        private readonly AuthenticatedWebApplicationFactory _factory;

        public BackgroundServiceTests(AuthenticatedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task BackgroundService_ShouldStartSuccessfully()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/healthz");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}
