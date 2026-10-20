using System.Net;

namespace MarkAi.Api.Servicii.Mesagerie;

/// <summary>The app's emails and SMS texts, in Romanian, in the same card style as the account-change emails.</summary>
public static class Sabloane
{
    public const string Aplicatie = "TeleBac";

    public static Email ConfirmareCont(string catre, string nume, string cod, int ore) => Card(catre,
        $"{Aplicatie} · Codul de confirmare a contului",
        $"Bun venit, {Prenume(nume)}!",
        $"Ca să intri în cont, scrie pe site acest cod:",
        cod,
        $"Codul e valabil {ore} de ore. Dacă nu confirmi contul până atunci, îl ștergem automat, împreună cu datele lui.",
        "Dacă nu tu ți-ai făcut cont, ignoră acest email: contul va fi șters.");

    public static Email AvertizareStergere(string catre, string nume, string cod) => Card(catre,
        $"{Aplicatie} · Contul tău se șterge în curând",
        $"{Prenume(nume)}, contul tău nu e confirmat încă",
        "Dacă nu îl confirmi în aproximativ o oră, contul și datele lui vor fi șterse. Codul tău nou:",
        cod,
        "Scrie codul pe site, la intrarea în cont. Codurile trimise înainte nu mai sunt valabile.",
        "Dacă nu tu ți-ai făcut cont, nu trebuie să faci nimic.");

    public static Email ContSters(string catre, string nume, string motiv) => Card(catre,
        $"{Aplicatie} · Contul tău a fost șters",
        $"{Prenume(nume)}, contul tău a fost șters",
        motiv,
        null,
        "Îți poți face oricând un cont nou cu aceeași adresă de email.",
        null);

    public static Email IncercareInregistrare(string catre, string nume) => Card(catre,
        $"{Aplicatie} · Cineva a încercat să își facă un cont cu emailul tău",
        $"Salut, {Prenume(nume)}!",
        "Cineva a încercat să își facă un cont nou cu această adresă de email. Ai deja un cont, așa că nu am creat altul.",
        null,
        "Dacă ai fost tu, intră în contul existent cu emailul sau numele de utilizator.",
        "Dacă nu ai fost tu, nu trebuie să faci nimic: contul tău e în siguranță.");

    public static Email ResetareParola(string catre, string nume, string cod) => Card(catre,
        $"{Aplicatie} · Codul pentru o parolă nouă",
        $"{Prenume(nume)}, ai cerut o parolă nouă",
        "Scrie acest cod pe site, împreună cu parola nouă:",
        cod,
        "Codul expiră în 15 minute și poate fi folosit o singură dată. După schimbare, contul e scos de pe toate dispozitivele.",
        "Dacă nu tu ai cerut o parolă nouă, ignoră acest email: parola ta rămâne aceeași.");

    public static Email ParolaSchimbata(string catre, string nume) => Card(catre,
        $"{Aplicatie} · Parola contului a fost schimbată",
        $"{Prenume(nume)}, parola ta a fost schimbată",
        "Parola contului tău a fost schimbată cu un cod de resetare, iar toate sesiunile deschise au fost închise.",
        null,
        "Dacă ai fost tu, nu trebuie să faci nimic.",
        "Dacă nu ai fost tu, cere imediat o parolă nouă din pagina de intrare și verifică dacă emailul tău e în siguranță.");

    public static Sms CodParola(string catre, string cod) =>
        new(catre, $"{Aplicatie}: codul pentru o parolă nouă este {cod}. Expiră în 15 minute. Dacă nu l-ai cerut, ignoră mesajul.");

    public static Sms CodTelefon(string catre, string cod) =>
        new(catre, $"{Aplicatie}: codul tău de verificare a telefonului este {cod}. Expiră în 10 minute. Nu îl da nimănui.");

    /// <summary>A plain notification (approvals, requests, reminders) in the same card style.</summary>
    public static Email Notificare(string catre, string subiect, string titlu, string text, string? nota = null) =>
        Card(catre, $"{Aplicatie} · {subiect}", titlu, text, null, nota ?? "Detaliile sunt în aplicație, după ce intri în cont.", null);

    private static string Prenume(string nume) => nume.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nume;

    private static Email Card(string catre, string subiect, string titlu, string text, string? cod, string nota, string? nota2)
    {
        string H(string s) => WebUtility.HtmlEncode(s);
        var html = $"""
            <div style="background:#eaf5fa;padding:32px;font-family:Arial,sans-serif;color:#15305a">
            <div style="max-width:480px;margin:auto;background:white;border-radius:24px;padding:32px;border-bottom:6px solid #0d5c9e">
            <h2 style="color:#6b3410">{Aplicatie}</h2><h3>{H(titlu)}</h3>
            <p>{H(text)}</p>
            {(cod is null ? "" : $"""<p style="background:#fff8dc;border:2px solid #ffc61a;border-radius:16px;padding:20px;font-size:32px;letter-spacing:8px;text-align:center">{H(cod)}</p>""")}
            <p>{H(nota)}</p>
            {(nota2 is null ? "" : $"<p style=\"color:#5a6684\">{H(nota2)}</p>")}
            </div></div>
            """;
        var textSimplu = string.Join("\n\n", new[] { titlu, text, cod, nota, nota2 }.Where(x => x is not null));
        return new Email(catre, subiect, html, textSimplu);
    }
}
