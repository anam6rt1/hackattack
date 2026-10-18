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

    public string ParolaHash { get; set; } = "";

    public virtual ICollection<EvenimentRecompensa> EvenimenteRecompensa { get; set; } = new List<EvenimentRecompensa>();

    public virtual ICollection<Graf> Grafuri { get; set; } = new List<Graf>();

    public virtual ICollection<ProgresCurs> ProgresCursuri { get; set; } = new List<ProgresCurs>();

    public virtual PreferinteUtilizator? PreferinteUtilizator { get; set; }

    public virtual ProfilMentor? ProfilMentor { get; set; }

    public virtual ICollection<Rezervare> Rezervari { get; set; } = new List<Rezervare>();

    public virtual ICollection<Sesiune> Sesiuni { get; set; } = new List<Sesiune>();

    public virtual ICollection<UtilizatorMaterie> Materii { get; set; } = new List<UtilizatorMaterie>();

    public virtual ICollection<UtilizatorRealizare> Realizari { get; set; } = new List<UtilizatorRealizare>();

    public virtual ICollection<UtilizatorRol> Roluri { get; set; } = new List<UtilizatorRol>();
}

