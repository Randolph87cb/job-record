using JobRecord.Core.Abstractions;
using JobRecord.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRecord.Infrastructure.Services;

public sealed class DatabaseInitializer(JobRecordDbContext dbContext) : IDatabaseInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => dbContext.Database.EnsureCreatedAsync(cancellationToken);
}
