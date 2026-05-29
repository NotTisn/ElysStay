using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.BackgroundJobs;

public class BackgroundJobHealthCheck : IHealthCheck
{
    private readonly ConcurrentDictionary<string, DateTime> _lastRunTimes = new();
    private readonly ConcurrentDictionary<string, Exception?> _lastErrors = new();

    public void ReportJobRun(string jobName)
    {
        _lastRunTimes[jobName] = DateTime.UtcNow;
        _lastErrors[jobName] = null;
    }

    public void ReportJobError(string jobName, Exception ex)
    {
        _lastErrors[jobName] = ex;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var degradedJobs = new List<string>();
        var data = new Dictionary<string, object>();

        foreach (var job in _lastRunTimes)
        {
            var jobName = job.Key;
            var lastRunTime = job.Value;
            var hasError = _lastErrors.TryGetValue(jobName, out var error) && error != null;

            data[$"{jobName}_LastRun"] = lastRunTime;
            
            // If the job hasn't run in 25 hours (daily schedule + buffer) or has an active error
            if (hasError || DateTime.UtcNow - lastRunTime > TimeSpan.FromHours(25))
            {
                degradedJobs.Add(jobName);
                if (hasError)
                {
                    data[$"{jobName}_Error"] = error!.Message;
                }
            }
        }

        if (degradedJobs.Any())
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: $"Background jobs degraded: {string.Join(", ", degradedJobs)}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All background jobs are healthy", data));
    }
}
