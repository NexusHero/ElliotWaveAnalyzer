using NetArchTest.Rules;

namespace ElliotWaveAnalyzer.Tests.Architecture;

/// <summary>
/// Enforces the layering and encapsulation rules at test time: Domain, Application and the
/// public Interfaces must not depend on Infrastructure, and Infrastructure implementation
/// types stay internal so they can only be reached through their interfaces.
/// </summary>
[TestFixture]
public sealed class ArchitectureTests
{
    private const string ApiAssembly = "ElliotWaveAnalyzer.Api";

    [Test]
    public void Domain_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ElliotWaveAnalyzer.Api.Domain.MarketCandle).Assembly)
            .That().ResideInNamespace($"{ApiAssembly}.Domain")
            .ShouldNot().HaveDependencyOn($"{ApiAssembly}.Infrastructure")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    [Test]
    public void Application_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ElliotWaveAnalyzer.Api.Domain.MarketCandle).Assembly)
            .That().ResideInNamespace($"{ApiAssembly}.Application")
            .ShouldNot().HaveDependencyOn($"{ApiAssembly}.Infrastructure")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    [Test]
    public void Interfaces_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ElliotWaveAnalyzer.Api.Domain.MarketCandle).Assembly)
            .That().ResideInNamespace($"{ApiAssembly}.Interfaces")
            .ShouldNot().HaveDependencyOn($"{ApiAssembly}.Infrastructure")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    [Test]
    public void Infrastructure_Implementations_MustBe_Internal()
    {
        var result = Types.InAssembly(typeof(ElliotWaveAnalyzer.Api.Domain.MarketCandle).Assembly)
            .That().ResideInNamespace($"{ApiAssembly}.Infrastructure")
            .And().AreClasses()
            .ShouldNot().BePublic()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            "Infrastructure implementation types should be internal: " +
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
