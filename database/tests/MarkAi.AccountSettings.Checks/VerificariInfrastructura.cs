using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;

/// <summary>The harness itself: the fakes are the ones the app uses, and background jobs only run when a check runs them.</summary>
static class VerificariInfrastructura
{
    private const string IntrebariAi = """
        {"grila":[{"text":"Cine a scris „Luceafărul”?","variante":["Mihai Eminescu","Ion Creangă","George Coșbuc","Lucian Blaga"],"corect":0,"explicatie":"Poemul e scris de Eminescu."}],
         "estimari":[{"text":"În ce an a apărut „Luceafărul”?","raspuns":1883}]}
        """;

    public static async Task Ruleaza(Harness h)
    {
        h.Check(h.Serviciu<ModelAi>().Activ is FakeAi, "app uses the fake AI model");
        h.Check(ReferenceEquals(h.Serviciu<TimeProvider>(), h.Ceas), "app uses the fake clock");

        using var admin = await h.Login("admin");
        var lectie = await Harness.LectiePlatforma(admin);
        var creat = await admin.PostAsJsonAsync("/cursuri", new
        {
            parcursId = lectie, titlu = "Luceafărul", capitol = "Romantismul",
            materiale = new[] { new { titlu = "Despre poem", text = "„Luceafărul” de Mihai Eminescu a apărut în 1883." } },
        });
        h.Check(creat.StatusCode == HttpStatusCode.Created, "course created");

        await Task.Delay(300);
        var stare = await admin.GetFromJsonAsync<JsonElement>($"/parcursuri/{lectie}/generare");
        h.Check(stare.GetProperty("stare").GetString() == "necunoscuta" && h.Ai.Apeluri.IsEmpty, "creating a chapter generates no questions by itself");

        var cerere = await admin.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 1 });
        h.Check(cerere.StatusCode == HttpStatusCode.Accepted, "the professor asks for questions");
        stare = await admin.GetFromJsonAsync<JsonElement>($"/parcursuri/{lectie}/generare");
        h.Check(stare.GetProperty("stare").GetString() == "in_asteptare" && h.Ai.Apeluri.IsEmpty, "question generation waits until a check runs the job");

        h.Ai.Raspunde = (_, _, _) => IntrebariAi;
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        stare = await admin.GetFromJsonAsync<JsonElement>($"/parcursuri/{lectie}/generare");
        h.Check(stare.GetProperty("stare").GetString() == "gata" && stare.GetProperty("intrebariGenerate").GetInt32() == 1, "job run on demand generates questions");
        h.Check(h.Ai.Apeluri.TryPeek(out var apel) && apel.Scop == ScopAi.Intrebari && apel.Mesaje[0].Text.Contains("1883"),
            "fake AI received the Lecție's material");
        var intrebari = await admin.GetFromJsonAsync<JsonElement>($"/continut/parcursuri/{lectie}/intrebari");
        h.Check(intrebari.GetProperty("elemente").GetArrayLength() == 2, "generated multiple-choice and estimate questions saved");
    }
}
