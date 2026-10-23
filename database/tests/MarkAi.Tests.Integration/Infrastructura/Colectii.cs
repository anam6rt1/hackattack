using MarkAi.Tests.Suport;

[assembly: AssemblyFixture(typeof(MarkAi.Tests.Integration.OprireBaza))]

namespace MarkAi.Tests.Integration;

/// <summary>Stops the test PostgreSQL (or drops its template) after the last test.</summary>
public sealed class OprireBaza : IAsyncDisposable
{
    public ValueTask DisposeAsync() => new(BazaTest.Opreste());
}

/// <summary>Twilio's credentials are process-wide environment variables, so these tests run alone, after the others.</summary>
[CollectionDefinition("Twilio", DisableParallelization = true)]
public sealed class ColectieTwilio;
