using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="EmailDeliveryChannel"/>'s enabled predicate — it requires the flag,
/// a host, a from-address and at least one recipient. (The SMTP send itself is I/O and is
/// excluded from coverage as adapter plumbing.)
/// </summary>
[TestFixture]
public sealed class EmailDeliveryChannelTests
{
    private static EmailDeliveryChannel Build(EmailOptions email) =>
        new(
            Options.Create(new DailyReportOptions { Email = email }),
            NullLogger<EmailDeliveryChannel>.Instance);

    private static EmailOptions Email(
        bool enabled = true,
        string host = "smtp.example.com",
        string from = "reports@example.com",
        string[]? to = null) =>
        new() { Enabled = enabled, Host = host, From = from, To = to ?? ["me@example.com"] };

    [Test]
    public void Name_IsEmail() => Assert.That(Build(Email()).Name, Is.EqualTo("Email"));

    [Test]
    public void IsEnabled_WhenFullyConfigured_IsTrue()
        => Assert.That(Build(Email()).IsEnabled, Is.True);

    [Test]
    public void IsEnabled_MissingPieces_IsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Build(Email(enabled: false)).IsEnabled, Is.False);
            Assert.That(Build(Email(host: "")).IsEnabled, Is.False);
            Assert.That(Build(Email(from: "")).IsEnabled, Is.False);
            Assert.That(Build(Email(to: [])).IsEnabled, Is.False);
        });
    }
}
