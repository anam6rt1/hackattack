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

    /// <summary>Part of every session token; raising it signs the account out everywhere.</summary>
    public int VersiuneSesiune { get; set; }

    /// <summary>Null until the emailed code is confirmed; unconfirmed accounts cannot sign in and are deleted after a day.</summary>
    public DateTime? EmailConfirmatLa { get; set; }

    /// <summary>When the "your account will be deleted soon" email was sent, so it is sent only once.</summary>
    public DateTime? AvertizatLa { get; set; }

    /// <summary>Set when the phone number was confirmed with an SMS code; "Sună-mă" calls only verified numbers.</summary>
    public DateTime? TelefonVerificatLa { get; set; }

    /// <summary>When the "your application will be deleted tomorrow" reminder was sent (professors, organisation admins).</summary>
    public DateTime? AvertizatAprobareLa { get; set; }

    public int? JudetId { get; set; }

    public int? LocalitateId { get; set; }

    /// <summary>Students' school year: "IX", "X", "XI" or "XII".</summary>
    public string? Clasa { get; set; }

    public virtual ICollection<MembruOrganizatie> Organizatii { get; set; } = new List<MembruOrganizatie>();

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

