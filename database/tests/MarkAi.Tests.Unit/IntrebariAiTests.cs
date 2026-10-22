using MarkAi.Api.Servicii;

namespace MarkAi.Tests.Unit;

/// <summary>
/// The checks on AI-proposed questions (<see cref="CalitateIntrebari"/>): questions about the document itself are recognised
/// without catching real subject questions that happen to use the same words, and near-duplicates are recognised across
/// diacritics, word order and punctuation without merging questions about different things.
/// </summary>
public sealed class IntrebariAiTests
{
    [Theory]
    [InlineData("Cum se numește capitolul despre Luceafărul?")]
    [InlineData("Conform materialului, când a apărut „Luceafărul”?")]
    [InlineData("CONFORM MATERIALULUI, cand a aparut Luceafarul?")]
    [InlineData("Ce afirmă textul de mai sus despre Hyperion?")]
    [InlineData("Din notițe, care este tema poeziei?")]
    [InlineData("Ce este prezentat în material despre Cătălina?")]
    [InlineData("Care este titlul capitolului al doilea?")]
    [InlineData("Ce se spune în acest capitol despre romantism?")]
    [InlineData("Ce idee principală are lecția aceasta?")]
    [InlineData("Ce informație apare în fișierul atașat?")]
    public void IntrebariDespreDocument(string text) => CalitateIntrebari.EsteDespreDocument(text).ShouldBeTrue();

    [Theory]
    [InlineData("Când a apărut poemul „Luceafărul”?")]
    [InlineData("Ce rol are materialul genetic în diviziunea celulară?")]
    [InlineData("Din ce material este făcut un conductor bun de electricitate?")]
    [InlineData("Ce funcție din C++ scrie datele în fișier?")]
    [InlineData("În ce capitol al romanului „Ion” are loc nunta Anei?")]
    [InlineData("Ce document a semnat Alexandru Ioan Cuza în 1864?")]
    [InlineData("Care este tema lecției de viață din „Moara cu noroc”?")]
    public void IntrebariDespreMaterie(string text) => CalitateIntrebari.EsteDespreDocument(text).ShouldBeFalse();

    [Theory]
    [InlineData("În ce an a apărut poemul „Luceafărul”?", "Poemul „Luceafărul” a apărut în ce an?")]
    [InlineData("Când a apărut Luceafărul?", "CAND A APARUT LUCEAFARUL")]
    [InlineData("Cine este Hyperion în „Luceafărul”?", "Cine este, în „Luceafărul”, Hyperion?")]
    [InlineData("Care este tema poeziei „Plumb” de George Bacovia?", "Care este tema principală a poeziei „Plumb” de George Bacovia?")]
    public void AceeasiIntrebareCuAlteCuvinte(string a, string b) => CalitateIntrebari.Asemanare(a, b).ShouldBeGreaterThanOrEqualTo(CalitateIntrebari.PragAsemanare);

    [Theory]
    [InlineData("Cine este Hyperion în „Luceafărul”?", "Cine este Cătălina în „Luceafărul”?")]
    [InlineData("În ce an a apărut „Luceafărul”?", "Cine a scris „Luceafărul”?")]
    [InlineData("Care este tema poeziei „Plumb”?", "Care este tema poeziei „Lacustră”?")]
    [InlineData("Ce este viteza?", "Ce este accelerația?")]
    public void IntrebariDiferite(string a, string b) => CalitateIntrebari.Asemanare(a, b).ShouldBeLessThan(CalitateIntrebari.PragAsemanare);

    [Fact]
    public void AsemanareaEsteSimetricaSiIntreZeroSiUnu()
    {
        var (a, b) = ("Cine a scris „Luceafărul”?", "„Luceafărul” a fost scris de cine?");
        CalitateIntrebari.Asemanare(a, b).ShouldBe(CalitateIntrebari.Asemanare(b, a));
        CalitateIntrebari.Asemanare(a, a).ShouldBe(1);
        CalitateIntrebari.Asemanare(a, "Ce este fotosinteza?").ShouldBe(0);
    }

    [Fact]
    public void FaraCuvinteDeContinutNimicNuSeamana()
    {
        CalitateIntrebari.Asemanare("Ce este?", "Ce este?").ShouldBe(0);
        CalitateIntrebari.Asemanare("", "Cine a scris „Plumb”?").ShouldBe(0);
    }

    [Fact]
    public void CuvinteleDeContinutSuntNormalizate()
    {
        CalitateIntrebari.Cuvinte("Când a apărut „Luceafărul”, în 1883?").ShouldBe(new HashSet<string> { "aparut", "luceafarul", "1883" }, ignoreOrder: true);
        CalitateIntrebari.Cuvinte("Ştefan (cu sedilă) și Ștefan (cu virgulă)").Count(c => c == "stefan").ShouldBe(1);
    }

    [Fact]
    public void FiltrulPastreazaOrdineaSiMaximul()
    {
        var (pastrate, asemanatoare, despre) = CalitateIntrebari.Filtreaza(
            ["Cine a scris „Plumb”?", "Ce este fotosinteza?", "Când a apărut „Luceafărul”?", "Ce este viteza?"], q => q, [], 3);
        pastrate.ShouldBe(["Cine a scris „Plumb”?", "Ce este fotosinteza?", "Când a apărut „Luceafărul”?"]);
        (asemanatoare, despre).ShouldBe((0, 0));
    }

    [Fact]
    public void FiltrulScoateRepetareaDinAcelasiLot()
    {
        var (pastrate, asemanatoare, _) = CalitateIntrebari.Filtreaza(
            ["În ce an a apărut „Luceafărul”?", "„Luceafărul” a apărut în ce an?", "Cine este Hyperion?"], q => q, [], 5);
        pastrate.ShouldBe(["În ce an a apărut „Luceafărul”?", "Cine este Hyperion?"]);
        asemanatoare.ShouldBe(1);
    }

    [Fact]
    public void FiltrulScoateCeExistaDeja()
    {
        var (pastrate, asemanatoare, _) = CalitateIntrebari.Filtreaza(["Când a apărut Luceafărul?", "Cine este Cătălina?"], q => q,
            ["CÂND a apărut „Luceafărul”"], 5);
        pastrate.ShouldBe(["Cine este Cătălina?"]);
        asemanatoare.ShouldBe(1);
    }

    [Fact]
    public void FiltrulNumaraIntrebarileDespreDocument()
    {
        var (pastrate, asemanatoare, despre) = CalitateIntrebari.Filtreaza(
            ["Cum se numește capitolul 2?", "Conform materialului, cine este Hyperion?", "Cine este Hyperion?"], q => q, [], 5);
        pastrate.ShouldBe(["Cine este Hyperion?"]);
        (asemanatoare, despre).ShouldBe((0, 2));
    }

    [Fact]
    public void MaximulSeAtingeInainteDeARespingeRestul()
    {
        // Once enough are kept, the rest is not even looked at (so it is not counted as dropped).
        var (pastrate, asemanatoare, despre) = CalitateIntrebari.Filtreaza(
            ["Cine este Hyperion?", "Cine este Cătălina?", "Cine este Hyperion?", "Din material: ce este?"], q => q, [], 2);
        pastrate.Count.ShouldBe(2);
        (asemanatoare, despre).ShouldBe((0, 0));
    }

    [Fact]
    public void FiltrulMergePeOrice()
    {
        var (pastrate, _, _) = CalitateIntrebari.Filtreaza(new[] { (Id: 1, Text: "Cine este Hyperion?"), (Id: 2, Text: "CINE este Hyperion") },
            q => q.Text, [], 5);
        pastrate.Select(q => q.Id).ShouldBe([1]);
    }
}
