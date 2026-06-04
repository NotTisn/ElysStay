using Xunit;

// Acceptance tests share a single PostgreSQL Testcontainer (see DatabaseHooks), and the
// BeforeScenario hook TRUNCATEs all tables between scenarios. Running feature classes in
// parallel would let one feature wipe/overwrite another feature's in-flight data, causing
// DbUpdateConcurrencyException and state bleed. Force sequential execution.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
