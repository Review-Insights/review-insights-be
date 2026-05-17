using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Domain.Entities;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FileUpload> FileUploads => Set<FileUpload>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileUpload>(entity =>
        {
            entity.ToTable("file_uploads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(512);
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.StorageKey).HasColumnName("storage_key").IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Status).HasColumnName("status")
                .HasConversion<int>().IsRequired();
            entity.Property(e => e.TotalRecords).HasColumnName("total_records");
            entity.Property(e => e.AnalyzedRecords).HasColumnName("analyzed_records");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            entity.HasIndex(e => e.Status).HasDatabaseName("ix_file_uploads_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_file_uploads_created_at");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClothingId).HasColumnName("clothing_id");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.ReviewText).HasColumnName("review_text");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.RecommendedInd).HasColumnName("recommended_ind");
            entity.Property(e => e.PositiveFeedbackCount).HasColumnName("positive_feedback_count");
            entity.Property(e => e.DivisionName).HasColumnName("division_name").HasMaxLength(100);
            entity.Property(e => e.DepartmentName).HasColumnName("department_name").HasMaxLength(100);
            entity.Property(e => e.ClassName).HasColumnName("class_name").HasMaxLength(100);

            entity.Property(e => e.OverallSentiment).HasColumnName("overall_sentiment")
                .HasConversion<int>();
            entity.Property(e => e.AspectSentiments).HasColumnName("aspect_sentiments")
                .HasColumnType("jsonb");
            entity.Property(e => e.ChurnProbability).HasColumnName("churn_probability");
            entity.Property(e => e.ChurnCauses).HasColumnName("churn_causes")
                .HasColumnType("jsonb");
            entity.Property(e => e.Priority).HasColumnName("priority")
                .HasConversion<int>();
            entity.Property(e => e.PriorityRule).HasColumnName("priority_rule").HasMaxLength(100);
            entity.Property(e => e.PriorityReason).HasColumnName("priority_reason");

            entity.Property(e => e.UploadId).HasColumnName("upload_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.AnalyzedAt).HasColumnName("analyzed_at");

            entity.HasOne(e => e.Upload)
                .WithMany(u => u.Reviews)
                .HasForeignKey(e => e.UploadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UploadId).HasDatabaseName("ix_reviews_upload_id");
            entity.HasIndex(e => e.ClothingId).HasDatabaseName("ix_reviews_clothing_id");
            entity.HasIndex(e => e.Priority).HasDatabaseName("ix_reviews_priority");
            entity.HasIndex(e => e.OverallSentiment).HasDatabaseName("ix_reviews_sentiment");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_reviews_created_at");
            entity.HasIndex(e => e.DepartmentName).HasDatabaseName("ix_reviews_department");
            entity.HasIndex(e => e.Rating).HasDatabaseName("ix_reviews_rating");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("reports");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasColumnName("status")
                .HasConversion<int>().IsRequired();
            entity.Property(e => e.Filters).HasColumnName("filters").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Scope).HasColumnName("scope").HasColumnType("jsonb");
            entity.Property(e => e.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.TotalRecords).HasColumnName("total_records");
            entity.Property(e => e.Summary).HasColumnName("summary").HasColumnType("jsonb");
            entity.Property(e => e.Insights).HasColumnName("insights").HasColumnType("jsonb");
            entity.Property(e => e.Suggestions).HasColumnName("suggestions").HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            entity.HasIndex(e => e.Status).HasDatabaseName("ix_reports_status");
            entity.HasIndex(e => e.GeneratedAt).HasDatabaseName("ix_reports_generated_at");
        });
    }
}
