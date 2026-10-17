using MarkAi.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarkAi.Data.Servicii;

/// <summary>PBKDF2 password hashes (ASP.NET Core Identity format), shared by the API and the seed tool.</summary>
public static class Parole
{
    private static readonly PasswordHasher<Utilizator> Hasher = new();

    public static string Hash(Utilizator utilizator, string parola) => Hasher.HashPassword(utilizator, parola);

    public static bool Verifica(Utilizator utilizator, string parola) =>
        utilizator.ParolaHash.Length > 0
        && Hasher.VerifyHashedPassword(utilizator, utilizator.ParolaHash, parola) != PasswordVerificationResult.Failed;
}
