using JobRecord.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRecord.Tests.Common;

public static class TestDbContextFactory
{
    public static TestDbScope CreateInMemory()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<JobRecordDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new JobRecordDbContext(options);
        context.Database.EnsureCreated();

        return new TestDbScope(context, connection);
    }
}

public sealed class TestDbScope(JobRecordDbContext context, SqliteConnection connection) : IDisposable
{
    public JobRecordDbContext Context { get; } = context;
    public SqliteConnection Connection { get; } = connection;

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
