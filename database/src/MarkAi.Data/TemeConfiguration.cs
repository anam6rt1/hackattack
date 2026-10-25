using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MarkAi.Data;

internal static class TemeConfiguration
{
    internal static void Configure(ModelBuilder m)
    {
        var g = m.Entity<GrupaElevi>();
        g.ToTable("grupa_elevi", "mark_ai"); g.HasKey(x => x.Id);
        g.Property(x => x.Id).UseIdentityAlwaysColumn();
        g.Property(x => x.Nume).HasMaxLength(100);
        g.HasOne(x => x.Organizatie).WithMany().HasForeignKey(x => x.OrganizatieId).OnDelete(DeleteBehavior.Cascade);
        g.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.ProfesorId).OnDelete(DeleteBehavior.SetNull);
        g.HasIndex(x => new { x.OrganizatieId, x.ProfesorId });
        var gm = m.Entity<GrupaMembru>();
        gm.ToTable("grupa_membru", "mark_ai"); gm.HasKey(x => new { x.GrupaId, x.ElevId });
        gm.HasOne(x => x.Grupa).WithMany(x => x.Membri).HasForeignKey(x => x.GrupaId).OnDelete(DeleteBehavior.Cascade);
        gm.HasOne(x => x.Elev).WithMany().HasForeignKey(x => x.ElevId).OnDelete(DeleteBehavior.Cascade);
        var t = m.Entity<Tema>();
        t.ToTable("tema", "mark_ai", b => b.HasCheckConstraint("tema_termen_check", "termen_la > creat_la"));
        t.HasKey(x => x.Id); t.Property(x => x.Id).UseIdentityAlwaysColumn();
        t.Property(x => x.Titlu).HasMaxLength(200); t.Property(x => x.Descriere).HasMaxLength(2000);
        t.HasOne(x => x.Organizatie).WithMany().HasForeignKey(x => x.OrganizatieId).OnDelete(DeleteBehavior.Cascade);
        t.HasOne(x => x.Profesor).WithMany().HasForeignKey(x => x.ProfesorId).OnDelete(DeleteBehavior.SetNull);
        t.HasIndex(x => new { x.OrganizatieId, x.TermenLa });
        var tl = m.Entity<TemaLectie>(); tl.ToTable("tema_lectie", "mark_ai"); tl.HasKey(x => new { x.TemaId, x.ParcursId });
        tl.HasOne(x => x.Tema).WithMany(x => x.Lectii).HasForeignKey(x => x.TemaId).OnDelete(DeleteBehavior.Cascade);
        tl.HasOne(x => x.Parcurs).WithMany().HasForeignKey(x => x.ParcursId).OnDelete(DeleteBehavior.Cascade);
        var tg = m.Entity<TemaGrupa>(); tg.ToTable("tema_grupa", "mark_ai"); tg.HasKey(x => new { x.TemaId, x.GrupaId });
        tg.HasOne(x => x.Tema).WithMany(x => x.Grupe).HasForeignKey(x => x.TemaId).OnDelete(DeleteBehavior.Cascade);
        tg.HasOne(x => x.Grupa).WithMany().HasForeignKey(x => x.GrupaId).OnDelete(DeleteBehavior.Cascade);
        var e = m.Entity<Greseala>();
        e.ToTable("greseala", "mark_ai", b => b.HasCheckConstraint("greseala_nivel_check", "nivel BETWEEN 0 AND 3 AND corecte_consecutive BETWEEN 0 AND 2"));
        e.HasKey(x => new { x.UtilizatorId, x.IntrebareId });
        e.HasOne<Utilizator>().WithMany().HasForeignKey(x => x.UtilizatorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Intrebare).WithMany().HasForeignKey(x => x.IntrebareId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.UtilizatorId, x.ScadentaLa });
        foreach (var tip in new[] { typeof(GrupaElevi), typeof(GrupaMembru), typeof(Tema), typeof(TemaLectie), typeof(TemaGrupa), typeof(Greseala) })
            foreach (var p in m.Entity(tip).Metadata.GetProperties())
                p.SetColumnName(Regex.Replace(p.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
