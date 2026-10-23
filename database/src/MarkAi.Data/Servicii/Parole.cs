using MarkAi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MarkAi.Data.Servicii;

/// <summary>PBKDF2 password hashes (ASP.NET Core Identity format), shared by the API and the seed tool.</summary>
public static class Parole
{
    private static PasswordHasher<Utilizator> Hasher = new();

    /// <summary>
    /// Tests only (never called by the app): fewer PBKDF2 iterations, so the hundreds of sign-ins of the test suite stay fast.
    /// A hash records its own iteration count, so hashes made either way keep verifying.
    /// </summary>
    internal static void PentruTeste(int iteratii) =>
        Hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = iteratii }));

    public static string Hash(Utilizator utilizator, string parola) => Hasher.HashPassword(utilizator, parola);

    public static bool Verifica(Utilizator utilizator, string parola) =>
        utilizator.ParolaHash.Length > 0
        && Hasher.VerifyHashedPassword(utilizator, utilizator.ParolaHash, parola) != PasswordVerificationResult.Failed;
}
