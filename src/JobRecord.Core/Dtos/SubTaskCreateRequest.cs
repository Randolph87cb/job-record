namespace JobRecord.Core.Dtos;

public sealed class SubTaskCreateRequest
{
    public string Title { get; init; } = string.Empty;
    public int? EstimateMinutes { get; init; }
    public string? Notes { get; init; }
}
