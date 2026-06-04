using BoDi;
using Microsoft.EntityFrameworkCore;
using TechTalk.SpecFlow;
using Tests.Integration.Fixtures;

namespace ElysStay.Tests.Acceptance.Hooks;

/// <summary>
/// Manages the Testcontainers PostgreSQL lifecycle for acceptance tests.
/// One container is started per Feature, shared across all scenarios in that feature.
/// The schema is reset between scenarios to prevent state bleed.
/// </summary>
[Binding]
public class DatabaseHooks
{
    private readonly IObjectContainer _container;

    public DatabaseHooks(IObjectContainer container)
    {
        _container = container;
    }

    [BeforeFeature]
    public static async Task BeforeFeature(FeatureContext featureContext)
    {
        var fixture = new DatabaseFixture();
        await fixture.InitializeAsync();
        featureContext.Set(fixture);
    }

    [AfterFeature]
    public static async Task AfterFeature(FeatureContext featureContext)
    {
        if (featureContext.TryGetValue(out DatabaseFixture fixture))
            await fixture.DisposeAsync();
    }

    [BeforeScenario(Order = 0)]
    public async Task BeforeScenario(FeatureContext featureContext)
    {
        var fixture = featureContext.Get<DatabaseFixture>();

        // Detach entities tracked by the long-lived per-feature DbContext. Without this, entities
        // from the previous scenario stay tracked after the TRUNCATE below, so an Update+SaveChanges
        // hits a row that no longer exists → DbUpdateConcurrencyException ("0 rows affected").
        fixture.DbContext.ChangeTracker.Clear();

        // Truncate all tables between scenarios (faster and safer than drop+recreate).
        // RESTART IDENTITY resets sequences; CASCADE handles FK constraints.
        await fixture.DbContext.Database.ExecuteSqlRawAsync(@"
            DO $$
            DECLARE r RECORD;
            BEGIN
                FOR r IN SELECT tablename FROM pg_tables
                         WHERE schemaname = 'public'
                           AND tablename != '__EFMigrationsHistory'
                LOOP
                    EXECUTE 'TRUNCATE TABLE public.' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE';
                END LOOP;
            END $$;");

        _container.RegisterInstanceAs(fixture);
    }
}
