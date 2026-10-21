using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration;

/// <summary>Base for integration tests: every test gets its own app and database (<see cref="Harness"/>), started before it runs.</summary>
public abstract class TestCuHarness : IAsyncLifetime
{
    protected Harness h = null!;

    /// <summary>Settings for this class's harness (e.g. relaxed limits).</summary>
    protected virtual IReadOnlyDictionary<string, string?>? Setari => null;
    protected virtual bool CuTwilio => false;

    public virtual async ValueTask InitializeAsync() => h = await Harness.Porneste(cuTwilio: CuTwilio, setari: Setari);
    public virtual async ValueTask DisposeAsync()
    {
        if (h is null) return;
        // A failed test shows what the app logged, which usually says why (the client only sees a generic message).
        if (TestContext.Current.TestState?.Result == TestResult.Failed)
            foreach (var linie in h.Jurnal.Linii.TakeLast(40)) TestContext.Current.TestOutputHelper?.WriteLine(linie);
        await h.DisposeAsync();
    }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}
