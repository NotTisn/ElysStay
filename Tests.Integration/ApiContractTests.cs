using System.Net.Http.Json;
using Application.Common.Models;
using Application.Features.Buildings.DTOs;
using Tests.Integration.Fixtures;
using Xunit;
using FluentAssertions;

namespace Tests.Integration
{
    public class ApiContractTests : IClassFixture<AuthenticatedWebApplicationFactory>
    {
        private readonly AuthenticatedWebApplicationFactory _factory;

        public ApiContractTests(AuthenticatedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetBuildings_ShouldReturnPagedJsonEnvelope()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/v1/buildings?page=1&pageSize=20");

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

            var payload = await response.Content.ReadFromJsonAsync<PagedResponse<BuildingDto>>();
            payload.Should().NotBeNull();
            payload!.Success.Should().BeTrue();
            payload.Data.Should().NotBeNull();
        }
    }
}
