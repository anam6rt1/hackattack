using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data;
internal static class OfflineConfiguration
{
    internal static void Configure(ModelBuilder m)
    {
        var o = m.Entity<OperatieSincronizata>(); o.ToTable("operatie_sincronizata", "mark_ai");
        o.HasKey(x => new { x.UtilizatorId, x.IdClient });
        o.Property(x => x.UtilizatorId).HasColumnName("utilizator_id"); o.Property(x => x.IdClient).HasColumnName("id_client");
        o.Property(x => x.Tip).HasColumnName("tip").HasMaxLength(40); o.Property(x => x.AplicataLa).HasColumnName("aplicata_la");
        o.Property(x => x.RezultatJson).HasColumnName("rezultat_json").HasColumnType("jsonb");
        o.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
    }
}
