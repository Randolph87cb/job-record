using JobRecord.Core.Abstractions;

namespace JobRecord.Tests.Common;

public sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; private set; } = now;

    public void Advance(TimeSpan delta) => Now = Now.Add(delta);

    public void Set(DateTimeOffset value) => Now = value;
}
