using System.Linq.Expressions;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii.Triviador;

public sealed record IntrebareGrila(int Id, int? ParcursId, string Text, IReadOnlyList<string> Variante, int Corect);
public sealed record IntrebareEstimare(int Id, int? ParcursId, string Text, int Raspuns);

/// <summary>
/// The questions of one room, with their answers; they never leave the server. A room draws from the active questions of its
/// subjects (public or not: Triviador is a practice game), sampled in SQL so a room never loads a whole subject: up to
/// <see cref="PeInsula"/> per island's Lecție (asked there first) and a random <see cref="Rezerva"/> from the rest of its
/// subjects and grade. Without questions of a kind, the demo set below is used.
/// </summary>
public sealed class PachetIntrebari(List<IntrebareGrila> grila, List<IntrebareEstimare> estimari)
{
    /// <summary>Multiple-choice questions (estimates: a fifth of it) per island's Lecție, and from the rest of the room's subjects.</summary>
    public const int PeInsula = 15, Rezerva = 100;

    public List<IntrebareGrila> Grila { get; } = grila;
    public List<IntrebareEstimare> Estimari { get; } = estimari;
    private readonly HashSet<int> _folosite = [];

    /// <param name="clasa">The room's grade filter: only questions of Lecții of that grade (null = any).</param>
    /// <param name="insule">The islands' Lecții.</param>
    public static async Task<PachetIntrebari> IncarcaAsync(MarkAiDbContext db, IReadOnlyCollection<int> materieIds, string? clasa, IReadOnlyCollection<int> insule,
        CancellationToken ct)
    {
        var active = db.Set<Intrebare>().AsNoTracking().Where(q => materieIds.Contains(q.MaterieId)
            && (clasa == null || (q.Parcurs != null && q.Parcurs.Clasa == clasa)));
        // Per island: a random sample of that Lecție's own questions (one SQL query with a per-Lecție limit).
        var deInsule = db.Set<Parcurs>().AsNoTracking().Where(p => insule.Contains(p.Id));
        // The islands' filter is repeated on the questions: EF numbers each Lecție's sample in a subquery (ROW_NUMBER) that
        // PostgreSQL does not narrow through the join, so without it every question in the database is checked and numbered
        // (~180 ms with 29 000 questions; under 1 ms with it).
        var intrebariInsule = db.Set<Intrebare>().Where(q => q.ParcursId != null && insule.Contains(q.ParcursId.Value));
        var grila = await CitesteGrile(deInsule.SelectMany(p => intrebariInsule.Where(q => q.ParcursId == p.Id && q.MaterieId == p.MaterieId)
            .Where(GrilaFolosibila).OrderBy(_ => EF.Functions.Random()).Take(PeInsula)), ct);
        var luate = grila.Select(q => q.Id).ToList();
        grila.AddRange(await CitesteGrile(active.Where(GrilaFolosibila).Where(q => !luate.Contains(q.Id)).OrderBy(_ => EF.Functions.Random()).Take(Rezerva), ct));

        var estimari = await CitesteEstimari(deInsule.SelectMany(p => intrebariInsule.Where(q => q.ParcursId == p.Id && q.MaterieId == p.MaterieId).Where(EstimareFolosibila)
            .OrderBy(_ => EF.Functions.Random()).Take(PeInsula / 5 + 1)), ct);
        luate = estimari.Select(q => q.Id).ToList();
        estimari.AddRange(await CitesteEstimari(active.Where(EstimareFolosibila).Where(q => !luate.Contains(q.Id))
            .OrderBy(_ => EF.Functions.Random()).Take(Rezerva / 5), ct));
        return new PachetIntrebari(grila.Count > 0 ? grila : [.. GrilaDemo], estimari.Count > 0 ? estimari : [.. EstimariDemo]);
    }

    // A multiple-choice question is usable when active, with 2–4 options and exactly one right one; an estimate when it has an answer.
    private static readonly Expression<Func<Intrebare, bool>> GrilaFolosibila = q => q.Activa && q.Tip == Intrebare.Grila
        && q.Variante.Any(v => v.Corecta) && q.Variante.Any(v => !v.Corecta) && q.Variante.Count <= 4;
    private static readonly Expression<Func<Intrebare, bool>> EstimareFolosibila = q => q.Activa && q.Tip == Intrebare.Estimare && q.RaspunsNumeric != null;

    private static async Task<List<IntrebareGrila>> CitesteGrile(IQueryable<Intrebare> q, CancellationToken ct) =>
        (await q.Select(x => new { x.Id, x.ParcursId, x.Text, Variante = x.Variante.OrderBy(v => v.Id).Select(v => new { v.Text, v.Corecta }).ToList() }).ToListAsync(ct))
        .Select(x => new IntrebareGrila(x.Id, x.ParcursId, x.Text, x.Variante.Select(v => v.Text).ToList(), x.Variante.FindIndex(v => v.Corecta))).ToList();

    private static async Task<List<IntrebareEstimare>> CitesteEstimari(IQueryable<Intrebare> q, CancellationToken ct) =>
        await q.Select(x => new IntrebareEstimare(x.Id, x.ParcursId, x.Text, x.RaspunsNumeric!.Value)).ToListAsync(ct);

    /// <summary>A question not used yet in this room, preferably from the attacked island's Lecție; starts over when all were used.</summary>
    public T Alege<T>(List<T> lista, int[] parcursIds, Func<T, int> id, Func<T, int?> parcurs, Random rnd)
    {
        var libere = lista.Where(q => !_folosite.Contains(id(q))).ToList();
        if (libere.Count == 0)
        {
            foreach (var q in lista) _folosite.Remove(id(q));
            libere = lista;
        }
        var dinInsula = parcursIds.Length > 0 ? libere.Where(q => parcurs(q) is { } p && parcursIds.Contains(p)).ToList() : [];
        var sursa = dinInsula.Count > 0 ? dinInsula : libere;
        var ales = sursa[rnd.Next(sursa.Count)];
        _folosite.Add(id(ales));
        return ales;
    }

    // The game's original demo set (Romanian literature), used when the chosen subjects have no questions yet.
    private static readonly IntrebareGrila[] GrilaDemo =
    [
        new(-1, null, "Cine este autorul nuvelei „Moara cu noroc”?", ["Liviu Rebreanu", "Ioan Slavici", "Ion Creangă", "Mihail Sadoveanu"], 1),
        new(-2, null, "Ce meserie avea Ghiță înainte să ia hanul în arendă?", ["Cizmar", "Fierar", "Morar", "Negustor"], 0),
        new(-3, null, "Cum se numesc cele două părți ale romanului „Ion”?", ["Iarna și Vara", "Glasul pământului și Glasul iubirii", "Satul și Orașul", "Începutul și Sfârșitul"], 1),
        new(-4, null, "Cine este personajul principal din „Baltagul”?", ["Ana Baciu", "Otilia Mărculescu", "Vitoria Lipan", "Cătălina"], 2),
        new(-5, null, "Cine este autorul romanului „Enigma Otiliei”?", ["Camil Petrescu", "George Călinescu", "Marin Preda", "Liviu Rebreanu"], 1),
        new(-6, null, "Ce personaj îl înșală pe Harap-Alb și îi ia locul?", ["Spânul", "Ursul", "Împăratul Roș", "Gerilă"], 0),
        new(-7, null, "Cine a scris poezia „Plumb”?", ["Tudor Arghezi", "Lucian Blaga", "George Bacovia", "Ion Barbu"], 2),
        new(-8, null, "Cine este autorul romanului „Moromeții”?", ["Marin Preda", "Marin Sorescu", "Mihail Sadoveanu", "Ioan Slavici"], 0),
        new(-9, null, "Cum se numește fata de împărat din „Luceafărul”?", ["Ileana", "Cătălina", "Ana", "Florica"], 1),
        new(-10, null, "Cine a scris poezia „Testament”?", ["George Bacovia", "Mihai Eminescu", "Tudor Arghezi", "Nichita Stănescu"], 2),
        new(-11, null, "Cine a scris piesa „Iona”?", ["Marin Sorescu", "Camil Petrescu", "Lucian Blaga", "Ion Barbu"], 0),
        new(-12, null, "Cine este naratorul din „Ultima noapte de dragoste, întâia noapte de război”?", ["Felix Sima", "Ștefan Gheorghidiu", "Ilie Moromete", "Ghiță"], 1),
        new(-13, null, "Cine a scris „Riga Crypto și lapona Enigel”?", ["Ion Barbu", "Lucian Blaga", "Tudor Arghezi", "George Bacovia"], 0),
        new(-14, null, "Ce este Lică Sămădăul?", ["Jandarm", "Hangiu", "Șef al porcarilor", "Preot"], 2),
    ];

    private static readonly IntrebareEstimare[] EstimariDemo =
    [
        new(-101, null, "În ce an a apărut romanul „Ion”?", 1920),
        new(-102, null, "În ce an a apărut poemul „Luceafărul”?", 1883),
        new(-103, null, "În ce an a apărut romanul „Baltagul”?", 1930),
        new(-104, null, "În ce an a apărut romanul „Enigma Otiliei”?", 1938),
        new(-105, null, "În ce an a apărut volumul „Novele din popor”, cu „Moara cu noroc”?", 1881),
    ];
}
