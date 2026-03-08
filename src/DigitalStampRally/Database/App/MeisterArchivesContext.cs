using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MeisterArchives.Database;

public partial class MeisterArchivesContext : DbContext
{
    public MeisterArchivesContext(DbContextOptions<MeisterArchivesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Archive> Archives { get; set; }

    public virtual DbSet<ArchiveHistory> ArchiveHistories { get; set; }

    public virtual DbSet<ArchiveReadLog> ArchiveReadLogs { get; set; }

    public virtual DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

    public virtual DbSet<Meister> Meisters { get; set; }

    public virtual DbSet<PendingRegistration> PendingRegistrations { get; set; }

    public virtual DbSet<PriceSetting> PriceSettings { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<StripeWebhookEvent> StripeWebhookEvents { get; set; }

    public virtual DbSet<TagMgr> TagMgrs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserArchiveBoughtLog> UserArchiveBoughtLogs { get; set; }

    public virtual DbSet<Writer> Writers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Archive>(entity =>
        {
            entity.HasKey(e => e.ArchiveId).HasName("PRIMARY");

            entity.ToTable("archives");

            entity.HasIndex(e => e.ArchiveUuid, "archive_uuid_UNIQUE").IsUnique();

            entity.HasIndex(e => e.PriceSettingsId, "fk_archives_price_settings1_idx");

            entity.HasIndex(e => e.WriterId, "fk_archives_writers1_idx");

            entity.Property(e => e.ArchiveId).HasColumnName("archive_id");
            entity.Property(e => e.ArchiveUuid)
                .HasMaxLength(256)
                .HasColumnName("archive_uuid");
            entity.Property(e => e.BodyReference)
                .HasMaxLength(256)
                .HasColumnName("body_reference");
            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime")
                .HasColumnName("create_at");
            entity.Property(e => e.DeleteFlag).HasColumnName("delete_flag");
            entity.Property(e => e.InputCompleted).HasColumnName("input_completed");
            entity.Property(e => e.NumberOfViews).HasColumnName("number_of_views");
            entity.Property(e => e.PriceSettingsId).HasColumnName("price_settings_id");
            entity.Property(e => e.PublishAt)
                .HasColumnType("datetime")
                .HasColumnName("publish_at");
            entity.Property(e => e.SalesPrice).HasColumnName("sales_price");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Subtitle)
                .HasMaxLength(500)
                .HasColumnName("subtitle");
            entity.Property(e => e.ThumbnailPath)
                .HasMaxLength(256)
                .HasColumnName("thumbnail_path");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasColumnName("update_at");
            entity.Property(e => e.WriterId).HasColumnName("writer_id");

            entity.HasOne(d => d.PriceSettings).WithMany(p => p.Archives)
                .HasForeignKey(d => d.PriceSettingsId)
                .HasConstraintName("fk_archives_price_settings1");

            entity.HasOne(d => d.Writer).WithMany(p => p.Archives)
                .HasForeignKey(d => d.WriterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_archives_writers1");

            entity.HasMany(d => d.Tags).WithMany(p => p.Archives)
                .UsingEntity<Dictionary<string, object>>(
                    "ArchiveTag",
                    r => r.HasOne<TagMgr>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_archive_tags_tag_mgr1"),
                    l => l.HasOne<Archive>().WithMany()
                        .HasForeignKey("ArchivesId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_archive_tags_archives1"),
                    j =>
                    {
                        j.HasKey("ArchivesId", "TagId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("archive_tags");
                        j.HasIndex(new[] { "ArchivesId" }, "fk_archive_tags_archives1_idx");
                        j.HasIndex(new[] { "TagId" }, "fk_archive_tags_tag_mgr1_idx");
                        j.IndexerProperty<uint>("ArchivesId").HasColumnName("archives_id");
                        j.IndexerProperty<uint>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<ArchiveHistory>(entity =>
        {
            entity.HasKey(e => new { e.HistoryVersion, e.ArchiveId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("archive_history");

            entity.HasIndex(e => e.ArchiveId, "fk_archive_history_archive_idx");

            entity.Property(e => e.HistoryVersion).HasColumnName("history_version");
            entity.Property(e => e.ArchiveId).HasColumnName("archive_id");
            entity.Property(e => e.BodyReference)
                .HasMaxLength(256)
                .HasColumnName("body_reference");
            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasColumnName("update_at");
            entity.Property(e => e.UpdateDescription)
                .HasMaxLength(100)
                .HasColumnName("update_description");
            entity.Property(e => e.UpdateType).HasColumnName("update_type");

            entity.HasOne(d => d.Archive).WithMany(p => p.ArchiveHistories)
                .HasForeignKey(d => d.ArchiveId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_archive_history_archive");
        });

        modelBuilder.Entity<ArchiveReadLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("archive_read_log");

            entity.HasIndex(e => e.ArchiveId, "fk_archive_read_log_archives1_idx");

            entity.HasIndex(e => e.UserId, "fk_archive_read_log_users1_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArchiveId).HasColumnName("archive_id");
            entity.Property(e => e.BaughtFlag).HasColumnName("baught_flag");
            entity.Property(e => e.ReadDate)
                .HasColumnType("datetime")
                .HasColumnName("read_date");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Archive).WithMany(p => p.ArchiveReadLogs)
                .HasForeignKey(d => d.ArchiveId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_archive_read_log_archives1");

            entity.HasOne(d => d.User).WithMany(p => p.ArchiveReadLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_archive_read_log_users1");
        });

        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PRIMARY");

            entity.ToTable("Idempotency_keys");

            entity.HasIndex(e => e.Key, "key_UNIQUE").IsUnique();

            entity.Property(e => e.Key)
                .HasMaxLength(256)
                .HasColumnName("key");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Handler)
                .HasMaxLength(128)
                .HasColumnName("handler");
            entity.Property(e => e.Status)
                .HasComment("Processing=0/Completed=1")
                .HasColumnName("status");
        });

        modelBuilder.Entity<Meister>(entity =>
        {
            entity.HasKey(e => e.MeisterId).HasName("PRIMARY");

            entity.ToTable("meister");

            entity.Property(e => e.MeisterId).HasColumnName("meister_id");
            entity.Property(e => e.Description)
                .HasMaxLength(512)
                .HasColumnName("description");
            entity.Property(e => e.MeisterTitle)
                .HasMaxLength(64)
                .HasColumnName("meister_title");
            entity.Property(e => e.RegistereUserId).HasColumnName("registere_user_id");
            entity.Property(e => e.Status).HasColumnName("status");
        });

        modelBuilder.Entity<PendingRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("pending_registrations");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.ExpireAt).HasColumnType("datetime");
            entity.Property(e => e.IdentityUserId)
                .HasMaxLength(191)
                .HasColumnName("identity_user_id");
            entity.Property(e => e.Token).HasMaxLength(128);
        });

        modelBuilder.Entity<PriceSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("price_settings");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MaxYen).HasColumnName("max_yen");
            entity.Property(e => e.MinYen).HasColumnName("min_yen");
            entity.Property(e => e.PaymentFeePercent).HasColumnName("payment_fee_percent");
            entity.Property(e => e.PaymentFeeYen).HasColumnName("payment_fee_yen");
            entity.Property(e => e.SaleFeeParcent).HasColumnName("sale_fee_parcent");
            entity.Property(e => e.SaleFeeYen).HasColumnName("sale_fee_yen");
            entity.Property(e => e.SaleFeeYenMin).HasColumnName("sale_fee_yen_min");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PRIMARY");

            entity.ToTable("request");

            entity.HasIndex(e => e.UserId, "fk_request_users1_idx");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime")
                .HasColumnName("create_at");
            entity.Property(e => e.DeleteFlag).HasColumnName("delete_flag");
            entity.Property(e => e.DesiredMeisterId)
                .HasComment("希望の専門")
                .HasColumnName("desired_meister_id");
            entity.Property(e => e.RequestBody)
                .HasMaxLength(3000)
                .HasColumnName("request_body");
            entity.Property(e => e.RequestTitle)
                .HasMaxLength(512)
                .HasColumnName("request_title");
            entity.Property(e => e.RequestUuid)
                .HasMaxLength(64)
                .HasColumnName("request_uuid");
            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasColumnName("update_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Requests)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_request_users1");

            entity.HasMany(d => d.ArchivesArchives).WithMany(p => p.RequestRequests)
                .UsingEntity<Dictionary<string, object>>(
                    "RequestReferenceArchive",
                    r => r.HasOne<Archive>().WithMany()
                        .HasForeignKey("ArchivesArchiveId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_request_reference_archive_archives1"),
                    l => l.HasOne<Request>().WithMany()
                        .HasForeignKey("RequestRequestId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_request_reference_archive_request1"),
                    j =>
                    {
                        j.HasKey("RequestRequestId", "ArchivesArchiveId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("request_reference_archive");
                        j.HasIndex(new[] { "ArchivesArchiveId" }, "fk_request_reference_archive_archives1_idx");
                        j.HasIndex(new[] { "RequestRequestId" }, "fk_request_reference_archive_request1_idx");
                        j.IndexerProperty<uint>("RequestRequestId").HasColumnName("request_request_id");
                        j.IndexerProperty<uint>("ArchivesArchiveId").HasColumnName("archives_archive_id");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Requests)
                .UsingEntity<Dictionary<string, object>>(
                    "RequestTag",
                    r => r.HasOne<TagMgr>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_request_tag_tag_mgr1"),
                    l => l.HasOne<Request>().WithMany()
                        .HasForeignKey("RequestId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_request_tag_request1"),
                    j =>
                    {
                        j.HasKey("RequestId", "TagId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("request_tag");
                        j.HasIndex(new[] { "RequestId" }, "fk_request_tag_request1_idx");
                        j.HasIndex(new[] { "TagId" }, "fk_request_tag_tag_mgr1_idx");
                        j.IndexerProperty<uint>("RequestId").HasColumnName("request_id");
                        j.IndexerProperty<uint>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<StripeWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.StripeEventId).HasName("PRIMARY");

            entity.ToTable("stripe_webhook_events");

            entity.Property(e => e.StripeEventId).HasColumnName("stripe_event_id");
            entity.Property(e => e.ProcessedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("processed_at");
        });

        modelBuilder.Entity<TagMgr>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PRIMARY");

            entity.ToTable("tag_mgr");

            entity.Property(e => e.TagId).HasColumnName("tag_id");
            entity.Property(e => e.TagString)
                .HasMaxLength(45)
                .HasColumnName("tag_string");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("users");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Birth)
                .HasColumnType("datetime")
                .HasColumnName("birth");
            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime")
                .HasColumnName("create_at");
            entity.Property(e => e.DeleteFlag).HasColumnName("delete_flag");
            entity.Property(e => e.ExternalCustmerId)
                .HasMaxLength(512)
                .HasColumnName("external_custmer_id");
            entity.Property(e => e.ExternalPaymentId)
                .HasMaxLength(512)
                .HasColumnName("external_payment_id");
            entity.Property(e => e.Gender).HasColumnName("gender");
            entity.Property(e => e.IdentityUserId)
                .HasMaxLength(191)
                .HasColumnName("identity_user_id");
            entity.Property(e => e.SosialLoginId)
                .HasMaxLength(256)
                .HasColumnName("sosial_login_id");
            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasColumnName("update_at");
        });

        modelBuilder.Entity<UserArchiveBoughtLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("user_archive_bought_log");

            entity.HasIndex(e => e.ArchiveId, "fk_user_archive_bought_log_archives1_idx");

            entity.HasIndex(e => e.UserId, "fk_user_archive_bought_log_users1_idx");

            entity.HasIndex(e => e.StripeCheckoutSessionId, "uq_uabl_checkout_session").IsUnique();

            entity.HasIndex(e => e.StripePaymentIntentId, "uq_uabl_payment_intent").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArchiveId).HasColumnName("archive_id");
            entity.Property(e => e.BaughtDate)
                .HasColumnType("datetime")
                .HasColumnName("baught_date");
            entity.Property(e => e.PaymentConfirmedAt)
                .HasColumnType("datetime")
                .HasColumnName("payment_confirmed_at");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StripeCheckoutSessionId).HasColumnName("stripe_checkout_session_id");
            entity.Property(e => e.StripePaymentIntentId).HasColumnName("stripe_payment_intent_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WriterRevenue)
                .HasComment("ライターの収益")
                .HasColumnName("writer_revenue");

            entity.HasOne(d => d.Archive).WithMany(p => p.UserArchiveBoughtLogs)
                .HasForeignKey(d => d.ArchiveId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user_archive_bought_log_archives1");

            entity.HasOne(d => d.User).WithMany(p => p.UserArchiveBoughtLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user_archive_bought_log_users1");
        });

        modelBuilder.Entity<Writer>(entity =>
        {
            entity.HasKey(e => e.WriterId).HasName("PRIMARY");

            entity.ToTable("writers");

            entity.HasIndex(e => e.MeisterId, "fk_writers_meister1_idx");

            entity.HasIndex(e => e.UserId, "fk_writers_users1_idx");

            entity.Property(e => e.WriterId).HasColumnName("writer_id");
            entity.Property(e => e.CreateAt)
                .HasColumnType("datetime")
                .HasColumnName("create_at");
            entity.Property(e => e.DeleteFlag).HasColumnName("delete_flag");
            entity.Property(e => e.IconFilePath)
                .HasMaxLength(512)
                .HasColumnName("icon_file_path");
            entity.Property(e => e.Link)
                .HasMaxLength(1024)
                .HasColumnName("link");
            entity.Property(e => e.MeisterId).HasColumnName("meister_id");
            entity.Property(e => e.SelfIntroduction)
                .HasMaxLength(1100)
                .HasColumnName("self_Introduction");
            entity.Property(e => e.UpdateAt)
                .HasColumnType("datetime")
                .HasColumnName("update_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WriterEnUnique).HasColumnName("writer_en_unique");
            entity.Property(e => e.WriterName)
                .HasMaxLength(64)
                .HasColumnName("writer_name");
            entity.Property(e => e.WriterNameEn)
                .HasMaxLength(45)
                .HasColumnName("writer_name_en");
            entity.Property(e => e.WriterUnique).HasColumnName("writer_unique");

            entity.HasOne(d => d.Meister).WithMany(p => p.Writers)
                .HasForeignKey(d => d.MeisterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_writers_meister1");

            entity.HasOne(d => d.User).WithMany(p => p.Writers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_writers_users1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
