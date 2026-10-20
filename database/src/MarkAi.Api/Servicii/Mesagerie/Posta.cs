using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii.Mesagerie;

/// <summary>
/// The app's outgoing mail and SMS. Codes are sent at once (the person is waiting, and must be told if it failed);
/// notifications are queued with the change that caused them and sent by <see cref="ProcesorMesaje"/>, with retries.
/// Every message is recorded in <c>mesaj_iesire</c>.
/// </summary>
public sealed class Posta(MarkAiDbContext db, IServiceScopeFactory scopes, IExpeditorEmail email, IExpeditorSms sms, IConfiguration cfg, TimeProvider ceas)
{
    /// <summary>Queues a notification; it is saved by the caller's next SaveChanges, together with the change it reports.</summary>
    public void Pune(Email m, string tip) => db.Add(new MesajIesire
    {
        Canal = "email", Catre = m.Catre, Subiect = m.Subiect, Html = m.Html, Text = m.Text, Tip = tip,
        CreatLa = Acum(), UrmatoareaIncercareLa = Acum(),
    });

    public async Task TrimiteAcumAsync(Email m, string tip, CancellationToken ct)
    {
        var rand = new MesajIesire { Canal = "email", Catre = m.Catre, Subiect = m.Subiect, Tip = tip, CreatLa = Acum(), UrmatoareaIncercareLa = Acum(), Furnizor = email.Nume };
        if (email.Nume == "jurnal") (rand.Html, rand.Text) = (m.Html, m.Text);
        await TrimiteSiInregistreaza(rand, () => email.TrimiteAsync(m, ct), ct);
    }

    /// <summary>Sends an SMS now, within the daily SMS cap (<c>Sms:MaxPeZi</c>, every SMS costs money).</summary>
    public async Task TrimiteAcumAsync(Sms m, string tip, CancellationToken ct)
    {
        var deLa = Acum().AddHours(-24);
        if (await db.Set<MesajIesire>().CountAsync(x => x.Canal == "sms" && x.CreatLa >= deLa && x.Eroare == null, ct) >= cfg.GetValue("Sms:MaxPeZi", 20))
            throw new Conflict("S-a atins limita zilnică de SMS-uri. Încearcă din nou mâine.");
        var rand = new MesajIesire { Canal = "sms", Catre = m.Catre, Tip = tip, CreatLa = Acum(), UrmatoareaIncercareLa = Acum(), Furnizor = sms.Nume };
        if (sms.Nume == "jurnal") rand.Text = m.Text;
        await TrimiteSiInregistreaza(rand, () => sms.TrimiteAsync(m, ct), ct);
    }

    private async Task TrimiteSiInregistreaza(MesajIesire rand, Func<Task> trimite, CancellationToken ct)
    {
        try
        {
            await trimite();
            rand.TrimisLa = Acum();
        }
        catch (TrimitereEsuata ex) { rand.Eroare = ex.Message; throw; }
        finally
        {
            // Recorded even when sending failed, in its own save so the caller's pending changes stay theirs.
            using var scope = scopes.CreateScope();
            var separat = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
            separat.Add(rand);
            await separat.SaveChangesAsync(CancellationToken.None);
        }
    }

    private DateTime Acum() => ceas.GetUtcNow().UtcDateTime;
}

/// <summary>Sends queued notifications every few seconds; a failure is retried later (1, 5, 15, 60, 240 minutes), then given up.</summary>
public sealed class ProcesorMesaje(IServiceScopeFactory scopes, TimeProvider ceas, ILogger<ProcesorMesaje> log) : BackgroundService
{
    private static readonly int[] Asteptari = [1, 5, 15, 60, 240];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Sending queued messages failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), ceas, ct);
        }
    }

    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IExpeditorEmail>();
        var acum = ceas.GetUtcNow().UtcDateTime;
        var deTrimis = await db.Set<MesajIesire>()
            .Where(m => m.TrimisLa == null && m.UrmatoareaIncercareLa <= acum && m.Incercari <= Asteptari.Length && m.Canal == "email")
            .OrderBy(m => m.Id).Take(20).ToListAsync(ct);
        foreach (var m in deTrimis)
        {
            try
            {
                await email.TrimiteAsync(new Email(m.Catre, m.Subiect ?? "", m.Html ?? "", m.Text ?? ""), ct);
                (m.TrimisLa, m.Furnizor, m.Eroare) = (acum, email.Nume, null);
                // Delivered for real: the content (which may hold a code) is no longer needed here.
                if (email.Nume != "jurnal") (m.Html, m.Text) = (null, null);
            }
            catch (TrimitereEsuata ex)
            {
                m.Eroare = ex.Message;
                m.UrmatoareaIncercareLa = acum.AddMinutes(Asteptari[Math.Min(m.Incercari, Asteptari.Length - 1)]);
                m.Incercari++;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
