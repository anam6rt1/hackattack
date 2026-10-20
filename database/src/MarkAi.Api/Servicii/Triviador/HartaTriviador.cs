namespace MarkAi.Api.Servicii.Triviador;

/// <summary>One island: its place on the map, the drawing (seed, building) and the lessons its questions prefer.</summary>
public sealed record InsulaHarta(string Id, string Nume, string Autor, int X, int Y, int Seed, string Semn, int[] CursIds);

/// <summary>A game map: islands, the bridges between them, and the size of the drawing (map units).</summary>
public sealed class HartaJoc(int latime, int inaltime, IReadOnlyList<InsulaHarta> insule, IReadOnlyList<string[]> poduri)
{
    public int Latime { get; } = latime;
    public int Inaltime { get; } = inaltime;
    public IReadOnlyList<InsulaHarta> Insule { get; } = insule;
    public IReadOnlyList<string[]> Poduri { get; } = poduri;

    public IEnumerable<string> Vecini(string id) =>
        Poduri.Where(p => p[0] == id || p[1] == id).Select(p => p[0] == id ? p[1] : p[0]);

    /// <summary>Bridge hops from one island to every other (all islands are reachable).</summary>
    public Dictionary<string, int> Distante(string de)
    {
        var dist = new Dictionary<string, int> { [de] = 0 };
        var coada = new Queue<string>([de]);
        while (coada.TryDequeue(out var x))
            foreach (var v in Vecini(x).Where(v => !dist.ContainsKey(v))) { dist[v] = dist[x] + 1; coada.Enqueue(v); }
        return dist;
    }

    /// <summary>Starting castles for <paramref name="jucatori"/> players, as far from each other as the bridges allow.</summary>
    public IReadOnlyList<string> Cetati(int jucatori)
    {
        // The first castle is the island farthest from the map's centre island; each next one maximises its distance to the others.
        var centru = Insule.OrderBy(i => Math.Pow(i.X - Latime / 2.0, 2) + Math.Pow(i.Y - Inaltime / 2.0, 2)).First().Id;
        var dinCentru = Distante(centru);
        var cetati = new List<string> { Insule.OrderByDescending(i => dinCentru[i.Id]).ThenBy(i => i.Id, StringComparer.Ordinal).First().Id };
        var distante = new List<Dictionary<string, int>> { Distante(cetati[0]) };
        while (cetati.Count < jucatori)
        {
            var urmatoarea = Insule.Where(i => !cetati.Contains(i.Id))
                .OrderByDescending(i => distante.Min(d => d[i.Id])).ThenByDescending(i => distante.Sum(d => d[i.Id]))
                .ThenBy(i => i.Id, StringComparer.Ordinal).First().Id;
            cetati.Add(urmatoarea);
            distante.Add(Distante(urmatoarea));
        }
        return cetati;
    }
}

/// <summary>
/// Builds a map for 9–30 islands: islands sit on a jittered grid (so bridges never cross), a random part of the grid is
/// left as open sea, and some bridges are removed while every island stays reachable.
/// </summary>
public static class GeneratorHarta
{
    public const int MinInsule = 9, MaxInsule = 30;
    private const int LatimeCelula = 250, InaltimeCelula = 210, Margine = 40, Sus = 70;
    private static readonly string[] Semne = ["castel", "conac", "turn", "casa", "clopot", "far", "moara"];

    /// <param name="capitole">Island names (chapters of the chosen subjects), in reading order; the rest become "Insula N".</param>
    public static HartaJoc Genereaza(int numar, IReadOnlyList<(string Nume, string Autor, int[] CursIds)> capitole, Random rnd)
    {
        if (numar is < MinInsule or > MaxInsule) throw new ArgumentOutOfRangeException(nameof(numar));
        var coloane = (int)Math.Ceiling(Math.Sqrt(numar * 1.6));
        var randuri = (int)Math.Ceiling(numar / (double)coloane);

        // Open sea: drop random grid cells, never one whose removal would cut the islands apart.
        var celule = Enumerable.Range(0, coloane * randuri).Select(i => (R: i / coloane, C: i % coloane)).ToHashSet();
        while (celule.Count > numar)
        {
            var candidate = celule.OrderBy(_ => rnd.Next()).ToList();
            var scoasa = candidate.First(c => Conectate(celule.Where(x => x != c).ToHashSet()));
            celule.Remove(scoasa);
        }

        var ordonate = celule.OrderBy(c => c.R).ThenBy(c => c.C).ToList();
        var id = ordonate.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => "i" + x.i);
        var insule = ordonate.Select((c, i) =>
        {
            var cap = i < capitole.Count ? capitole[i] : ($"Insula {i + 1}", "întrebări generale", Array.Empty<int>());
            return new InsulaHarta(id[c], cap.Item1, cap.Item2,
                Margine + c.C * LatimeCelula + LatimeCelula / 2 + rnd.Next(-35, 36),
                Sus + c.R * InaltimeCelula + InaltimeCelula / 2 + rnd.Next(-25, 26),
                rnd.Next(1, 1000), Semne[rnd.Next(Semne.Length)], cap.Item3);
        }).ToList();

        // Bridges between grid neighbours; then about a quarter are removed, keeping the map connected.
        var poduri = ordonate.SelectMany(c => new[] { (c.R, C: c.C + 1), (R: c.R + 1, c.C) }.Where(celule.Contains).Select(v => (c, v))).ToList();
        foreach (var pod in poduri.OrderBy(_ => rnd.Next()).Take(poduri.Count / 4).ToList())
        {
            var fara = poduri.Where(p => p != pod).ToList();
            if (Conectate(celule, fara)) poduri = fara;
        }
        var latime = Margine * 2 + coloane * LatimeCelula;
        var inaltime = Sus + randuri * InaltimeCelula + Margine;
        return new HartaJoc(latime, inaltime, insule, poduri.Select(p => new[] { id[p.c], id[p.v] }).ToList());
    }

    private static bool Conectate(HashSet<(int R, int C)> celule, List<((int R, int C) A, (int R, int C) B)>? poduri = null)
    {
        if (celule.Count == 0) return true;
        bool Legate((int R, int C) a, (int R, int C) b) => poduri is null
            ? Math.Abs(a.R - b.R) + Math.Abs(a.C - b.C) == 1
            : poduri.Contains((a, b)) || poduri.Contains((b, a));
        var vazute = new HashSet<(int, int)> { celule.First() };
        var coada = new Queue<(int R, int C)>(vazute);
        while (coada.TryDequeue(out var x))
            foreach (var v in celule.Where(v => !vazute.Contains(v) && Legate(x, v))) { vazute.Add(v); coada.Enqueue(v); }
        return vazute.Count == celule.Count;
    }
}
