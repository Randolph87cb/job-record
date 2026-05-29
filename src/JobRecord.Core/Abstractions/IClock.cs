namespace JobRecord.Core.Abstractions;

public interface IClock
{
    DateTimeOffset Now { get; }
}
