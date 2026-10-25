using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MarkAi.Data;
internal static class RapoarteConfiguration
{
    internal static void Configure(ModelBuilder m)
    {
        var i = m.Entity<InvitatieElev>(); i.ToTable("invitatie_elev", "mark_ai"); i.HasKey(x => x.Id);
        i.Property(x => x.Id).UseIdentityAlwaysColumn(); i.HasIndex(x => x.TokenHash).IsUnique();
        i.HasIndex(x => new { x.OrganizatieId, x.Email });
        i.Property(x => x.Email).HasMaxLength(200); i.Property(x => x.Nume).HasMaxLength(120);
        i.HasOne<Organizatie>().WithMany().HasForeignKey(x => x.OrganizatieId).OnDelete(DeleteBehavior.Cascade);
        var p = m.Entity<SumarParinte>(); p.ToTable("sumar_parinte", "mark_ai"); p.HasKey(x => x.UtilizatorId);
        p.HasIndex(x => x.TokenHash).IsUnique(); p.HasIndex(x => x.OprireHash).IsUnique();
        p.Property(x => x.Email).HasMaxLength(200);
        p.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        var r = m.Entity<RaspunsStatistica>(); r.ToTable("raspuns_statistica", "mark_ai"); r.HasKey(x => new { x.IncercareId, x.IntrebareId });
        r.HasIndex(x => new { x.IntrebareId, x.RaspunsLa }); r.HasIndex(x => new { x.UtilizatorId, x.RaspunsLa });
        r.HasOne<IncercareQuiz>().WithMany().HasForeignKey(x => x.IncercareId).OnDelete(DeleteBehavior.Cascade);
        r.HasOne<Intrebare>().WithMany().HasForeignKey(x => x.IntrebareId).OnDelete(DeleteBehavior.Cascade);
        r.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        var e = m.Entity<EmailSumar>(); e.ToTable("email_sumar", "mark_ai"); e.HasKey(x => x.MesajId);
        e.HasOne(x => x.Mesaj).WithMany().HasForeignKey(x => x.MesajId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.UtilizatorId);
        var f = m.Entity<FeedbackPilot>(); f.ToTable("feedback_pilot", "mark_ai"); f.HasKey(x => x.Id); f.Property(x => x.Id).UseIdentityAlwaysColumn();
        f.Property(x => x.Text).HasMaxLength(2000); f.Property(x => x.Pagina).HasMaxLength(200); f.HasIndex(x => new { x.UtilizatorId, x.CreatLa });
        f.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.SetNull);
        foreach (var tip in new[] { typeof(InvitatieElev), typeof(SumarParinte), typeof(RaspunsStatistica), typeof(EmailSumar), typeof(FeedbackPilot) })
            foreach (var prop in m.Entity(tip).Metadata.GetProperties()) prop.SetColumnName(Regex.Replace(prop.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
