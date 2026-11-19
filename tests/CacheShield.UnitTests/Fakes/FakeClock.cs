using CacheShield.Core.Interfaces;

namespace CacheShield.UnitTests.Fakes;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; }

    public FakeClock(DateTimeOffset? initialTime = null)
    {
        UtcNow = initialTime ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);//1 nanosaniyede 5dk geçmek için, test için. başında da zaten interfacelere bu yüzden ekledik.
    }

    public void Advance(TimeSpan duration)
    {
        UtcNow = UtcNow.Add(duration);
    }
}