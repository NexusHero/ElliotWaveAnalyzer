using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for the shared <see cref="CronBackgroundService"/> scheduling machinery via a
/// concrete probe subclass: an invalid cron stops the loop without ever running the work, and
/// a valid schedule cancels cleanly while waiting for the next occurrence.
/// </summary>
[TestFixture]
public sealed class CronBackgroundServiceTests
{
    private sealed class ProbeCronService(string cron) : CronBackgroundService(
        new ServiceCollection().BuildServiceProvider(), NullLogger<ProbeCronService>.Instance)
    {
        public int Runs { get; private set; }

        protected override string SchedulerName => "Probe scheduler";
        protected override string CronExpression => cron;

        protected override Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task InvalidCron_StopsWithoutRunningTheWork()
    {
        var service = new ProbeCronService("not a cron");

        await ((IHostedService)service).StartAsync(CancellationToken.None);

        Assert.That(service.Runs, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidCron_CancelledWhileWaiting_StopsCleanly()
    {
        // "every minute" — the loop enters Task.Delay until the next minute; stopping must
        // cancel that wait without throwing and without having run the work.
        var service = new ProbeCronService("* * * * *");

        await ((IHostedService)service).StartAsync(CancellationToken.None);
        await ((IHostedService)service).StopAsync(CancellationToken.None);

        Assert.That(service.Runs, Is.EqualTo(0));
    }
}
