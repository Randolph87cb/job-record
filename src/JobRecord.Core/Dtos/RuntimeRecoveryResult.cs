namespace JobRecord.Core.Dtos;

public sealed class RuntimeRecoveryResult
{
    public int FixedEntriesCount { get; init; }
    public int PausedTasksCount { get; init; }
}
