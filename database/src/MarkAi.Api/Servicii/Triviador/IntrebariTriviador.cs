using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii.Triviador;

public sealed record IntrebareGrila(int Id, int? ParcursId, string Text, IReadOnlyList<string> Variante, int Corect);
public sealed record IntrebareEstimare(int Id, int? ParcursId, string Text, int Raspuns);

/// <summary>
/// The questions of one room, with their answers; they never leave the server. A room draws from every active question of
/// its subjects (public or not: Triviador is a practice game). Without questions of a kind, the demo set below is used.
/// </summary>
public sealed class PachetIntrebari(List<IntrebareGrila> grila, List<IntrebareEstimare> estimari)
{
    public List<IntrebareGrila> Grila { get; } = grila;
    public List<IntrebareEstimare> Estimari { get; } = estimari;
    private readonly HashSet<int> _folosite = [];

    /// <param name="parcursIds">With a grade filter, only the questions of these Lecții (otherwise every question of the subjects).</param>
    public static async Task<PachetIntrebari> IncarcaAsync(MarkAiDbContext db, IReadOnlyCollection<int> materieIds, IReadOnlyCollection<int>? parcursIds, CancellationToken ct)
    {
        var q0 = db.Set<Intrebare>().AsNoTracking().Include(q => q.Variante).Where(q => materieIds.Contains(q.MaterieId) && q.Activa);
        if (parcursIds is not null) q0 = q0.Where(q => q.ParcursId != null && parcursIds.Contains(q.ParcursId.Value));
        var intrebari = await q0.ToListAsync(ct);
        var grila = intrebari.Where(q => q.Tip == Intrebare.Grila)
            .Select(q => (q, v: q.Variante.OrderBy(o => o.Id).ToList()))
            .Where(t => t.v.Count is >= 2 and <= 4 && t.v.Count(o => o.Corecta) == 1)
            .Select(t => new IntrebareGrila(t.q.Id, t.q.ParcursId, t.q.Text, t.v.Select(o => o.Text).ToList(), t.v.FindIndex(o => o.Corecta)))
            .ToList();
        var estimari = intrebari.Where(q => q.Tip == Intrebare.Estimare && q.RaspunsNumeric is not null)
            .Select(q => new IntrebareEstimare(q.Id, q.ParcursId, q.Text, q.RaspunsNumeric!.Value)).ToList();
        return new PachetIntrebari(grila.Count > 0 ? grila : [.. GrilaDemo], estimari.Count > 0 ? estimari : [.. EstimariDemo]);
    }

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
