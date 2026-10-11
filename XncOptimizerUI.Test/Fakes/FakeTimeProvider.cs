namespace XncOptimizerUI.Test.Fakes
{
    /// <summary>Minimal fixed clock; .NET 9 ships no in-box fake TimeProvider.</summary>
    internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
