using System;
using System.Collections.Generic;

namespace MarkAi.Data.Entities;

public partial class PreferinteUtilizator
{
    public int UtilizatorId { get; set; }

    public int? InterfataId { get; set; }

    public int? BackgroundStaticId { get; set; }

    public string ModBackground { get; set; } = null!;

    public string Timezone { get; set; } = null!;

    public DateTime ActualizatLa { get; set; }

    /// <summary>Phone tutor voice code (see the API's voice catalog); null = default voice.</summary>
    public string? VoceTutor { get; set; }

    /// <summary>Phone tutor speaking rate, percent of normal (70–130).</summary>
    public int VitezaTutor { get; set; } = 100;

    /// <summary>Phone tutor pitch shift, percent (−20…+20).</summary>
    public int TonTutor { get; set; }

    /// <summary>Profile avatar code (a character from the bac works, drawn by the frontend); null = default avatar.</summary>
    public string? Avatar { get; set; }

    /// <summary>Materiale shows only the Lecții not finished yet (quiz not passed); a student's choice, kept with the account.</summary>
    public bool AscundeLectiiTerminate { get; set; }

    public virtual Background? BackgroundStatic { get; set; }

    public virtual Interfata? Interfata { get; set; }

    public virtual Utilizator Utilizator { get; set; } = null!;
}

