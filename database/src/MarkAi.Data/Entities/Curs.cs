using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Curs
{
    public int Id { get; set; }

    public int MaterieId { get; set; }

    public string Titlu { get; set; } = null!;

    public string? Descriere { get; set; }

    public string? Continut { get; set; }

    public int Ordine { get; set; }

    /// <summary>Gruparea vizuala (insula) din care face parte cursul, de ex. "Harap-Alb".</summary>
    public string? Capitol { get; set; }

    public string? VideoUrl { get; set; }

    public List<SectiuneMaterial> Materiale { get; set; } = new();

    public bool Publicat { get; set; } = true;

    public int? CreatDeId { get; set; }

    public DateTime CreatLa { get; set; }

    public virtual Utilizator? CreatDe { get; set; }

    public virtual ICollection<ProgresCurs> Progrese { get; set; } = new List<ProgresCurs>();

    public virtual ICollection<CameraTutoring> CamereTutoring { get; set; } = new List<CameraTutoring>();

    public virtual ICollection<GrafCurs> GrafuriCurs { get; set; } = new List<GrafCurs>();

    public virtual Materie Materie { get; set; } = null!;

    public virtual ICollection<Sesiune> Sesiuni { get; set; } = new List<Sesiune>();
}

