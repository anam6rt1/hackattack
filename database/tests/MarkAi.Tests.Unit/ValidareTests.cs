using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;

namespace MarkAi.Tests.Unit;

/// <summary>Input checks shared by every endpoint: required and optional text, Romanian phone numbers, the school year.</summary>
public sealed class ValidareTests
{
    [Theory]
    [InlineData("0722 123 456", "+40722123456")]
    [InlineData("0722-123-456", "+40722123456")]
    [InlineData("(0722) 123.456", "+40722123456")]
    [InlineData("+40722123456", "+40722123456")]
    [InlineData("0040722123456", "+40722123456")]
    [InlineData("40722123456", "+40722123456")]
    [InlineData("+4915888623971", "+4915888623971")]
    [InlineData(" 0766111222 ", "+40766111222")]
    public void TelefonulEsteAdusLaE164(string intrare, string asteptat) => Validare.Telefon(intrare).ShouldBe(asteptat);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TelefonulGolEsteNull(string? intrare) => Validare.Telefon(intrare).ShouldBeNull();

    [Theory]
    [InlineData("12")]
    [InlineData("07abc")]
    [InlineData("+40 7661112223334445")]
    [InlineData("+4+0722123456")]
    [InlineData("0")]
    public void TelefonulNevalidEsteRefuzat(string intrare) =>
        Should.Throw<CerereInvalida>(() => Validare.Telefon(intrare)).Message.ShouldContain("nu este valid");

    [Fact]
    public void ObligatoriuTaieSpatiile() => Validare.Obligatoriu("  Ana  ", "Nume").ShouldBe("Ana");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ObligatoriuRefuzaGolul(string? valoare) =>
        Should.Throw<CerereInvalida>(() => Validare.Obligatoriu(valoare, "Nume")).Message.ShouldBe("Câmpul „Nume” este obligatoriu.");

    [Fact]
    public void ObligatoriuRefuzaTextulPreaLung() =>
        Should.Throw<CerereInvalida>(() => Validare.Obligatoriu(new string('a', 11), "Titlu", 10)).Message.ShouldBe("Câmpul „Titlu” are cel mult 10 caractere.");

    [Fact]
    public void ObligatoriuAcceptaExactLimita() => Validare.Obligatoriu(new string('a', 10), "Titlu", 10).Length.ShouldBe(10);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OptionalGolEsteNull(string? valoare) => Validare.Optional(valoare).ShouldBeNull();

    [Fact]
    public void OptionalTaieSpatiile() => Validare.Optional(" x ").ShouldBe("x");

    [Fact]
    public void OptionalRefuzaTextulPreaLung() => Should.Throw<CerereInvalida>(() => Validare.Optional(new string('a', 5), 4));

    [Theory]
    [InlineData("ix", "IX")]
    [InlineData(" XII ", "XII")]
    [InlineData("x", "X")]
    [InlineData("Xi", "XI")]
    public void ClasaEsteNormalizata(string intrare, string asteptat) => AutentificareEndpoints.Clasa(intrare).ShouldBe(asteptat);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ClasaGoalaEsteNull(string? intrare) => AutentificareEndpoints.Clasa(intrare).ShouldBeNull();

    [Theory]
    [InlineData("VIII")]
    [InlineData("XIII")]
    [InlineData("Bac")]
    [InlineData("12")]
    public void ClasaNecunoscutaEsteRefuzata(string intrare) => Should.Throw<CerereInvalida>(() => AutentificareEndpoints.Clasa(intrare));
}
