namespace MarkAi.Api.Infrastructura;

/// <summary>
/// Page sizes of the API's lists. Each list (and each tab or role of it that the API can tell apart) has its own default and
/// maximum; the site asks for the size it shows with <c>marime</c> (its own constants, frontend/lib/pagini.js), up to the maximum.
/// Changing how many items a page of the site shows never needs a change here; these only bound what the API answers.
/// </summary>
public static class Pagini
{
    /// <param name="Implicit">Items per page when <c>marime</c> is not given.</param>
    /// <param name="Maxim">The most a request may ask for.</param>
    public sealed record Marime(int Implicit, int Maxim);

    // Profesori (students looking for a tutor).
    public static readonly Marime ProfesoriPentruElevi = new(20, 100);

    // The organisation panel's tabs (GET /organizatia-mea/membri): requests, professors, students.
    public static readonly Marime CereriOrganizatie = new(20, 100);
    public static readonly Marime ProfesoriOrganizatie = new(20, 100);
    public static readonly Marime EleviOrganizatie = new(20, 100);

    // A professor confirming students of an organisation they teach in (GET /organizatii/{id}/elevi).
    public static readonly Marime EleviDeConfirmatProfesor = new(20, 100);

    // The platform admin's organisations, one per tab (state).
    public static readonly Marime OrganizatiiInAsteptareAdmin = new(20, 100);
    public static readonly Marime OrganizatiiAprobateAdmin = new(20, 100);
    public static readonly Marime OrganizatiiRespinseAdmin = new(20, 100);

    // The platform admin's professors, one per tab (state).
    public static readonly Marime ProfesoriInAsteptareAdmin = new(20, 100);
    public static readonly Marime ProfesoriAprobatiAdmin = new(20, 100);
    public static readonly Marime ProfesoriRespinsiAdmin = new(20, 100);

    // The content editor's Lecții, for a professor and for the platform admin.
    public static readonly Marime LectiiEditorProfesor = new(20, 100);
    public static readonly Marime LectiiEditorAdmin = new(20, 100);

    // A student's progress per subject (home page): only the subjects where they opened a Lecție.
    public static readonly Marime ProgresMateriiElev = new(20, 100);

    // The subjects overview (Materiale), for a student and for the platform admin.
    public static readonly Marime MateriiElev = new(20, 100);
    public static readonly Marime MateriiAdmin = new(20, 100);

    // A subject's Lecții (Materiale), for a student and for the platform admin.
    public static readonly Marime LectiiMaterieElev = new(20, 100);
    public static readonly Marime LectiiMaterieAdmin = new(20, 100);

    /// <summary>Rows to skip and take for page <paramref name="pagina"/> (from 1) of <paramref name="marime"/> items.</summary>
    public static (int Sari, int Ia) Fereastra(Marime lista, int? pagina, int? marime)
    {
        if (pagina is < 1) throw new CerereInvalida("Pagina începe de la 1.");
        if (marime is < 1 || marime > lista.Maxim) throw new CerereInvalida($"O pagină are între 1 și {lista.Maxim} elemente.");
        var n = marime ?? lista.Implicit;
        return (((pagina ?? 1) - 1) * n, n);
    }

    /// <summary>One page of <paramref name="q"/> (already ordered).</summary>
    public static IQueryable<T> Pagina<T>(this IQueryable<T> q, Marime lista, int? pagina, int? marime)
    {
        var (sari, ia) = Fereastra(lista, pagina, marime);
        return q.Skip(sari).Take(ia);
    }
}
