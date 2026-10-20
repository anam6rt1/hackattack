using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;

/// <summary>Lesson quiz attempts: answers never leak before the end, passing needs 4 of 5, a pause between attempts, XP once.</summary>
static class VerificariQuiz
{
    public static async Task Ruleaza(Harness h)
    {
        using var admin = await h.Login("admin");
        var creat = await admin.PostAsJsonAsync("/cursuri", new
        {
            materieId = 1, titlu = "Luceafărul", capitol = "Romantismul",
            materiale = new[] { new { titlu = "Despre poem", text = "„Luceafărul” de Mihai Eminescu a apărut în 1883." } },
        });
        var cursId = (await creat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        // Six generated questions; the right option is always the first one generated.
        h.Ai.Raspunde = (_, _, _) => JsonSerializer.Serialize(new
        {
            grila = Enumerable.Range(1, 6).Select(i => new { text = $"Întrebarea {i}", variante = new[] { "Răspunsul bun", "Altă variantă 1", "Altă variantă 2", "Altă variantă 3" }, corect = 0, explicatie = $"Explicația {i}" }),
            estimari = Array.Empty<object>(),
        });
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        var corecte = (await h.Db(db => db.Set<TriviaOption>().Where(o => o.IsCorrect).Select(o => o.Id).ToListAsync())).ToHashSet();

        using var elev = await h.Login("george");
        var raspunsVechi = await elev.PostAsJsonAsync($"/cursuri/{cursId}/raspuns", new { intrebareId = 1, variantaId = 1 });
        h.Check(raspunsVechi.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "old single-answer endpoint removed");

        var start = await elev.PostAsync($"/cursuri/{cursId}/quiz", null);
        var testText = await start.Content.ReadAsStringAsync();
        var test = JsonDocument.Parse(testText).RootElement;
        h.Check(start.IsSuccessStatusCode && test.GetProperty("total").GetInt32() == 5 && test.GetProperty("necesare").GetInt32() == 4,
            "attempt has 5 questions and needs 4 correct");
        h.Check(!testText.Contains("corect", StringComparison.OrdinalIgnoreCase) && !testText.Contains("explicati", StringComparison.OrdinalIgnoreCase),
            "attempt reveals no correct answers or explanations");
        var id = test.GetProperty("id").GetInt32();
        var intrebari = test.GetProperty("intrebari").EnumerateArray().ToList();

        // A wrong answer only says it was wrong.
        var prima = intrebari[0];
        var gresita = prima.GetProperty("variante").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).First(v => !corecte.Contains(v));
        var r = await elev.PostAsJsonAsync($"/cursuri/{cursId}/quiz/{id}/raspuns", new { intrebareId = prima.GetProperty("id").GetInt32(), variantaId = gresita });
        var rText = await r.Content.ReadAsStringAsync();
        var raspuns = JsonDocument.Parse(rText).RootElement;
        h.Check(!raspuns.GetProperty("corect").GetBoolean() && raspuns.GetProperty("ramase").GetInt32() == 4
            && raspuns.GetProperty("final").ValueKind == JsonValueKind.Null && !rText.Contains("variantaCorecta"), "wrong answer reveals only that it was wrong");
        r = await elev.PostAsJsonAsync($"/cursuri/{cursId}/quiz/{id}/raspuns", new { intrebareId = prima.GetProperty("id").GetInt32(), variantaId = gresita });
        h.Check(r.StatusCode == HttpStatusCode.Conflict, "a question cannot be answered twice");

        // Starting again while unfinished resumes the same attempt, without the answered question.
        var reluat = await (await elev.PostAsync($"/cursuri/{cursId}/quiz", null)).Content.ReadFromJsonAsync<JsonElement>();
        h.Check(reluat.GetProperty("id").GetInt32() == id && reluat.GetProperty("intrebari").GetArrayLength() == 4, "unfinished attempt is resumed");

        // 1 wrong + 2 more wrong + 2 right = 2/5: not passed, corrections revealed, no XP.
        var final = await Raspunde(elev, cursId, id, intrebari.Skip(1).ToList(), corecte, nrCorecte: 2);
        h.Check(!final.GetProperty("promovat").GetBoolean() && final.GetProperty("corecte").GetInt32() == 2 && final.GetProperty("xpCastigat").GetInt32() == 0,
            "2 of 5 does not pass and gives no XP");
        h.Check(final.GetProperty("corectii").EnumerateArray().All(c => corecte.Contains(c.GetProperty("variantaCorectaId").GetInt32())
            && c.GetProperty("explicatie").GetString()!.StartsWith("Explicația")), "corrections and explanations revealed at the end");

        var preaRepede = await elev.PostAsync($"/cursuri/{cursId}/quiz", null);
        h.Check(preaRepede.StatusCode == HttpStatusCode.Conflict, "a new attempt right away is refused (pause)");
        h.Ceas.Advance(TimeSpan.FromSeconds(61));

        // 4 of 5: passes, 100 XP.
        var a2 = await (await elev.PostAsync($"/cursuri/{cursId}/quiz", null)).Content.ReadFromJsonAsync<JsonElement>();
        final = await Raspunde(elev, cursId, a2.GetProperty("id").GetInt32(), a2.GetProperty("intrebari").EnumerateArray().ToList(), corecte, nrCorecte: 4);
        h.Check(final.GetProperty("promovat").GetBoolean() && final.GetProperty("xpCastigat").GetInt32() == 100, "4 of 5 passes and gives 100 XP");

        // Passing again gives no more XP.
        h.Ceas.Advance(TimeSpan.FromSeconds(61));
        var a3 = await (await elev.PostAsync($"/cursuri/{cursId}/quiz", null)).Content.ReadFromJsonAsync<JsonElement>();
        final = await Raspunde(elev, cursId, a3.GetProperty("id").GetInt32(), a3.GetProperty("intrebari").EnumerateArray().ToList(), corecte, nrCorecte: 5);
        h.Check(final.GetProperty("promovat").GetBoolean() && final.GetProperty("xpCastigat").GetInt32() == 0, "XP is awarded only once per lesson");

        // Someone else's attempt cannot be answered.
        using var altCont = await h.Login("admin");
        r = await altCont.PostAsJsonAsync($"/cursuri/{cursId}/quiz/{a3.GetProperty("id").GetInt32()}/raspuns", new { intrebareId = 1, variantaId = 1 });
        h.Check(!r.IsSuccessStatusCode, "another account cannot answer this attempt");
    }

    // Answers the given questions in order: the first nrCorecte right, the rest wrong. Returns the final result.
    static async Task<JsonElement> Raspunde(HttpClient elev, int cursId, int id, List<JsonElement> intrebari, HashSet<int> corecte, int nrCorecte)
    {
        JsonElement ultim = default;
        foreach (var (q, i) in intrebari.Select((q, i) => (q, i)))
        {
            var variante = q.GetProperty("variante").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).ToList();
            var alesa = i < nrCorecte ? variante.First(corecte.Contains) : variante.First(v => !corecte.Contains(v));
            var r = await elev.PostAsJsonAsync($"/cursuri/{cursId}/quiz/{id}/raspuns", new { intrebareId = q.GetProperty("id").GetInt32(), variantaId = alesa });
            ultim = await r.Content.ReadFromJsonAsync<JsonElement>();
        }
        return ultim.GetProperty("final");
    }
}
