using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Servicii;

public sealed class LocuriEpuizateException() : InvalidOperationException("Nu mai sunt locuri libere la această ședință.");

/// <summary>
/// Booking a tutoring slot. The slot row is locked so concurrent bookings cannot exceed its capacity.
/// Students pay the slot's fixed price (or nothing when pro bono); the mentor's share is the fee
/// negotiated with the platform, snapshotted on the booking.
/// </summary>
public sealed class RezervareService(MarkAiDbContext db)
{
    public const string SlotDeschis = "deschisa";
    public const string RezervareConfirmata = "confirmata";
    public const string RezervareAnulata = "anulata";
    public const string MentorAprobat = "aprobat";

    public async Task<Rezervare> RezervaAsync(int elevId, int slotId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM mark_ai.camera_tutoring WHERE id = {slotId} FOR UPDATE", ct);
        var slot = await db.CamereTutoring.Include(c => c.Tarif).Include(c => c.Mentor)
            .SingleOrDefaultAsync(c => c.Id == slotId, ct) ?? throw new KeyNotFoundException("Ședința nu există.");
        var acum = await db.Database.SqlQuery<DateTime>($"SELECT now() AS \"Value\"").SingleAsync(ct);
        if (slot.Status != SlotDeschis || slot.Mentor.StareVerificare != MentorAprobat || slot.InceputLa is null || slot.InceputLa <= acum)
            throw new InvalidOperationException("Ședința nu mai acceptă rezervări.");
        if (slot.MentorId == elevId) throw new InvalidOperationException("Nu îți poți rezerva propria ședință.");

        var active = db.Rezervari.Where(r => r.CameraId == slotId && r.Status == RezervareConfirmata);
        if (await active.AnyAsync(r => r.ElevId == elevId, ct)) throw new InvalidOperationException("Ai rezervat deja această ședință.");
        if (await active.CountAsync(ct) >= slot.Capacitate) throw new LocuriEpuizateException();

        var pret = slot.ProBono ? 0m : slot.Tarif!.Suma;
        var moneda = slot.Tarif?.Moneda ?? slot.Mentor.Moneda;
        var rezervare = new Rezervare
        {
            CameraId = slotId, ElevId = elevId, Status = RezervareConfirmata, TarifAchitat = pret, Moneda = moneda,
        };
        db.Rezervari.Add(rezervare);
        if (!slot.ProBono)
        {
            // Payment is simulated for now; a real processor would confirm it asynchronously.
            rezervare.Plati.Add(new Plata { Suma = pret, Moneda = moneda, Status = "simulata", Procesator = "simulat", PlatitaLa = acum });
            var sumaMentor = Math.Min(slot.Mentor.OnorariuNegociat, pret);
            rezervare.ComisionMentor = new ComisionMentor
            {
                MentorId = slot.MentorId, TarifBaza = pret, SumaComision = sumaMentor, Moneda = moneda, Status = "in_asteptare",
                ProcentSnapshot = pret == 0 ? 0 : Math.Round(sumaMentor / pret * 100, 2),
            };
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rezervare;
    }

    public async Task AnuleazaAsync(int elevId, int rezervareId, CancellationToken ct = default)
    {
        var r = await db.Rezervari.Include(x => x.ComisionMentor)
            .SingleOrDefaultAsync(x => x.Id == rezervareId && x.ElevId == elevId, ct) ?? throw new KeyNotFoundException("Rezervarea nu există.");
        if (r.Status != RezervareConfirmata) return;
        r.Status = RezervareAnulata;
        if (r.ComisionMentor is not null) r.ComisionMentor.Status = "anulat";
        await db.SaveChangesAsync(ct);
    }
}
