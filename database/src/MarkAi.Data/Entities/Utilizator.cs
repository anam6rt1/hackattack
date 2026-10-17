using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Utilizator
{
    public int Id { get; set; }

    public string Nume { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Telefon { get; set; }

    public DateTime CreatLa { get; set; }

    public virtual ICollection<EvenimentRecompensa> EvenimentRecompensas { get; set; } = new List<EvenimentRecompensa>();

    public virtual ICollection<Graf> Grafs { get; set; } = new List<Graf>();

    public virtual PreferinteUtilizator? PreferinteUtilizator { get; set; }

    public virtual ProfilMentor? ProfilMentor { get; set; }

    public virtual ICollection<Rezervare> Rezervares { get; set; } = new List<Rezervare>();

    public virtual ICollection<Sesiune> Sesiunes { get; set; } = new List<Sesiune>();

    public virtual ICollection<UtilizatorMaterie> UtilizatorMateries { get; set; } = new List<UtilizatorMaterie>();

    public virtual ICollection<UtilizatorRealizare> UtilizatorRealizares { get; set; } = new List<UtilizatorRealizare>();

    public virtual ICollection<UtilizatorRol> UtilizatorRols { get; set; } = new List<UtilizatorRol>();
}

