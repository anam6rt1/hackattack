using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Endpoints;
public record CsvElevi(string Csv);
public record ConfirmaImport(string Previzualizare);
public record AcceptaInvitatie(string Token, string? Username = null, string? Parola = null, bool? Sub16Ani = null, bool AcordParinteDeclarat = false);
public record ImportPregatit(int Uid, int Org, List<RandInvitatie> Randuri);
public static class InvitatiiEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("/organizatii/{org:int}/invitatii").RequireAuthorization(Politici.ProfesorAprobat);
        g.MapPost("/previzualizare", async (int org, CsvElevi c, HttpContext h, MarkAiDbContext db, IMemoryCache cache, CancellationToken ct) =>
        {
            await RapoarteEndpoints.Admin(db, org, h.User.Id(), ct);
            var randuri = Invitatii.Citeste(c.Csv); var token = Invitatii.Token();
            if (randuri.All(r => r.Eroare == null)) cache.Set("import:" + token, new ImportPregatit(h.User.Id(), org, randuri), TimeSpan.FromMinutes(15));
            h.Response.Headers.CacheControl = "no-store";
            return (object)new { Randuri = randuri, Previzualizare = randuri.All(r => r.Eroare == null) ? token : null };
        });
        g.MapPost("/trimite", async (int org, ConfirmaImport c, HttpContext h, MarkAiDbContext db, IMemoryCache cache, Posta posta, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            await RapoarteEndpoints.Admin(db, org, h.User.Id(), ct);
            if (c.Previzualizare is null || !cache.TryGetValue<ImportPregatit>("import:" + c.Previzualizare, out var p) || p!.Uid != h.User.Id() || p.Org != org)
                throw new CerereInvalida("Verifică din nou fișierul înainte de trimitere.");
            var now = ceas.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Invitatii.Buget(db, cfg, now, p.Randuri.Count, ct);
            var trimise = 0;
            foreach (var r in p.Randuri)
            {
                if (await db.Set<InvitatieElev>().AnyAsync(i => i.OrganizatieId == org && i.Email == r.Email && i.ExpiraLa > now && i.AcceptataLa == null, ct)) continue;
                var token = Invitatii.Token();
                db.Add(new InvitatieElev { OrganizatieId = org, Nume = r.Nume, Email = r.Email, Clasa = r.Clasa, TokenHash = Invitatii.Hash(token), CreatLa = now, ExpiraLa = now.AddDays(7) });
                posta.Pune(Invitatii.Mesaj(r.Email, "TeleBac · Invitație pentru elev", "Ai fost invitat în organizație. Poți accepta invitația în următoarele 7 zile. Dacă nu ai cont, alegi o parolă înainte de acceptare.", Invitatii.Url(cfg, "/invitatie", token)), "invitatie_elev");
                trimise++;
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); cache.Remove("import:" + c.Previzualizare);
            return (object)new { Trimise = trimise, Omise = p.Randuri.Count - trimise };
        });
        // Tokens stay in the fragment in browser links and in POST bodies, outside access logs.
        app.MapPost("/invitatii/detalii", async (AcceptaInvitatie c, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            var hash = Invitatii.Hash(c.Token); var now = ceas.GetUtcNow().UtcDateTime;
            var i = await db.Set<InvitatieElev>().SingleOrDefaultAsync(i => i.TokenHash == hash && i.ExpiraLa > now && i.AcceptataLa == null, ct) ?? throw new KeyNotFoundException("Invitația a expirat sau a fost folosită.");
            var org = await db.Set<Organizatie>().SingleAsync(o => o.Id == i.OrganizatieId && o.Stare == Organizatie.Aprobata, ct);
            return (object)new { i.Nume, i.Email, i.Clasa, Organizatie = org.Nume };
        }).RequireRateLimiting(LimiteCereri.Inregistrare);
        app.MapPost("/invitatii/accepta", async (AcceptaInvitatie c, HttpContext h, MarkAiDbContext db, IConfiguration cfg, TimeProvider ceas, CancellationToken ct) =>
        {
            var hash = Invitatii.Hash(c.Token); var now = ceas.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7731025)", ct);
            var i = await db.Set<InvitatieElev>().SingleOrDefaultAsync(i => i.TokenHash == hash && i.ExpiraLa > now && i.AcceptataLa == null, ct) ?? throw new KeyNotFoundException("Invitația a expirat sau a fost folosită.");
            if (!await db.Set<Organizatie>().AnyAsync(o => o.Id == i.OrganizatieId && o.Stare == Organizatie.Aprobata, ct)) throw new Conflict("Organizația nu mai este activă.");
            var u = await db.Utilizatori.Include(u => u.Roluri).ThenInclude(r => r.Rol).SingleOrDefaultAsync(u => u.Email.ToLower() == i.Email, ct);
            if (u != null)
            {
                if (h.User.Identity?.IsAuthenticated != true || h.User.Id() != u.Id || u.EmailConfirmatLa == null || u.StergereCerutaLa != null || !u.Roluri.Any(r => r.Rol.Nume == Roluri.Elev))
                    throw new Conflict("Intră în contul de elev asociat acestei adrese pentru a accepta invitația.");
            }
            else
            {
                var username = Validare.Obligatoriu(c.Username, "Utilizator", 40);
                if (!username.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')) throw new CerereInvalida("Numele de utilizator nu este valid.");
                if (await db.Utilizatori.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct)) throw new Conflict("Numele de utilizator este deja folosit.");
                if (c.Parola is null || c.Parola.Length is < 8 or > 1024) throw new CerereInvalida("Alege o parolă între 8 și 1024 de caractere.");
                if (cfg.GetValue("Confidentialitate:SolicitaDeclaratieVarsta", false) && c.Sub16Ani == null || c.Sub16Ani == true && !c.AcordParinteDeclarat) throw new CerereInvalida("Completează declarația de vârstă și acordul părintelui, dacă este necesar.");
                u = new Utilizator { Nume = i.Nume, Username = username, Email = i.Email, Clasa = i.Clasa, CreatLa = now, EmailConfirmatLa = now, Sub16AniDeclarat = c.Sub16Ani, AcordParinteDeclaratLa = c.Sub16Ani == true ? now : null, VersiuneInformare = c.Sub16Ani != null ? DatePersonale.VersiuneInformare : null };
                u.ParolaHash = Parole.Hash(u, c.Parola); u.Roluri.Add(new UtilizatorRol { Rol = await db.Roluri.SingleAsync(r => r.Nume == Roluri.Elev, ct) }); db.Add(u); await db.SaveChangesAsync(ct);
            }
            var membri = db.Set<MembruOrganizatie>();
            if (await membri.AnyAsync(m => m.UtilizatorId == u.Id && m.OrganizatieId != i.OrganizatieId && m.Rol == "elev" && (m.Stare == "aprobat" || m.Stare == "in_asteptare"), ct)) throw new Conflict("Ieși mai întâi din organizația actuală sau retrage cererea existentă.");
            var membru = await membri.SingleOrDefaultAsync(m => m.UtilizatorId == u.Id && m.OrganizatieId == i.OrganizatieId, ct);
            if (membru == null) db.Add(new MembruOrganizatie { OrganizatieId = i.OrganizatieId, UtilizatorId = u.Id, Rol = "elev", Stare = "aprobat", CerutLa = now, DecisLa = now });
            else if (membru.Rol != "elev") throw new Conflict("Contul are deja alt rol în organizație.");
            else { membru.Stare = "aprobat"; membru.ExpiraLa = null; membru.DecisLa = now; }
            i.AcceptataLa = now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(new { Mesaj = "Invitație acceptată. Poți intra în cont." });
        }).RequireRateLimiting(LimiteCereri.Inregistrare);
    }
}
