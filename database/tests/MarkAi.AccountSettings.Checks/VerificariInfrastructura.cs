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
        var creat = await admin.PostAsJsonAsync("/cursuri", new
        {
            materieId = 1, titlu = "Luceafărul", capitol = "Romantismul",
            materiale = new[] { new { titlu = "Despre poem", text = "„Luceafărul” de Mihai Eminescu a apărut în 1883." } },
        });
        h.Check(creat.StatusCode == HttpStatusCode.Created, "course created");
        var cursId = (await creat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        await Task.Delay(300);
        var stare = await admin.GetFromJsonAsync<JsonElement>($"/cursuri/{cursId}/generare");
        h.Check(stare.GetProperty("stare").GetString() == "in_asteptare" && h.Ai.Apeluri.IsEmpty, "question generation waits until a check runs the job");

        h.Ai.Raspunde = (_, _, _) => IntrebariAi;
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        stare = await admin.GetFromJsonAsync<JsonElement>($"/cursuri/{cursId}/generare");
        h.Check(stare.GetProperty("stare").GetString() == "gata" && stare.GetProperty("intrebariGenerate").GetInt32() == 1, "job run on demand generates questions");
        h.Check(h.Ai.Apeluri.TryPeek(out var apel) && apel.Scop == ScopAi.Intrebari && apel.Mesaje[0].Text.Contains("1883"),
            "fake AI received the lesson material");
        var intrebari = await admin.GetFromJsonAsync<JsonElement>($"/cursuri/{cursId}/intrebari/toate");
        h.Check(intrebari.GetProperty("elemente").GetArrayLength() == 2, "generated multiple-choice and estimate questions saved");
    }

    /// <summary>Runs with fake Twilio credentials: "Sună-mă" reaches the fake REST API, never Twilio.</summary>
    public static async Task RuleazaTwilio(Harness h)
    {
        using var elev = await h.Login("george");
        var salvat = await elev.PatchAsJsonAsync("/utilizatori/me/setari", new { nume = "George Pavel", username = "george", telefon = "0722111222" });
        h.Check(salvat.IsSuccessStatusCode, "student phone saved");
        var r = await elev.PostAsync("/linia-ai/suna-ma", null);
        h.Check(r.IsSuccessStatusCode, "call request accepted");
        h.Check(h.Twilio.Apeluri.TryPeek(out var apel) && apel["To"] == "+40722111222" && apel["Url"].StartsWith("https://markai.test/api/twilio/voce"),
            "outbound call sent to the fake Twilio API with the student's number");
        h.Check(h.Twilio.Apeluri.Count == 1, "exactly one call recorded");
    }
}
