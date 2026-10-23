namespace MarkAi.Data.Entities;

/// <summary>
/// A question of a Lecție's quiz (also drawn by Triviador for the Lecție's island). Questions without a Lecție belong to the
/// whole subject and are used only by Triviador.
/// </summary>
public class Intrebare
{
    public const string Grila = "grila", Estimare = "estimare";
    public const string SursaAi = "ai", SursaManual = "manual";

    public int Id { get; set; }

    public int MaterieId { get; set; }

    public int? ParcursId { get; set; }

    public string Text { get; set; } = "";

    public bool Activa { get; set; } = true;

    /// <summary>"grila" = multiple choice (options in <see cref="Variante"/>); "estimare" = numeric tie-break answer for Triviador.</summary>
    public string Tip { get; set; } = Grila;

    public int? RaspunsNumeric { get; set; }

    public string? Explicatie { get; set; }

    /// <summary>"ai" = generated from the Lecție's material; "manual" = written, edited or imported by a person.</summary>
    public string Sursa { get; set; } = SursaManual;

    public DateTime CreataLa { get; set; }

    public virtual Parcurs? Parcurs { get; set; }

    public virtual ICollection<VariantaIntrebare> Variante { get; set; } = new List<VariantaIntrebare>();
}

public class VariantaIntrebare
{
    public int Id { get; set; }

    public int IntrebareId { get; set; }

    public string Text { get; set; } = "";

    public bool Corecta { get; set; }

    public virtual Intrebare Intrebare { get; set; } = null!;
}
