using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using MarkAi.Api.Setari;

namespace MarkAi.Api.Servicii;

public sealed class DatePersonale(MarkAiDbContext db, VersiuniSesiune versiuni, TimeProvider ceas, ConfirmariCont confirmari, IncarcariReluabile incarcari)
{
    public const string VersiuneInformare = "2026-10-25";

    private async Task<Utilizator> Verifica(int uid, string parola, CancellationToken ct)
    {
        var u = await db.Utilizatori.Include(x => x.Roluri).ThenInclude(x => x.Rol).SingleAsync(x => x.Id == uid, ct);
        if (u.StergereCerutaLa is not null || string.IsNullOrEmpty(parola) || parola.Length > 1024 || !Parole.Verifica(u, parola))
            throw new CerereInvalida("Parola actuală nu este corectă.");
        return u;
    }

    // Explicit allowlists: future token/hash/storage fields cannot accidentally enter an export.
    private async Task<object[]> Randuri<T>(IQueryable<T> query, string campuri, CancellationToken ct) where T : class
    {
        var proprietati = campuri.Split(' ').Select(n => typeof(T).GetProperty(n) ?? throw new InvalidOperationException("Unknown export field: " + n)).ToArray();
        return (await query.AsNoTracking().ToListAsync(ct)).Select(x => (object)proprietati.ToDictionary(p => p.Name, p => p.GetValue(x))).ToArray();
    }

    public async Task<byte[]> ExportaAsync(int uid, string parola, CancellationToken ct)
    {
        var u = await Verifica(uid, parola, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var date = new Dictionary<string, object?>
        {
            ["invitatiiPrimite"] = await Randuri(db.Set<InvitatieElev>().Where(x => x.Email == u.Email.ToLower()), "OrganizatieId Nume Email Clasa CreatLa ExpiraLa AcceptataLa", ct),
            ["feedback"] = await Randuri(db.Set<FeedbackPilot>().Where(x => x.UtilizatorId == uid), "Id Text Pagina CreatLa RezolvatLa", ct),
            ["sumarParinte"] = await Randuri(db.Set<SumarParinte>().Where(x => x.UtilizatorId == uid), "Email CreatLa ExpiraLa ConfirmatLa UltimaSaptamana", ct),
            ["statisticiRaspunsuri"] = await Randuri(db.Set<RaspunsStatistica>().Where(x => x.UtilizatorId == uid), "IncercareId IntrebareId Corect RaspunsLa", ct),
            ["versiune"] = 1, ["exportatLa"] = ceas.GetUtcNow(),
            ["simulari"] = (await db.Set<Simulare>().AsNoTracking().Where(s => s.UtilizatorId == uid).ToListAsync(ct)).Select(s => new {
                s.Id, s.MaterieId, s.Tip, s.Clasa, s.InceputLa, s.ExpiraLa, s.FinalizatLa, s.Total, Corecte = s.FinalizatLa != null ? (int?)s.Corecte : null,
                Raspunsuri = JsonSerializer.Deserialize<List<MarkAi.Api.Endpoints.IntrebareSimulare>>(s.IntrebariJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
                    .Select(q => new { q.Id, q.Text, q.Raspunsa, q.VariantaAleasaId, q.ValoareAleasa }).ToArray() }).ToArray(),
            ["preferinteNotificari"] = await Randuri(db.Set<PreferinteNotificari>().Where(x => x.UtilizatorId == uid), "Sedinte Decizii TemeNoi TermeneTeme ActualizatLa", ct),
            ["notificari"] = await Randuri(db.Set<Notificare>().Where(x => x.UtilizatorId == uid), "Id Tip Titlu Text Url CreatLa FinalizatLa Canal", ct),
            ["browsereNotificari"] = await Randuri(db.Set<AbonamentPush>().Where(x => x.UtilizatorId == uid), "Id CreatLa", ct),
            ["greseli"] = await Randuri(db.Set<Greseala>().Where(x => x.UtilizatorId == uid), "IntrebareId GresitLa ScadentaLa Nivel CorecteConsecutive", ct),
            ["grupeElev"] = await Randuri(db.Set<GrupaMembru>().Where(x => x.ElevId == uid), "GrupaId ElevId", ct),
            ["grupeProfesor"] = await Randuri(db.Set<GrupaElevi>().Where(x => x.ProfesorId == uid), "Id OrganizatieId Nume", ct),
            ["temeProfesor"] = await Randuri(db.Set<Tema>().Where(x => x.ProfesorId == uid), "Id OrganizatieId Titlu Descriere CreatLa TermenLa ToataOrganizatia Anulata", ct),
            ["temeElev"] = await Randuri(db.Set<Tema>().Where(t => db.Set<MembruOrganizatie>().Any(m => m.UtilizatorId == uid && m.OrganizatieId == t.OrganizatieId && m.Rol == "elev" && m.Stare == "aprobat")
                && (t.ToataOrganizatia || t.Grupe.Any(g => g.Grupa.Membri.Any(m => m.ElevId == uid)))), "Id OrganizatieId Titlu Descriere CreatLa TermenLa Anulata", ct),
            ["cont"] = new { u.Id, u.Nume, u.Username, u.Email, u.Telefon, u.CreatLa, u.EmailConfirmatLa, u.TelefonVerificatLa,
                u.JudetId, u.LocalitateId, u.Clasa, u.Sub16AniDeclarat, u.AcordParinteDeclaratLa, u.VersiuneInformare,
                Roluri = u.Roluri.Select(r => r.Rol.Nume).ToArray() },
            ["preferinte"] = await Randuri(db.PreferinteUtilizatori.Where(x => x.UtilizatorId == uid), "UtilizatorId InterfataId BackgroundStaticId ModBackground Timezone ActualizatLa VoceTutor VitezaTutor TonTutor Avatar AscundeLectiiTerminate", ct),
            ["profilProfesor"] = await Randuri(db.ProfiluriMentor.Where(x => x.UtilizatorId == uid), "Descriere StareVerificare ProcentComision OnorariuNegociat Moneda ActivatLa MotivRespingere RetrasLa LocOrganizatieId", ct),
            ["materiiProfesor"] = await Randuri(db.Set<MentorMaterie>().Where(x => x.MentorId == uid), "MentorId MaterieId", ct),
            ["organizatii"] = await Randuri(db.Set<MembruOrganizatie>().Where(x => x.UtilizatorId == uid), "OrganizatieId Rol Stare PoatePublicaPublic CerutLa ExpiraLa DecisLa MotivRespingere", ct),
            ["organizatiiAdministrate"] = await Randuri(db.Set<Organizatie>().Where(x => x.AdminId == uid), "Id Nume Adresa JudetId LocalitateId Stare CreatLa", ct),
            ["progres"] = await Randuri(db.Set<ProgresLectie>().Where(x => x.UtilizatorId == uid), "ParcursId UltimulCursId PromovatLa UltimaAccesare", ct),
            ["incercariQuiz"] = await Randuri(db.Set<IncercareQuiz>().Where(x => x.UtilizatorId == uid), "ParcursId IntrebariIds Raspunsuri Corecte InceputLa FinalizataLa", ct),
            ["recompense"] = await Randuri(db.EvenimenteRecompensa.Where(x => x.UtilizatorId == uid), "XpAcordat PuncteMaterie MaterieId TipEveniment CreatLa", ct),
            ["generariAi"] = await Randuri(db.Set<GenerareAi>().Where(x => x.UtilizatorId == uid), "ParcursId Numar CreatLa Stare Mesaj IntrebariGenerate Actualizat", ct),
            ["lectiiCreate"] = await Randuri(db.Set<Parcurs>().Where(x => x.AutorId == uid), "Id Titlu Descriere MaterieId Clasa OrganizatieId Vizibilitate Publicat", ct),
            ["fisiereIncarcate"] = await Randuri(db.Set<FisierLectie>().Where(x => x.IncarcatDeId == uid), "Id ParcursId Nume Tip MarimeOcteti IncarcatLa", ct),
            ["sedinteProfesor"] = await Randuri(db.CamereTutoring.Where(x => x.MentorId == uid), "Id MaterieId Titlu Descriere Capacitate ProBono Tip LinkIntalnire NotaLoc Status InceputLa FinalizatLa", ct),
            ["rezervari"] = await Randuri(db.Rezervari.Where(x => x.ElevId == uid), "Id CameraId Status TarifAchitat Moneda RezervataLa FinalizataLa Participare", ct),
            ["plati"] = await Randuri(db.Plati.Where(x => x.Rezervare.ElevId == uid), "Id RezervareId Suma Moneda Status Procesator TranzactieExterna PlatitaLa", ct),
            ["comisioane"] = await Randuri(db.ComisioaneMentor.Where(x => x.MentorId == uid), "RezervareId DecontareId TarifBaza ProcentSnapshot SumaComision Moneda Status CalculatLa", ct),
            ["decontari"] = await Randuri(db.Decontari.Where(x => x.MentorId == uid), "Id SumaTotala Moneda Status ReferintaPlata CreataLa PlatitaLa", ct),
            ["sesiuni"] = await Randuri(db.Sesiuni.Where(x => x.UtilizatorId == uid), "Id CursId RezervareId Canal Status InceputLa FinalizatLa DurataSecunde", ct),
            ["conversatii"] = await Randuri(db.Mesaje.Where(x => x.Sesiune.UtilizatorId == uid), "SesiuneId Expeditor Tip Continut CreatLa", ct),
            ["mesajeTrimise"] = await Randuri(db.Set<MesajIesire>().Where(x => x.Catre == u.Email || (u.Telefon != null && x.Catre == u.Telefon)), "Canal Catre Subiect Tip CreatLa TrimisLa Furnizor", ct),
            ["documentePersonale"] = await db.ProfiluriMentor.AsNoTracking().Where(x => x.UtilizatorId == uid)
                .Select(x => new { fotografie = x.PozaUrl != null ? "/utilizatori/me/date/documente/fotografie" : null,
                    dovada = x.DovadaCalificareUrl != null ? "/utilizatori/me/date/documente/dovada" : null }).SingleOrDefaultAsync(ct),
            ["incarcariNeterminate"] = await incarcari.ExportaPentruAsync(uid, ct),
        };
        await tx.CommitAsync(ct);
        return JsonSerializer.SerializeToUtf8Bytes(date, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    public async Task StergeAsync(int uid, string parola, string confirmare, CancellationToken ct)
    {
        if (confirmare != "STERGE") throw new CerereInvalida("Scrie STERGE pentru a confirma ștergerea contului.");
        var u = await Verifica(uid, parola, ct);
        if (await db.Set<Organizatie>().AnyAsync(o => o.AdminId == uid, ct))
            throw new Conflict("Transferă administrarea organizației înainte de a șterge contul. Contactează administratorul platformei.");
        if (u.Roluri.Any(r => r.Rol.Nume == Roluri.Admin))
            throw new Conflict("Un cont de administrator trebuie dezactivat de alt administrator, după transferarea responsabilităților.");
        var acum = ceas.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM mark_ai.utilizator WHERE id = {uid} FOR UPDATE", ct);
        await db.Entry(u).ReloadAsync(ct);
        if (u.StergereCerutaLa is not null) return;
        var p = await db.ProfiluriMentor.SingleOrDefaultAsync(x => x.UtilizatorId == uid, ct);
        var chei = new[] { p?.PozaUrl, p?.DovadaCalificareUrl }.OfType<string>().ToArray();
        var sedinte = db.CamereTutoring.Where(x => x.MentorId == uid && x.Status == RezervareService.SlotDeschis && x.InceputLa > acum);
        var rezervariAnulate = db.Rezervari.Where(r => ((r.ElevId == uid && r.Camera.InceputLa > acum) || sedinte.Any(s => s.Id == r.CameraId)) && r.Status == RezervareService.RezervareConfirmata);
        await db.ComisioaneMentor.Where(c => rezervariAnulate.Any(r => r.Id == c.RezervareId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "anulat"), ct);
        await db.Plati.Where(p => p.Status == "simulata" && rezervariAnulate.Any(r => r.Id == p.RezervareId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "rambursata"), ct);
        await rezervariAnulate.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RezervareService.RezervareAnulata), ct);
        await sedinte.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "anulata").SetProperty(x => x.LinkIntalnire, (string?)null).SetProperty(x => x.NotaLoc, (string?)null), ct);
        await db.Mesaje.Where(x => x.Sesiune.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.EvenimenteRecompensa.Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Sesiuni.Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<MesajIesire>().Where(x => x.Catre == u.Email || (u.Telefon != null && x.Catre == u.Telefon)).ExecuteDeleteAsync(ct);
        await db.Set<CodVerificare>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<IncercareQuiz>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<ProgresLectie>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<Greseala>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<FeedbackPilot>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<SumarParinte>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<MesajIesire>().Where(m => db.Set<EmailSumar>().Any(e => e.MesajId == m.Id && e.UtilizatorId == uid)).ExecuteDeleteAsync(ct);
        await db.Set<InvitatieElev>().Where(i => i.Email == u.Email.ToLower()).ExecuteDeleteAsync(ct);
        await db.Set<Simulare>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<Notificare>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<AbonamentPush>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<PreferinteNotificari>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<GrupaMembru>().Where(x => x.ElevId == uid).ExecuteDeleteAsync(ct);
        await db.Set<GrupaElevi>().Where(x => x.ProfesorId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.ProfesorId, (int?)null), ct);
        await db.Set<Tema>().Where(x => x.ProfesorId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.ProfesorId, (int?)null), ct);
        await db.PreferinteUtilizatori.Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<MentorMaterie>().Where(x => x.MentorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<MembruOrganizatie>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        await db.Set<GenerareAi>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        // Organisation materials remain available; remove the departing account's attribution.
        await db.Set<Parcurs>().Where(x => x.AutorId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.AutorId, (int?)null), ct);
        await db.Set<FisierLectie>().Where(x => x.IncarcatDeId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.IncarcatDeId, (int?)null), ct);
        if (p is not null)
        {
            p.Descriere = null; p.PozaUrl = null; p.DovadaCalificareUrl = null; p.MotivRespingere = null;
            p.StareVerificare = "respins"; p.LocOrganizatieId = null; p.RetrasLa = acum;
        }
        await db.Set<UtilizatorRol>().Where(x => x.UtilizatorId == uid).ExecuteDeleteAsync(ct);
        var alias = Guid.NewGuid().ToString("N");
        u.Nume = "Cont șters"; u.Username = "sters-" + alias; u.Email = alias + "@deleted.invalid"; u.Telefon = null;
        u.ParolaHash = ""; u.EmailConfirmatLa = null; u.TelefonVerificatLa = null;
        u.JudetId = null; u.LocalitateId = null; u.Clasa = null; u.Sub16AniDeclarat = null;
        u.AcordParinteDeclaratLa = null; u.VersiuneInformare = null; u.UltimaCerereApel = null; u.StergereCerutaLa = acum;
        db.Add(new StergereDateCont { UtilizatorId = uid, CerutLa = acum, CheiFisiere = chei });
        await db.SaveChangesAsync(ct);
        await versiuni.CresteAsync(db, uid, ct);
        await tx.CommitAsync(ct);
        versiuni.MarcheazaSters(uid);
        confirmari.Anuleaza(uid);
    }
}
