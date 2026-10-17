using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class Sesiune
{
    public int Id { get; set; }

    public int UtilizatorId { get; set; }

    public int? CursId { get; set; }

    public int? RezervareId { get; set; }

    public string Canal { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? TwilioCallSid { get; set; }

    public DateTime InceputLa { get; set; }

    public DateTime? FinalizatLa { get; set; }

    public virtual Curs? Curs { get; set; }

    public virtual ICollection<EvenimentRecompensa> EvenimentRecompensas { get; set; } = new List<EvenimentRecompensa>();

    public virtual ICollection<Mesaj> Mesajs { get; set; } = new List<Mesaj>();

    public virtual Rezervare? Rezervare { get; set; }

    public virtual ICollection<SmsLog> SmsLogs { get; set; } = new List<SmsLog>();

    public virtual ICollection<TablaPas> TablaPas { get; set; } = new List<TablaPas>();

    public virtual Utilizator Utilizator { get; set; } = null!;
}

