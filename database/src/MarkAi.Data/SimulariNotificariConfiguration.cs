using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MarkAi.Data;
internal static class SimulariNotificariConfiguration
{
    internal static void Configure(ModelBuilder m)
    {
        var s = m.Entity<Simulare>(); s.ToTable("simulare", "mark_ai", t => {
            t.HasCheckConstraint("simulare_limite_check", "total BETWEEN 1 AND 50 AND corecte BETWEEN 0 AND total AND expira_la > inceput_la");
            t.HasCheckConstraint("simulare_tip_check", "tip IN ('bac','evaluare_nationala')");
        });
        s.HasKey(x => x.Id); s.Property(x => x.Id).UseIdentityAlwaysColumn(); s.Property(x => x.IntrebariJson).HasColumnType("jsonb");
        s.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        s.HasOne<Materie>().WithMany().HasForeignKey(x => x.MaterieId).OnDelete(DeleteBehavior.Restrict);
        s.HasIndex(x => new { x.UtilizatorId, x.InceputLa });
        var p = m.Entity<PreferinteNotificari>(); p.ToTable("preferinte_notificari", "mark_ai"); p.HasKey(x => x.UtilizatorId);
        p.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        var a = m.Entity<AbonamentPush>(); a.ToTable("abonament_push", "mark_ai"); a.HasKey(x => x.Id); a.Property(x => x.Id).UseIdentityAlwaysColumn();
        a.Property(x => x.Endpoint).HasMaxLength(2048); a.HasIndex(x => x.Endpoint).IsUnique();
        a.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        var n = m.Entity<Notificare>(); n.ToTable("notificare", "mark_ai"); n.HasKey(x => x.Id); n.Property(x => x.Id).UseIdentityAlwaysColumn();
        n.Property(x => x.Cheie).HasMaxLength(180); n.HasIndex(x => new { x.UtilizatorId, x.Cheie }).IsUnique();
        n.HasIndex(x => new { x.UtilizatorId, x.Tip, x.ResursaId }); n.HasIndex(x => new { x.FinalizatLa, x.UrmatoareaIncercareLa });
        n.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        n.HasOne(x => x.MesajEmail).WithMany().HasForeignKey(x => x.MesajEmailId).OnDelete(DeleteBehavior.SetNull);
        foreach (var tip in new[] { typeof(Simulare), typeof(PreferinteNotificari), typeof(AbonamentPush), typeof(Notificare) })
            foreach (var prop in m.Entity(tip).Metadata.GetProperties()) prop.SetColumnName(Regex.Replace(prop.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
