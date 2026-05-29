using JobRecord.Core.Abstractions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;

namespace JobRecord.Core.Services;

public sealed class StatisticsService(IJobRecordDbContext dbContext, IClock clock) : IStatisticsService
{
    public async Task<TodaySummary> GetTodaySummaryAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var targetDay = day ?? DateOnly.FromDateTime(clock.Now.LocalDateTime.Date);
        var priority = await GetPrioritySummaryAsync(targetDay, cancellationToken);
        var completed = await GetTodayCompletedTasksAsync(targetDay, cancellationToken);

        return new TodaySummary
        {
            TotalDuration = priority.Aggregate(TimeSpan.Zero, static (sum, item) => sum + item.Duration),
            CompletedTaskCount = completed,
            PriorityBreakdown = priority
        };
    }

    public Task<TimeSpan> GetTaskDurationAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var duration = dbContext.TimeEntries
            .Where(entry => entry.TaskItemId == taskId)
            .AsEnumerable()
            .Aggregate(TimeSpan.Zero, (sum, entry) => sum + GetEffectiveDuration(entry));

        return Task.FromResult(duration);
    }

    public Task<int> GetTodayCompletedTasksAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var targetDay = day ?? DateOnly.FromDateTime(clock.Now.LocalDateTime.Date);
        var offset = clock.Now.Offset;
        var start = new DateTimeOffset(targetDay.Year, targetDay.Month, targetDay.Day, 0, 0, 0, offset);
        var end = start.AddDays(1);

        var count = dbContext.Tasks
            .AsEnumerable()
            .Count(task =>
                task.CompletedAt.HasValue &&
                task.CompletedAt.Value >= start &&
                task.CompletedAt.Value < end);

        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<PrioritySummaryItem>> GetPrioritySummaryAsync(DateOnly? day = null, CancellationToken cancellationToken = default)
    {
        var targetDay = day ?? DateOnly.FromDateTime(clock.Now.LocalDateTime.Date);
        var offset = clock.Now.Offset;
        var dayStart = new DateTimeOffset(targetDay.Year, targetDay.Month, targetDay.Day, 0, 0, 0, offset);
        var dayEnd = dayStart.AddDays(1);

        var taskPriorityMap = dbContext.Tasks.ToDictionary(task => task.Id, task => task.Priority);
        var durations = Enum.GetValues<TaskPriority>()
            .ToDictionary(priority => priority, _ => TimeSpan.Zero);

        foreach (var entry in dbContext.TimeEntries.AsEnumerable())
        {
            if (!taskPriorityMap.TryGetValue(entry.TaskItemId, out var priority))
            {
                continue;
            }

            foreach (var segment in SplitEntry(entry, dayStart, dayEnd))
            {
                durations[priority] += segment;
            }
        }

        var items = durations
            .Select(pair => new PrioritySummaryItem
            {
                Priority = pair.Key,
                Duration = pair.Value
            })
            .OrderBy(item => item.Priority)
            .ToList();

        return Task.FromResult<IReadOnlyList<PrioritySummaryItem>>(items);
    }

    public async Task<TimeSpan> GetCurrentTaskDisplayDurationAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var storedDuration = dbContext.TimeEntries
            .Where(entry => entry.TaskItemId == taskId && entry.EndAt.HasValue)
            .AsEnumerable()
            .Aggregate(TimeSpan.Zero, (sum, entry) => sum + TimeSpan.FromSeconds(entry.DurationSeconds));

        var activeEntry = dbContext.TimeEntries.SingleOrDefault(entry => entry.TaskItemId == taskId && entry.EndAt == null);
        if (activeEntry is null)
        {
            return storedDuration;
        }

        var liveDuration = clock.Now - activeEntry.StartAt;
        return await Task.FromResult(storedDuration + liveDuration);
    }

    private static IEnumerable<TimeSpan> SplitEntry(Models.TimeEntry entry, DateTimeOffset dayStart, DateTimeOffset dayEnd)
    {
        var effectiveEnd = entry.EndAt ?? DateTimeOffset.Now;
        var effectiveStart = entry.StartAt;

        if (effectiveEnd <= dayStart || effectiveStart >= dayEnd)
        {
            yield break;
        }

        var segmentStart = effectiveStart < dayStart ? dayStart : effectiveStart;
        var segmentEnd = effectiveEnd > dayEnd ? dayEnd : effectiveEnd;
        if (segmentEnd > segmentStart)
        {
            yield return segmentEnd - segmentStart;
        }
    }

    private TimeSpan GetEffectiveDuration(Models.TimeEntry entry)
    {
        if (entry.EndAt.HasValue)
        {
            return TimeSpan.FromSeconds(entry.DurationSeconds);
        }

        return clock.Now - entry.StartAt;
    }
}
