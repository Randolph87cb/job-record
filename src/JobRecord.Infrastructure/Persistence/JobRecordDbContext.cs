using JobRecord.Core.Abstractions;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace JobRecord.Infrastructure.Persistence;

public sealed class JobRecordDbContext(DbContextOptions<JobRecordDbContext> options)
    : DbContext(options), IJobRecordDbContext
{
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TimeEntry> TimeEntriesSet => Set<TimeEntry>();
    public DbSet<AppSettings> AppSettingsSet => Set<AppSettings>();
    public DbSet<RuntimeState> RuntimeStatesSet => Set<RuntimeState>();

    IQueryable<TaskItem> IJobRecordDbContext.Tasks => TaskItems;
    IQueryable<TimeEntry> IJobRecordDbContext.TimeEntries => TimeEntriesSet;
    IQueryable<AppSettings> IJobRecordDbContext.Settings => AppSettingsSet;
    IQueryable<RuntimeState> IJobRecordDbContext.RuntimeStates => RuntimeStatesSet;

    public new void Add<T>(T entity) where T : class => Set<T>().Add(entity);

    public new void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(builder =>
        {
            builder.ToTable("Tasks");
            builder.HasKey(task => task.Id);
            builder.Property(task => task.Title).HasMaxLength(80).IsRequired();
            builder.Property(task => task.Priority).HasConversion<int>();
            builder.Property(task => task.Status).HasConversion<int>();
            builder.Property(task => task.Notes).HasMaxLength(1000);
            builder.HasIndex(task => new { task.IsArchived, task.Status, task.SortOrder });
        });

        modelBuilder.Entity<TimeEntry>(builder =>
        {
            builder.ToTable("TimeEntries");
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.EntryType).HasConversion<int>();
            builder.HasIndex(entry => entry.TaskItemId);
            builder.HasIndex(entry => entry.EndAt);
            builder.HasOne(entry => entry.TaskItem)
                .WithMany(task => task.TimeEntries)
                .HasForeignKey(entry => entry.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSettings>(builder =>
        {
            builder.ToTable("AppSettings");
            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.Id).ValueGeneratedNever();
            builder.Property(settings => settings.DockMode).HasConversion<int>();
        });

        modelBuilder.Entity<RuntimeState>(builder =>
        {
            builder.ToTable("RuntimeState");
            builder.HasKey(state => state.Id);
            builder.Property(state => state.Id).ValueGeneratedNever();
        });

        SeedDefaults(modelBuilder);
    }

    private static void SeedDefaults(ModelBuilder modelBuilder)
    {
        var seedTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<AppSettings>().HasData(new AppSettings
        {
            Id = AppSettings.SingletonId,
            DockMode = DockMode.TopCenter,
            BarWidth = 460,
            BarHeight = 34,
            MarginTop = 6,
            MarginSide = 12,
            WindowLeft = null,
            WindowTop = null,
            AutoCollapseEnabled = true,
            AutoCollapseSeconds = 8,
            LaunchAtStartup = false,
            MinimizeToTray = true,
            IsBarVisible = true,
            UpdatedAt = seedTime
        });

        modelBuilder.Entity<RuntimeState>().HasData(new RuntimeState
        {
            Id = RuntimeState.SingletonId,
            CurrentTaskId = null,
            IsBarVisible = true,
            IsExpanded = false,
            LastActiveAt = seedTime,
            UpdatedAt = seedTime
        });
    }
}
