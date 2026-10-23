namespace MarkAi.Api.Infrastructura;

/// <summary>
/// How many subjects and organisations a professor may pick (the site's pickers stop at the same numbers, frontend/lib/alegeri.js).
/// Four covers every sensible case and keeps the chosen chips from pushing the rest of a form down.
/// </summary>
public static class Alegeri
{
    /// <summary>Subjects a professor teaches (sign-up and Setări).</summary>
    public const int MateriiProfesor = 4;

    /// <summary>Organisations a professor teaches in or waits for (sign-up and later join requests).</summary>
    public const int OrganizatiiProfesor = 4;

    /// <summary>Organisations a Lecție is shown in besides its own (view-only shares).</summary>
    public const int PartajariLectie = 4;
}
