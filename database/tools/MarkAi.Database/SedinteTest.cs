using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Scheduled tutoring sessions for a test or demo database, so Profesori, bookings and Acasă can be tried without preparing
/// anything. Each professor gets three of six sessions, in turn, so together they cover the next 7 days and the weeks after,
/// online / față în față / hibrid, paid and pro bono, with free places and one full session; some are booked by the students
/// given. Dates are relative to <c>acum</c> (local time in Bucharest). A session is matched by professor and title and only added
/// again once the previous one has passed, so running it twice adds nothing and running it a week later refreshes the dates.
/// Used by <c>date-test</c>, the preview API and the tests.
/// </summary>
public static class SedinteTest
{
    private sealed record Sablon(string Titlu, int Zile, int Ora, string Tip, int Locuri, bool ProBono, int Rezervari, bool LaOrganizatie);

    // The first three are within 7 days, the last three after; "Exerciții rezolvate" is full once booked.
    private static readonly Sablon[] Sabloane =
    [
        new("Recapitulare pentru bacalaureat", 1, 17, CameraTutoring.Online, 10, false, 2, false),
        new("Exerciții rezolvate", 3, 16, CameraTutoring.FataInFata, 2, false, 2, true),
        new("Consultații gratuite", 5, 18, CameraTutoring.Hibrid, 6, true, 2, true),
        new("Pregătire pentru teză", 10, 17, CameraTutoring.Online, 8, true, 0, false),
        new("Test de verificare", 14, 15, CameraTutoring.Hibrid, 4, false, 1, false),
        new("Atelier față în față", 21, 10, CameraTutoring.FataInFata, 12, false, 0, false),
    ];

    private static readonly TimeZoneInfo Bucuresti = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    /// <param name="profesori">Usernames of approved professors with at least one subject; others are skipped.</param>
    /// <param name="elevi">Usernames of students who book places, in turn.</param>
    /// <returns>The number of sessions and bookings added.</returns>
    public static async Task<(int Sedinte, int Rezervari)> AdaugaAsync(MarkAiDbContext db, IReadOnlyList<string> profesori, IReadOnlyList<string> elevi, DateTime acum)
    {
        var pret = await db.TarifeFixe.Where(t => t.Activ).OrderBy(t => t.Id).FirstOrDefaultAsync();
        var idElevi = new List<int>();
        foreach (var e in elevi)
            if (await db.Utilizatori.Where(u => u.Username == e).Select(u => (int?)u.Id).SingleOrDefaultAsync() is { } id) idElevi.Add(id);
        var azi = TimeZoneInfo.ConvertTimeFromUtc(acum, Bucuresti).Date;
        var rezervari = new RezervareService(db);
        int sedinte = 0, rezervate = 0, urmatorul = 0;

        foreach (var (username, i) in profesori.Select((p, i) => (p, i)))
        {
            var p = await db.Utilizatori.Where(u => u.Username == username && u.ProfilMentor != null && u.ProfilMentor.StareVerificare == RezervareService.MentorAprobat)
                .Select(u => new { u.Id, u.JudetId, u.LocalitateId, Materie = u.ProfilMentor!.Materii.Select(m => (int?)m.MaterieId).FirstOrDefault(),
                    Organizatie = db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == u.Id && m.Stare == MembruOrganizatie.Aprobat && m.Organizatie.Stare == Organizatie.Aprobata)
                        .Select(m => m.Organizatie).FirstOrDefault() })
                .SingleOrDefaultAsync();
            if (p?.Materie is null) continue;
            foreach (var s in new[] { i % 6, (i + 2) % 6, (i + 4) % 6 }.Select(k => Sabloane[k]))
            {
                if (await db.CamereTutoring.AnyAsync(c => c.MentorId == p.Id && c.Titlu == s.Titlu && c.Status == RezervareService.SlotDeschis && c.InceputLa > acum)) continue;
                var inceput = TimeZoneInfo.ConvertTimeToUtc(azi.AddDays(s.Zile).AddHours(s.Ora), Bucuresti);
                var proBono = s.ProBono || pret is null;
                var c = new CameraTutoring
                {
                    MentorId = p.Id, MaterieId = p.Materie.Value, Titlu = s.Titlu, Capacitate = s.Locuri, ProBono = proBono, TarifId = proBono ? null : pret!.Id,
                    Status = RezervareService.SlotDeschis, InceputLa = inceput, FinalizatLa = inceput.AddMinutes(pret?.DurataMinute ?? 60), Tip = s.Tip,
                    Descriere = "Ședință de test: rezervările nu se plătesc cu adevărat.",
                    LinkIntalnire = s.Tip == CameraTutoring.FataInFata ? null : "https://example.com/sedinta-test",
                };
                if (s.Tip != CameraTutoring.Online)
                {
                    // At the organisation when asked (and there is one), otherwise at the professor's own place, like the API.
                    if ((s.LaOrganizatie || p.JudetId is null) && p.Organizatie is { } o)
                        (c.LocOrganizatieId, c.LocJudetId, c.LocLocalitateId) = (o.Id, o.JudetId, o.LocalitateId);
                    else if (p.JudetId is not null) (c.LocJudetId, c.LocLocalitateId) = (p.JudetId, p.LocalitateId);
                    else continue;
                    c.NotaLoc = "Sala 12, etajul 1";
                }
                db.CamereTutoring.Add(c);
                await db.SaveChangesAsync();
                sedinte++;
                for (var r = 0; r < Math.Min(s.Rezervari, idElevi.Count); r++)
                {
                    var elev = idElevi[urmatorul++ % idElevi.Count];
                    if (elev == p.Id) continue;
                    await rezervari.RezervaAsync(elev, c.Id, r % 2 == 0 ? Rezervare.Online : Rezervare.LaSala);
                    rezervate++;
                }
            }
        }
        return (sedinte, rezervate);
    }
}
