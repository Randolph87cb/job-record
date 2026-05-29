using JobRecord.Core.Abstractions;

namespace JobRecord.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
