using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DigitalStampRally.Database;

public partial class DigitalStampRallyContext : DbContext
{
    public DigitalStampRallyContext(DbContextOptions<DigitalStampRallyContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventReward> EventRewards { get; set; }

    public virtual DbSet<EventSpot> EventSpots { get; set; }

    public virtual DbSet<Goal> Goals { get; set; }

    public virtual DbSet<ParticipantSession> ParticipantSessions { get; set; }

    public virtual DbSet<Stamp> Stamps { get; set; }

    public virtual DbSet<StampScanLog> StampScanLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("events");

            entity.HasIndex(e => e.EndsAt, "idx_events_ends");

            entity.HasIndex(e => new { e.Status, e.StartsAt }, "idx_events_status_starts");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EndsAt)
                .HasColumnType("datetime")
                .HasColumnName("ends_at");
            entity.Property(e => e.GoalTokenHash)
                .HasMaxLength(64)
                .HasColumnName("goal_token_hash");
            entity.Property(e => e.RequiredStampCount)
                .HasColumnType("int(11)")
                .HasColumnName("required_stamp_count");
            entity.Property(e => e.StartsAt)
                .HasColumnType("datetime")
                .HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasComment("0:下書き 1:公開 2:終了 3:停止")
                .HasColumnType("int(11)")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.TotalizePasswordHash)
                .HasMaxLength(64)
                .HasColumnName("totalize_password_hash");
            entity.Property(e => e.TotalizeTokenHash)
                .HasMaxLength(64)
                .HasColumnName("totalize_token_hash");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<EventReward>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.EventsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("event_rewards", tb => tb.HasComment("景品/達成条件"));

            entity.HasIndex(e => e.EventsId, "fk_event_rewards_events1_idx");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(1024)
                .HasColumnName("description");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.RequiredStampCount)
                .HasComment("typeに応じて")
                .HasColumnType("int(11)")
                .HasColumnName("required_stamp_count");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasComment("0:スタンプ数達成 1:必須スポット 2:両方")
                .HasColumnType("int(11)")
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Events).WithMany(p => p.EventRewards)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_event_rewards_events1");

            entity.HasMany(d => d.EventSpots).WithMany(p => p.EventRewards)
                .UsingEntity<Dictionary<string, object>>(
                    "RewardRequiredSpot",
                    r => r.HasOne<EventSpot>().WithMany()
                        .HasForeignKey("EventSpotsId", "EventSpotsEventsId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_reward_required_spots_event_spots1"),
                    l => l.HasOne<EventReward>().WithMany()
                        .HasForeignKey("EventRewardsId", "EventRewardsEventsId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_reward_required_spots_event_rewards1"),
                    j =>
                    {
                        j.HasKey("EventRewardsId", "EventRewardsEventsId", "EventSpotsId", "EventSpotsEventsId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0 });
                        j.ToTable("reward_required_spots");
                        j.HasIndex(new[] { "EventSpotsId", "EventSpotsEventsId" }, "fk_reward_required_spots_event_spots1_idx");
                        j.HasIndex(new[] { "EventRewardsId", "EventSpotsId" }, "idx_reward_required_spots_event_rewards_event_spots");
                        j.IndexerProperty<long>("EventRewardsId")
                            .HasColumnType("bigint(12)")
                            .HasColumnName("event_rewards_id");
                        j.IndexerProperty<long>("EventRewardsEventsId")
                            .HasColumnType("bigint(12)")
                            .HasColumnName("event_rewards_events_id");
                        j.IndexerProperty<long>("EventSpotsId")
                            .HasColumnType("bigint(12)")
                            .HasColumnName("event_spots_id");
                        j.IndexerProperty<long>("EventSpotsEventsId")
                            .HasColumnType("bigint(12)")
                            .HasColumnName("event_spots_events_id");
                    });
        });

        modelBuilder.Entity<EventSpot>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.EventsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("event_spots", tb => tb.HasComment("QRが貼られている地点＝1レコード"));

            entity.HasIndex(e => e.EventsId, "fk_event_spots_events_idx");

            entity.HasIndex(e => new { e.EventsId, e.IsActive }, "idx_events_spots_events_is_active");

            entity.HasIndex(e => new { e.EventsId, e.SortOrder }, "idx_events_spots_events_sort");

            entity.HasIndex(e => e.QrTokenHash, "idx_events_spots_qr_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(1024)
                .HasColumnName("description");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.QrTokenHash)
                .HasMaxLength(64)
                .HasColumnName("qr_token_hash");
            entity.Property(e => e.SortOrder)
                .HasComment("表示順")
                .HasColumnType("int(11)")
                .HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Events).WithMany(p => p.EventSpots)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_event_spots_events");
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("goals");

            entity.HasIndex(e => new { e.ParticipantSessionsId, e.EventsId }, "fk_goals_sessions").IsUnique();

            entity.HasIndex(e => new { e.EventsId, e.GoaledAt }, "idx_goals_event_goaled_at");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.AchievementCode)
                .HasMaxLength(8)
                .IsFixedLength()
                .HasColumnName("achievement_code");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.GoaledAt)
                .HasColumnType("datetime")
                .HasColumnName("goaled_at");
            entity.Property(e => e.ParticipantSessionsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("participant_sessions_id");

            entity.HasOne(d => d.Events).WithMany(p => p.Goals)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_goals_events");

            entity.HasOne(d => d.ParticipantSession).WithOne(p => p.Goal)
                .HasForeignKey<Goal>(d => new { d.ParticipantSessionsId, d.EventsId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_goals_sessions");
        });

        modelBuilder.Entity<ParticipantSession>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.EventsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("participant_sessions", tb => tb.HasComment("ログインなし参加者の“台帳”"));

            entity.HasIndex(e => e.EventsId, "fk_participant_sessions_events1_idx");

            entity.HasIndex(e => new { e.EventsId, e.LastSeenAt }, "idx_participant_sessions_events_last_seen_at");

            entity.HasIndex(e => new { e.EventsId, e.SessionKey }, "idx_participant_sessions_events_session_key");

            entity.HasIndex(e => e.SessionKey, "session_key_UNIQUE").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.FirstSeenAt)
                .HasComment("参加者が最初にこのイベントに来た時刻")
                .HasColumnType("datetime")
                .HasColumnName("first_seen_at");
            entity.Property(e => e.IpHash)
                .HasMaxLength(64)
                .HasColumnName("ip_hash");
            entity.Property(e => e.IsBlocked).HasColumnName("is_blocked");
            entity.Property(e => e.LastSeenAt)
                .HasComment("最後に何か操作した時刻")
                .HasColumnType("datetime")
                .HasColumnName("last_seen_at");
            entity.Property(e => e.SessionKey)
                .HasMaxLength(64)
                .HasComment("UUID等（端末に保存）")
                .HasColumnName("session_key");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(300)
                .HasColumnName("user_agent");

            entity.HasOne(d => d.Events).WithMany(p => p.ParticipantSessions)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_participant_sessions_events1");
        });

        modelBuilder.Entity<Stamp>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.EventsId, e.EventSpotsId, e.EventSpotsEventsId, e.ParticipantSessionsId, e.ParticipantSessionsEventsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0, 0, 0 });

            entity.ToTable("stamps", tb => tb.HasComment("「この参加者はこのスポットのスタンプを持っている」を一意にするテーブル。"));

            entity.HasIndex(e => new { e.EventSpotsId, e.EventSpotsEventsId }, "fk_stamps_event_spots1_idx");

            entity.HasIndex(e => e.EventsId, "fk_stamps_events1_idx");

            entity.HasIndex(e => new { e.ParticipantSessionsId, e.ParticipantSessionsEventsId }, "fk_stamps_participant_sessions1_idx");

            entity.HasIndex(e => new { e.EventsId, e.EventSpotsId, e.ParticipantSessionsId }, "idx_stamps_events_event_spots_participant_sessions").IsUnique();

            entity.HasIndex(e => new { e.ParticipantSessionsId, e.StampedAt }, "idx_stamps_participant_sessions_stamped_at");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.EventSpotsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("event_spots_id");
            entity.Property(e => e.EventSpotsEventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("event_spots_events_id");
            entity.Property(e => e.ParticipantSessionsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("participant_sessions_id");
            entity.Property(e => e.ParticipantSessionsEventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("participant_sessions_events_id");
            entity.Property(e => e.StampedAt)
                .HasComment("初回押印時刻")
                .HasColumnType("datetime")
                .HasColumnName("stamped_at");

            entity.HasOne(d => d.Events).WithMany(p => p.Stamps)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamps_events1");

            entity.HasOne(d => d.EventSpot).WithMany(p => p.Stamps)
                .HasForeignKey(d => new { d.EventSpotsId, d.EventSpotsEventsId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamps_event_spots1");

            entity.HasOne(d => d.ParticipantSession).WithMany(p => p.Stamps)
                .HasForeignKey(d => new { d.ParticipantSessionsId, d.ParticipantSessionsEventsId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamps_participant_sessions1");
        });

        modelBuilder.Entity<StampScanLog>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.EventsId, e.EventSpotsId, e.EventSpotsEventsId, e.ParticipantSessionsId, e.ParticipantSessionsEventsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0, 0, 0 });

            entity.ToTable("stamp_scan_logs", tb => tb.HasComment("「読み取った」事実を残す。押印済みでもログは残してOK。"));

            entity.HasIndex(e => new { e.EventSpotsId, e.EventSpotsEventsId }, "fk_stamp_scans_event_spots1_idx");

            entity.HasIndex(e => e.EventsId, "fk_stamp_scans_events1_idx");

            entity.HasIndex(e => new { e.ParticipantSessionsId, e.ParticipantSessionsEventsId }, "fk_stamp_scans_participant_sessions1_idx");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(12)")
                .HasColumnName("id");
            entity.Property(e => e.EventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("events_id");
            entity.Property(e => e.EventSpotsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("event_spots_id");
            entity.Property(e => e.EventSpotsEventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("event_spots_events_id");
            entity.Property(e => e.ParticipantSessionsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("participant_sessions_id");
            entity.Property(e => e.ParticipantSessionsEventsId)
                .HasColumnType("bigint(12)")
                .HasColumnName("participant_sessions_events_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.RawTokenHash)
                .HasMaxLength(64)
                .HasColumnName("raw_token_hash");
            entity.Property(e => e.Result)
                .HasComment("0:成功 1:重複 2:無効QR 3:停止中 4:期限外…")
                .HasColumnType("int(11)")
                .HasColumnName("result");
            entity.Property(e => e.ScannedAt)
                .HasComment("読み取り時刻")
                .HasColumnType("datetime")
                .HasColumnName("scanned_at");

            entity.HasOne(d => d.Events).WithMany(p => p.StampScanLogs)
                .HasForeignKey(d => d.EventsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamp_scans_events1");

            entity.HasOne(d => d.EventSpot).WithMany(p => p.StampScanLogs)
                .HasForeignKey(d => new { d.EventSpotsId, d.EventSpotsEventsId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamp_scans_event_spots1");

            entity.HasOne(d => d.ParticipantSession).WithMany(p => p.StampScanLogs)
                .HasForeignKey(d => new { d.ParticipantSessionsId, d.ParticipantSessionsEventsId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stamp_scans_participant_sessions1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
