using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Rezervare
{
    public int Id { get; set; }

    public int CameraId { get; set; }

    public int ElevId { get; set; }

    public string Status { get; set; } = null!;

    public decimal TarifAchitat { get; set; }

    public string Moneda { get; set; } = null!;

    public DateTime RezervataLa { get; set; }

    public DateTime? FinalizataLa { get; set; }

    public virtual CameraTutoring Camera { get; set; } = null!;

    public virtual ComisionMentor? ComisionMentor { get; set; }

    public virtual Utilizator Elev { get; set; } = null!;

    public virtual ICollection<Plata> Plata { get; set; } = new List<Plata>();

    public virtual ICollection<Sesiune> Sesiunes { get; set; } = new List<Sesiune>();
}

