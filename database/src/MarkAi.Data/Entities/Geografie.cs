namespace MarkAi.Data.Entities;

/// <summary>A county (or Bucharest), from SIRUTA; <see cref="Id"/> is the SIRUTA county number (1–52).</summary>
public class Judet
{
    public int Id { get; set; }

    public string Nume { get; set; } = null!;

    public virtual ICollection<Localitate> Localitati { get; set; } = new List<Localitate>();
}

/// <summary>
/// A locality (village, town district, Bucharest sector) from SIRUTA, with the municipality, town or commune it belongs to:
/// "same city" means the same <see cref="UatId"/>.
/// </summary>
public class Localitate
{
    /// <summary>The SIRUTA code.</summary>
    public int Id { get; set; }

    public int JudetId { get; set; }

    public string Nume { get; set; } = null!;

    /// <summary>SIRUTA code of the municipality, town or commune.</summary>
    public int UatId { get; set; }

    /// <summary>E.g. "Municipiul Alba Iulia", "Comuna Ciugud", shown to tell same-named villages apart.</summary>
    public string UatNume { get; set; } = null!;

    public bool Urban { get; set; }

    /// <summary>The name in lower case without diacritics, for search.</summary>
    public string Cautare { get; set; } = null!;

    public virtual Judet Judet { get; set; } = null!;
}
