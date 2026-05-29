using FluentAssertions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Services;
using JobRecord.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRecord.Tests.Infrastructure;

public sealed class PersistenceTests
{
    [Fact]
    public async Task SqliteFile_ShouldPersistDataAcrossContextRestarts()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<JobRecordDbContext>()
                .UseSqlite($"Data Source={tempFile}")
                .Options;

            var clock = new Common.TestClock(new DateTimeOffset(2026, 5, 29, 8, 0, 0, TimeSpan.Zero));

            await using (var firstContext = new JobRecordDbContext(options))
            {
                await firstContext.Database.EnsureCreatedAsync();
                var taskService = new TaskService(firstContext, clock);
                await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "持久化任务" });
            }

            await using (var secondContext = new JobRecordDbContext(options))
            {
                var tasks = secondContext.TaskItems.ToList();
                tasks.Should().ContainSingle();
                tasks[0].Title.Should().Be("持久化任务");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
