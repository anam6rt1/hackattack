using MarkAi.Api.Infrastructura;
using MarkAi.Data.Entities;

namespace MarkAi.Tests.Unit;

/// <summary>
/// The access rules of <see cref="AccesContinut"/> evaluated on in-memory Lecții: who sees a Lecție or chapter and who edits it.
/// Liceu = organisation 1, Centru = 2; user 7 is the author.
/// </summary>
public sealed class AccesContinutTests
{
    private static readonly ContextAcces Anonim = ContextAcces.Anonim;
    private static readonly ContextAcces Autor = new(7, false, [1], []);
    private static readonly ContextAcces MembruLiceu = new(8, false, [1], []);
    private static readonly ContextAcces MembruCentru = new(9, false, [2], []);
    private static readonly ContextAcces AdminLiceu = new(10, false, [1], [1]);
    private static readonly ContextAcces AdminPlatforma = new(11, true, [], []);
    private static readonly ContextAcces Strain = new(12, false, [], []);

    private static Parcurs Lectie(string viz = Parcurs.Public, int? org = 1, bool publicat = true, int? autor = 7, params int[] partajari) => new()
    {
        Vizibilitate = viz, OrganizatieId = org, Publicat = publicat, AutorId = autor,
        Partajari = partajari.Select(o => new ParcursOrganizatie { OrganizatieId = o }).ToList(),
    };

    private static bool Vede(ContextAcces a, Parcurs p) => AccesContinut.Vizibil(a).Compile()(p);
    private static bool Editeaza(ContextAcces a, Parcurs p) => AccesContinut.PoateEdita(a).Compile()(p);
    private static bool VedeCapitol(ContextAcces a, Parcurs p, bool publicat = true) =>
        AccesContinut.CapitolVizibil(a).Compile()(new Curs { Parcurs = p, Publicat = publicat });

    [Fact]
    public void LectiaPublicaEsteVazutaDeToti()
    {
        var p = Lectie();
        new[] { Anonim, Strain, MembruCentru, MembruLiceu }.ShouldAllBe(a => Vede(a, p));
    }

    [Fact]
    public void LectiaDoarOrganizatieEsteAscunsaCelorDinAfara()
    {
        var p = Lectie(Parcurs.DoarOrganizatie);
        Vede(Anonim, p).ShouldBeFalse();
        Vede(Strain, p).ShouldBeFalse();
        Vede(MembruCentru, p).ShouldBeFalse();
    }

    [Fact]
    public void MembriiOrganizatieiVadLectiaEi() => Vede(MembruLiceu, Lectie(Parcurs.DoarOrganizatie)).ShouldBeTrue();

    [Fact]
    public void PartajareaDeschideLectiaAltreiOrganizatii() => Vede(MembruCentru, Lectie(Parcurs.DoarOrganizatie, partajari: 2)).ShouldBeTrue();

    [Fact]
    public void LectiaNepublicataEsteAscunsaChiarDacaEPublica()
    {
        var p = Lectie(publicat: false);
        Vede(Strain, p).ShouldBeFalse();
        Vede(MembruLiceu, p).ShouldBeFalse();
    }

    [Fact]
    public void EditoriiVadLectiaNepublicata()
    {
        var p = Lectie(Parcurs.DoarOrganizatie, publicat: false);
        Vede(Autor, p).ShouldBeTrue();
        Vede(AdminLiceu, p).ShouldBeTrue();
        Vede(AdminPlatforma, p).ShouldBeTrue();
    }

    [Fact]
    public void AutorulEditeaza() => Editeaza(Autor, Lectie()).ShouldBeTrue();

    [Fact]
    public void AdministratorulOrganizatieiDeBazaEditeaza() => Editeaza(AdminLiceu, Lectie()).ShouldBeTrue();

    [Fact]
    public void AdministratorulAltreiOrganizatiiNuEditeaza() => Editeaza(new ContextAcces(13, false, [2], [2]), Lectie(partajari: 2)).ShouldBeFalse();

    [Fact]
    public void AdminulPlatformeiEditeazaOrice() => Editeaza(AdminPlatforma, Lectie(org: null, autor: null)).ShouldBeTrue();

    [Fact]
    public void UnMembruNuEditeaza() => Editeaza(MembruLiceu, Lectie()).ShouldBeFalse();

    [Fact]
    public void AnonimulNuEditeazaLectiaFaraAutor() => Editeaza(Anonim, Lectie(autor: null)).ShouldBeFalse();

    [Fact]
    public void AnonimulNuDevineAutorAlUneiLectiiFaraAutor()
    {
        // The anonymous id is 0; a Lecție with author id 0 must not count as theirs.
        Editeaza(Anonim, Lectie(autor: 0)).ShouldBeFalse();
    }

    [Fact]
    public void LectiaDePlatformaFaraOrganizatieEsteDoarAAdminului()
    {
        var p = Lectie(org: null, autor: 11);
        Editeaza(AdminLiceu, p).ShouldBeFalse();
        Editeaza(AdminPlatforma, p).ShouldBeTrue();
    }

    [Fact]
    public void CapitolulNepublicatEsteAscunsElevilor() => VedeCapitol(MembruLiceu, Lectie(), publicat: false).ShouldBeFalse();

    [Fact]
    public void CapitolulNepublicatEsteVazutDeEditori() => VedeCapitol(Autor, Lectie(), publicat: false).ShouldBeTrue();

    [Fact]
    public void CapitolulUrmeazaLectia()
    {
        var p = Lectie(Parcurs.DoarOrganizatie);
        VedeCapitol(Strain, p).ShouldBeFalse();
        VedeCapitol(MembruLiceu, p).ShouldBeTrue();
        VedeCapitol(MembruCentru, Lectie(Parcurs.DoarOrganizatie, partajari: 2)).ShouldBeTrue();
    }

    [Fact]
    public void CapitolulLectieiNepublicateEsteAscuns() => VedeCapitol(MembruLiceu, Lectie(publicat: false)).ShouldBeFalse();

    [Theory]
    [InlineData(Parcurs.Public, true)]
    [InlineData(Parcurs.DoarOrganizatie, false)]
    public void VizibilitateaPentruUnStrain(string viz, bool vede) => Vede(Strain, Lectie(viz)).ShouldBe(vede);
}
