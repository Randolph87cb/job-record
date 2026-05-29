using JobRecord.Core.Models;

namespace JobRecord.Core.Abstractions;

public interface IJobRecordDbContext
{
    IQueryable<TaskItem> Tasks { get; }
    IQueryable<TimeEntry> TimeEntries { get; }
    IQueryable<AppSettings> Settings { get; }
    IQueryable<RuntimeState> RuntimeStates { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
