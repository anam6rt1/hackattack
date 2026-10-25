using System.Net;
using System.Security.Cryptography;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using static MarkAi.Tests.Suport.DocumenteTest;

namespace MarkAi.Tests.Integration.Fisiere;

public sealed class IncarcariReluabileTests : TestFisiere
{
    private async Task<string> Porneste(byte[] date, string? hash = null) => (await irina.Post($"/parcursuri/{lectie}/incarcari", new
    {
        nume = "material.pdf", marime = date.Length, sha256 = hash ?? Convert.ToHexStringLower(SHA256.HashData(date)),
    }).Json(HttpStatusCode.OK)).GetProperty("token").GetString()!;

    [Fact]
    public async Task ReluareaPornesteDeLaUltimaBucataSiFinalizareaEsteIdempotenta()
    {
        var date = Pdf(umplutura: IncarcariReluabile.Bucata + 2000);
        var token = await Porneste(date);
        await irina.PutAsync($"/incarcari/{token}?offset=0", new ByteArrayContent(date[..IncarcariReluabile.Bucata]), Ct).Status(HttpStatusCode.OK);
        (await irina.GetJson($"/incarcari/{token}")).Int("offset").ShouldBe(IncarcariReluabile.Bucata);
        await irina.PostAsync($"/incarcari/{token}/finalizeaza", null, Ct).Status(HttpStatusCode.BadRequest);
        await irina.PutAsync($"/incarcari/{token}?offset=0", new ByteArrayContent([1]), Ct).Status(HttpStatusCode.Conflict);
        await director.Get($"/incarcari/{token}").Status(HttpStatusCode.NotFound);
        await irina.PutAsync($"/incarcari/{token}?offset={IncarcariReluabile.Bucata}", new ByteArrayContent(date[IncarcariReluabile.Bucata..]), Ct).Status(HttpStatusCode.OK);
        var id = (await irina.PostAsync($"/incarcari/{token}/finalizeaza", null, Ct).Json(HttpStatusCode.Created)).Int("id");
        (await irina.PostAsync($"/incarcari/{token}/finalizeaza", null, Ct).Json(HttpStatusCode.OK)).Int("id").ShouldBe(id);
        (await irina.GetByteArrayAsync($"/fisiere/{id}?descarca=true", Ct)).ShouldBe(date);
        await irina.Delete($"/incarcari/{token}").Status(HttpStatusCode.NoContent);
        await irina.Get($"/incarcari/{token}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VerificaIntegritateaSiLimitaBucatilorInainteDeSalvare()
    {
        var date = Pdf();
        var token = await Porneste(date, new string('0', 64));
        await irina.PutAsync($"/incarcari/{token}?offset=0", new ByteArrayContent(new byte[IncarcariReluabile.Bucata + 1]), Ct).Status(HttpStatusCode.BadRequest);
        (await irina.GetJson($"/incarcari/{token}")).Int("offset").ShouldBe(0);
        await irina.PutAsync($"/incarcari/{token}?offset=0", new ByteArrayContent(date), Ct).Status(HttpStatusCode.OK);
        await irina.PostAsync($"/incarcari/{token}/finalizeaza", null, Ct).Status(HttpStatusCode.BadRequest);
        (await h.Db(db => db.Set<FisierLectie>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task DouaLectiiImpartAceeasiCopieIarStergereaUneiaNuOInvalideaza()
    {
        var date = Pdf();
        var primul = await IncarcaId(irina, date, "primul.pdf");
        var alta = (await irina.Post("/parcursuri", new { materieId = 1, titlu = "Altă lecție", clasa = "XII", vizibilitate = "organizatie", organizatieId = MarkAi.Tests.Suport.Harness.IdLiceu }).Json(HttpStatusCode.Created)).Int("id");
        var alDoilea = (await Incarca(irina, date, "al doilea.pdf", parcurs: alta).Json(HttpStatusCode.Created)).Int("id");
        (await Cheie(primul)).ShouldBe(await Cheie(alDoilea));
        Stocare.Chei(Stocare.DirectorLectii).Count().ShouldBe(1);
        await irina.Delete($"/fisiere/{primul}").Status(HttpStatusCode.NoContent);
        await Procesor.RuleazaOdataAsync(Ct);
        (await irina.GetByteArrayAsync($"/fisiere/{alDoilea}?descarca=true", Ct)).ShouldBe(date);
        await george.Get($"/fisiere/{alDoilea}").Status(HttpStatusCode.NotFound);
    }
}
