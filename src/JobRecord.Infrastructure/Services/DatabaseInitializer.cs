using JobRecord.Core.Abstractions;
using JobRecord.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRecord.Infrastructure.Services;

public sealed class DatabaseInitializer(JobRecordDbContext dbContext) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSubTaskSchemaAsync(cancellationToken);
    }

    private async Task EnsureSubTaskSchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SubTasks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_SubTasks" PRIMARY KEY,
                "TaskItemId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "EstimateMinutes" INTEGER NULL,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "StartedAt" TEXT NULL,
                "CompletedAt" TEXT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsArchived" INTEGER NOT NULL,
                CONSTRAINT "FK_SubTasks_Tasks_TaskItemId" FOREIGN KEY ("TaskItemId") REFERENCES "Tasks" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        if (!await ColumnExistsAsync("TimeEntries", "SubTaskItemId", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "TimeEntries" ADD COLUMN "SubTaskItemId" TEXT NULL;""",
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_SubTasks_TaskItemId_IsArchived_Status_SortOrder"
                ON "SubTasks" ("TaskItemId", "IsArchived", "Status", "SortOrder");
            CREATE INDEX IF NOT EXISTS "IX_TimeEntries_SubTaskItemId"
                ON "TimeEntries" ("SubTaskItemId");
            """,
            cancellationToken);
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
