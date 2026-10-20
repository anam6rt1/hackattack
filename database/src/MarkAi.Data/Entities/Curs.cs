using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Curs
{
    public int Id { get; set; }

    public int MaterieId { get; set; }

    /// <summary>The path (shown as "Lecție") this chapter belongs to; always of the same subject.</summary>
    public int ParcursId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public string? Continut { get; set; }

    /// <summary>Recommended order inside the path; every chapter can be opened at any time.</summary>
    public int Ordine { get; set; }

    /// <summary>Optional chapter label shown under the island name, e.g. "Harap-Alb".</summary>
    public string? Capitol { get; set; }

    public string? VideoUrl { get; set; }

    public List<SectiuneMaterial> Materiale { get; set; } = new();

    public bool Publicat { get; set; } = true;

    public int? CreatDeId { get; set; }

    public DateTime CreatLa { get; set; }

    /// <summary>Title, chapter label and description in lower case without diacritics, computed by the database.</summary>
    public string Cautare { get; set; } = "";

    public virtual Utilizator? CreatDe { get; set; }

    public virtual ICollection<ProgresCurs> Progrese { get; set; } = new List<ProgresCurs>();

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<GrafCurs> GrafuriCurs { get; set; } = new List<GrafCurs>();

    public virtual Materie Materie { get; set; } = null!;

    public virtual Parcurs Parcurs { get; set; } = null!;

    public virtual ICollection<Sesiune> Sesiuni { get; set; } = new List<Sesiune>();

    public virtual ICollection<FisierCapitol> Fisiere { get; set; } = new List<FisierCapitol>();
}

