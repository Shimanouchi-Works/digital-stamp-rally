using Microsoft.EntityFrameworkCore;

namespace DigitalStampRally.Database;

public partial class DigitalStampRallyContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequiredSpotRow>(entity =>
        {
            entity.HasNoKey();
            // 実体テーブルに紐づかない（FromSql専用）
            entity.ToView(null);
            entity.Property(e => e.EventSpotsId).HasColumnName("event_spots_id");
        });
    }
}
