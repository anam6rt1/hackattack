using System.Security.Cryptography;
using System.Text;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

/// <summary>
/// One-time 6-digit codes kept in the database (they must outlive a restart: an email code is valid for a day).
/// Only a keyed hash is stored; a new code replaces the previous one; after 5 wrong attempts a code stops working.
/// </summary>
public sealed class CoduriVerificare(MarkAiDbContext db, OptiuniJwt jwt, TimeProvider ceas)
{
    public const string ScopEmail = "email", ScopTelefon = "telefon", ScopParola = "parola";
    /// <summary>Destination of a password-reset code: the account itself, whichever channel delivered it.</summary>
    public const string DestinatieCont = "cont";
    public const int MaxIncercari = 5;

    public async Task<string> CreeazaAsync(int utilizatorId, string scop, string destinatie, DateTime expiraLa, CancellationToken ct)
    {
        var acum = ceas.GetUtcNow().UtcDateTime;
        await db.Set<CodVerificare>().Where(c => c.UtilizatorId == utilizatorId && c.Scop == scop && c.FolositLa == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiraLa, acum), ct);
        var cod = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.Add(new CodVerificare { UtilizatorId = utilizatorId, Scop = scop, Destinatie = destinatie, HashCod = Hash(scop, destinatie, cod), CreatLa = acum, ExpiraLa = expiraLa });
        await db.SaveChangesAsync(ct);
        return cod;
    }

    /// <summary>True (and the code is used up) when <paramref name="cod"/> is the current valid code for this destination.</summary>
    public async Task<bool> VerificaAsync(int utilizatorId, string scop, string destinatie, string cod, CancellationToken ct)
    {
        var acum = ceas.GetUtcNow().UtcDateTime;
        var c = await db.Set<CodVerificare>()
            .Where(x => x.UtilizatorId == utilizatorId && x.Scop == scop && x.Destinatie == destinatie && x.FolositLa == null && x.ExpiraLa > acum)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (c is null || c.Incercari >= MaxIncercari) return false;
        var bun = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c.HashCod), Encoding.ASCII.GetBytes(Hash(scop, destinatie, (cod ?? "").Trim())));
        if (bun) c.FolositLa = acum; else c.Incercari++;
        await db.SaveChangesAsync(ct);
        return bun;
    }

    private string Hash(string scop, string destinatie, string cod) =>
        Convert.ToHexString(HMACSHA256.HashData(jwt.Cheie.Key, Encoding.UTF8.GetBytes($"cod:{scop}:{destinatie}:{cod}")));
}

/// <summary>
/// Hard deletion of an account and everything that belongs to it, in one transaction, then its uploaded files.
/// Used for accounts that never became active (unconfirmed email, applications not approved in time), so it refuses
/// accounts with bookings or trivia history instead of deleting other people's data with them.
/// </summary>
public sealed class StergereCont(MarkAiDbContext db, Stocare stocare, Posta posta, ILogger<StergereCont> log)
{
    public async Task<bool> StergeAsync(int utilizatorId, Email? notificare, CancellationToken ct)
    {
        if (await db.Rezervari.AnyAsync(r => r.ElevId == utilizatorId, ct) || await db.CamereTutoring.AnyAsync(c => c.MentorId == utilizatorId, ct)
            || await db.Set<TriviaParticipant>().AnyAsync(p => p.UserId == utilizatorId, ct) || await db.Set<TriviaRoom>().AnyAsync(r => r.HostId == utilizatorId, ct)
            || await db.Set<Organizatie>().AnyAsync(o => o.AdminId == utilizatorId && o.Stare == Organizatie.Aprobata, ct))
        {
            log.LogWarning("Account {Id} not deleted: it has bookings, trivia history or runs an approved organisation", utilizatorId);
            return false;
        }
        var fisiere = await db.ProfiluriMentor.Where(p => p.UtilizatorId == utilizatorId).Select(p => new[] { p.PozaUrl, p.DovadaCalificareUrl }).SingleOrDefaultAsync(ct) ?? [];

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Rows that point at the account without ON DELETE CASCADE, children first. Roles, preferences, progress,
            // quiz attempts and codes cascade; authored lessons and "verified by" are set to NULL by their foreign keys.
            var u = utilizatorId;
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.mesaj WHERE sesiune_id IN (SELECT id FROM mark_ai.sesiune WHERE utilizator_id = {u})", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.sms_log WHERE sesiune_id IN (SELECT id FROM mark_ai.sesiune WHERE utilizator_id = {u})", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.tabla_pas WHERE sesiune_id IN (SELECT id FROM mark_ai.sesiune WHERE utilizator_id = {u})", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.eveniment_recompensa WHERE utilizator_id = {u} OR sesiune_id IN (SELECT id FROM mark_ai.sesiune WHERE utilizator_id = {u})", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.sesiune WHERE utilizator_id = {u}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.utilizator_realizare WHERE utilizator_id = {u}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.graf WHERE owner_id = {u}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.profil_mentor WHERE utilizator_id = {u}", ct);
            // An organisation that was never approved goes with its admin (its member requests cascade).
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.organizatie WHERE admin_id = {u}", ct);
            if (await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.utilizator WHERE id = {u}", ct) == 0) return false;
            if (notificare is not null) posta.Pune(notificare, "cont_sters");
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        foreach (var f in fisiere) stocare.Sterge(f);
        log.LogInformation("Account {Id} deleted", utilizatorId);
        return true;
    }
}

/// <summary>
/// Every few minutes: warns accounts that are about to expire and deletes those that expired.
/// Email confirmation: a warning (with a fresh code) in the last hour, deletion after <c>Conturi:OreConfirmare</c> (24) hours.
/// Applications (professors never approved by an organisation, organisations never approved): a reminder on day 6,
/// deletion after <c>Conturi:ZileAprobare</c> (7) days. Membership requests nobody decided on expire after 7 days
/// (the account stays); the organisation's admin is reminded a day before.
/// </summary>
public sealed class CuratareConturi(IServiceScopeFactory scopes, IConfiguration cfg, TimeProvider ceas, ILogger<CuratareConturi> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Account cleanup failed"); }
            await Task.Delay(TimeSpan.FromMinutes(5), ceas, ct);
        }
    }

    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var coduri = scope.ServiceProvider.GetRequiredService<CoduriVerificare>();
        var posta = scope.ServiceProvider.GetRequiredService<Posta>();
        var stergere = scope.ServiceProvider.GetRequiredService<StergereCont>();
        var acum = ceas.GetUtcNow().UtcDateTime;
        var ore = cfg.GetValue("Conturi:OreConfirmare", 24);

        var deAvertizat = await db.Utilizatori.Where(u => u.EmailConfirmatLa == null && u.AvertizatLa == null && u.CreatLa <= acum.AddHours(1 - ore))
            .ToListAsync(ct);
        foreach (var u in deAvertizat)
        {
            var cod = await coduri.CreeazaAsync(u.Id, CoduriVerificare.ScopEmail, u.Email, u.CreatLa.AddHours(ore), ct);
            u.AvertizatLa = acum;
            posta.Pune(Sabloane.AvertizareStergere(u.Email, u.Nume, cod), "avertizare_stergere");
            await db.SaveChangesAsync(ct);
        }

        var expirate = await db.Utilizatori.Where(u => u.EmailConfirmatLa == null && u.CreatLa <= acum.AddHours(-ore))
            .Select(u => new { u.Id, u.Email, u.Nume }).ToListAsync(ct);
        foreach (var u in expirate)
            await stergere.StergeAsync(u.Id, Sabloane.ContSters(u.Email, u.Nume,
                $"Nu ai confirmat adresa de email în {ore} de ore de la înregistrare, așa că am șters contul și datele lui."), ct);

        await AprobariAsync(db, posta, stergere, acum, ct);
        await CereriAsync(db, posta, scope.ServiceProvider.GetRequiredService<ServiciuOrganizatii>(), acum, ct);
    }

    // Confirmed accounts still waiting to be approved: professors never activated, and admins of organisations not approved.
    private async Task AprobariAsync(MarkAiDbContext db, Posta posta, StergereCont stergere, DateTime acum, CancellationToken ct)
    {
        var zile = cfg.GetValue("Conturi:ZileAprobare", 7);
        var neaprobati = db.Utilizatori.Where(u => u.EmailConfirmatLa != null
            && ((u.ProfilMentor != null && u.ProfilMentor.ActivatLa == null && !db.Set<Organizatie>().Any(o => o.AdminId == u.Id))
                || db.Set<Organizatie>().Any(o => o.AdminId == u.Id && o.Stare != Organizatie.Aprobata)));

        foreach (var u in await neaprobati.Where(u => u.AvertizatAprobareLa == null && u.CreatLa <= acum.AddDays(1 - zile)).ToListAsync(ct))
        {
            u.AvertizatAprobareLa = acum;
            posta.Pune(Sabloane.Notificare(u.Email, "Cererea ta nu a fost încă aprobată", "Contul tău se șterge mâine dacă nu e aprobat",
                $"Cererea ta așteaptă de {zile - 1} zile. Dacă nu e aprobată până mâine, contul și datele lui vor fi șterse.",
                "Poți cere să intri și în altă organizație din pagina de așteptare."), "avertizare_aprobare");
            await db.SaveChangesAsync(ct);
        }
        foreach (var u in await neaprobati.Where(u => u.CreatLa <= acum.AddDays(-zile)).Select(u => new { u.Id, u.Email, u.Nume }).ToListAsync(ct))
            await stergere.StergeAsync(u.Id, Sabloane.ContSters(u.Email, u.Nume,
                $"Cererea ta nu a fost aprobată în {zile} zile de la înregistrare, așa că am șters contul și datele lui."), ct);
    }

    private static async Task CereriAsync(MarkAiDbContext db, Posta posta, ServiciuOrganizatii org, DateTime acum, CancellationToken ct)
    {
        var deAmintit = await db.Set<MembruOrganizatie>().Include(m => m.Organizatie).ThenInclude(o => o.Admin).Include(m => m.Utilizator)
            .Where(m => m.Stare == MembruOrganizatie.InAsteptare && m.AmintitLa == null && m.ExpiraLa <= acum.AddDays(1) && m.ExpiraLa > acum
                && m.Utilizator.EmailConfirmatLa != null).ToListAsync(ct);
        foreach (var m in deAmintit)
        {
            m.AmintitLa = acum;
            posta.Pune(Sabloane.Notificare(m.Organizatie.Admin.Email, "O cerere expiră mâine", $"Cererea lui {m.Utilizator.Nume} expiră mâine",
                $"{m.Utilizator.Nume} a cerut să intre în „{m.Organizatie.Nume}” ca {(m.Rol == MembruOrganizatie.RolProfesor ? "profesor" : "elev")}. Dacă nu decide nimeni până mâine, cererea expiră."),
                "amintire_cerere");
        }
        await db.SaveChangesAsync(ct);

        var expirate = await db.Set<MembruOrganizatie>().Include(m => m.Organizatie).Include(m => m.Utilizator)
            .Where(m => m.Stare == MembruOrganizatie.InAsteptare && m.ExpiraLa <= acum).ToListAsync(ct);
        foreach (var m in expirate)
        {
            m.Stare = MembruOrganizatie.Expirat;
            if (m.Utilizator.EmailConfirmatLa is not null)
                posta.Pune(Sabloane.Notificare(m.Utilizator.Email, "Cererea ta a expirat", $"Cererea ta în „{m.Organizatie.Nume}” a expirat",
                    "Nimeni din organizație nu a răspuns în 7 zile. Poți trimite o cerere nouă sau poți alege altă organizație.",
                    "Contul tău rămâne activ."), "cerere_expirata");
        }
        await db.SaveChangesAsync(ct);
        foreach (var uid in expirate.Where(m => m.Rol == MembruOrganizatie.RolProfesor).Select(m => m.UtilizatorId).Distinct())
            await org.SincronizeazaProfesorAsync(uid, ct);
    }
}
