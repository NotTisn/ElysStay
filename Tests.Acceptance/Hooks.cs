using TechTalk.SpecFlow;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tests.Integration.Fixtures;

namespace Tests.Acceptance
{
    [Binding]
    public class Hooks
    {
        private static WebApplicationFactory<Program> _factory;
        private static PostgreSqlContainer _dbContainer;

        [BeforeTestRun]
        public static async Task BeforeTestRun()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15")
                .Build();
            
            await _dbContainer.StartAsync();

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString() }
                    });
                });
            });
        }

        [BeforeScenario]
        public async Task BeforeScenario(ScenarioContext scenarioContext, DatabaseFixture fixture)
        {
            // Initialize the database fixture so DbContext is not null!
            await fixture.InitializeAsync();
            
            // Clear tables to prevent unique constraint errors between scenarios
            fixture.DbContext.Rooms.RemoveRange(fixture.DbContext.Rooms);
            fixture.DbContext.Buildings.RemoveRange(fixture.DbContext.Buildings);
            fixture.DbContext.Users.RemoveRange(fixture.DbContext.Users);
            await fixture.DbContext.SaveChangesAsync();
            
            var client = _factory.CreateClient();
            scenarioContext.Set(client);
        }

        [AfterTestRun]
        public static async Task AfterTestRun()
        {
            _factory?.Dispose();
            if (_dbContainer != null)
            {
                await _dbContainer.DisposeAsync();
            }
        }
    }
}
