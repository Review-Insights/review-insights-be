using debil_be.Entities;
using Microsoft.EntityFrameworkCore;

namespace debil_be.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<BlueprintTask> BlueprintTasks => Set<BlueprintTask>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<AnalysisRow> AnalysisRows => Set<AnalysisRow>();
    public DbSet<TaskMetric> TaskMetrics => Set<TaskMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blueprint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.DataStructure).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<BlueprintTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TaskName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Question).HasMaxLength(2000);
            entity.Property(e => e.Instruction).HasMaxLength(4000);
            entity.Property(e => e.Values).HasColumnType("jsonb");
            entity.Property(e => e.Format).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);

            entity.HasOne(e => e.Blueprint)
                .WithMany(b => b.Tasks)
                .HasForeignKey(e => e.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.BlueprintId);
        });

        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlueprintName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Filename).IsRequired().HasMaxLength(512);
            entity.Property(e => e.FileStorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(e => e.InputColumns).HasColumnType("jsonb");
            entity.Property(e => e.OutputColumns).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Blueprint)
                .WithMany(b => b.Analyses)
                .HasForeignKey(e => e.BlueprintId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BlueprintId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<AnalysisRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InputData).HasColumnType("jsonb");
            entity.Property(e => e.OutputData).HasColumnType("jsonb");

            entity.HasOne(e => e.Analysis)
                .WithMany(a => a.Rows)
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AnalysisId);
            entity.HasIndex(e => new { e.AnalysisId, e.RowIndex }).IsUnique();
        });

        modelBuilder.Entity<TaskMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TaskName).HasMaxLength(256);
            entity.Property(e => e.ModelName).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AnalysisId);
            entity.HasIndex(e => new { e.AnalysisId, e.TaskId }).IsUnique();
        });
    }
}
