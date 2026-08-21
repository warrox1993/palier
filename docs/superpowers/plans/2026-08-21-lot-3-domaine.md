# Lot 3 — `Palier.Domain` : plan d'implémentation

> **Pour les exécutants :** SOUS-SKILL REQUISE — `superpowers:executing-plans`. Les étapes utilisent des cases à cocher (`- [ ]`).

**But :** livrer `Palier.Domain` en entier — grandeurs typées, calculs purs, planchers de sécurité — à 100 % de couverture lignes, branches et méthodes.

**Architecture :** un type par grandeur, construit par fabrique qui refuse l'invalide, si bien qu'aucune valeur du domaine ne peut exister dans un état incorrect. Les calculs sont des classes statiques pures, groupées par dossier thématique. Aucune valeur de référence — limite haute, MET, cible de volume — n'est écrite en dur : elle est reçue en paramètre.

**Pile :** .NET 10, C# `Nullable enable`, `ImplicitUsings`, xUnit 2.9.3, coverlet MSBuild.

**Spec :** `docs/superpowers/specs/2026-08-21-lot-3-domaine-design.md`

## Contraintes globales

- **Namespaces file-scoped obligatoires** — `csharp_style_namespace_declarations = file_scoped:error`.
- **Couverture 100 %** en `line,branch,method`, `ThresholdStat total`, sur `[Palier.Domain]*`. **Chaque branche exige son test**, y compris le bras `default` d'un `switch` sur énumération : le précédent est dans `MetabolismeDeBaseTests.MifflinStJeor_refuse_un_sexe_hors_enumeration`.
- **Le domaine est en français** — `MetabolismeDeBase`, `Sexe`, `Energie`. L'infrastructure reste en anglais parce qu'elle est le miroir du schéma ; ne pas aligner l'un sur l'autre.
- **`CA1707` est éteinte** : les noms de tests portent des underscores, en français, décrivant le comportement.
- **Aucune chaîne destinée à un utilisateur.** Le domaine rend des valeurs et des énumérations ; les libellés vivent en base et passent par i18next.
- **Aucune valeur de référence en dur** : ni limite haute, ni MET, ni cible de volume. Reçues en paramètre.
- **`decimal` partout**, jamais `double` : ce sont des grandeurs de santé, pas de la physique.
- **Commit après chaque tâche**, message en français à l'impératif.
- **Commandes :** `dotnet test back/Palier.sln --settings back/coverage.runsettings` pour la suite complète ; `dotnet test back/Palier.Domain.Tests/Palier.Domain.Tests.csproj --filter "FullyQualifiedName~<Nom>"` pour un test isolé.
- **Le seuil de couverture fait échouer `dotnet test`** dès qu'une branche manque. C'est le signal, pas un obstacle : une branche non couverte est une branche qui ment.

---

## Structure des fichiers

```
back/Palier.Domain/
├── Grandeurs/
│   ├── Masse.cs                    poids corporel, 0 < x ≤ 500 kg
│   ├── Taille.cs                   30 ≤ x ≤ 300 cm
│   ├── Age.cs                      0 ≤ x ≤ 130 ans
│   ├── Energie.cs                  kcal, ≥ 0
│   ├── PourcentageMasseGrasse.cs   0 < x < 100, + provenance
│   ├── Charge.cs                   charge soulevée, 0 ≤ x ≤ 1000 kg
│   ├── Repetitions.cs              1 ≤ x ≤ 1000
│   ├── Rir.cs                      0 ≤ x ≤ 10
│   ├── MasseNutriment.cs           valeur + unité (g, mg, µg)
│   └── VolumeEau.cs                millilitres, ≥ 0
├── Energie/
│   ├── Sexe.cs                     (existe)
│   ├── MetabolismeDeBase.cs        (existe — à retyper) + Katch-McArdle
│   ├── NiveauActivite.cs           les quatre facteurs
│   ├── DepenseEstimee.cs           estimation + bornes, indissociables
│   ├── DepenseTotale.cs            MB × facteur + coût de séance
│   └── TdeeAdaptatif.cs            mesure rétrospective, ou refus
├── Objectifs/
│   ├── CibleMacronutriments.cs     protéines, lipides, glucides, fibres, eau
│   ├── PlancherCalorique.cs        1200 / 1500, non contournable
│   └── ApportCible.cs              ajustement borné, plancher appliqué en dernier
├── Nutriments/
│   ├── StatutReference.cs          les quatre statuts
│   ├── ReferenceNutriment.cs       valeur + statut, sans chiffre en dur
│   ├── ApportAgrege.cs             alimentation + compléments
│   └── ComparaisonReference.cs     quatre issues, jamais deux
├── Entrainement/
│   ├── ForceEstimee.cs             Epley + RIR, avec fiabilité ou rien
│   ├── Fiabilite.cs                les quatre paliers
│   ├── VolumeParGroupe.cs          séries par groupe, ratio tirage/poussée
│   └── DetectionPlateau.cs         trois séances sans progression
└── Securite/
    ├── PerteDePoidsRapide.cs       > 1 %/semaine sur trois semaines
    └── RestrictionSevere.cs        apports répétés sous le métabolisme de base
```

Les tests miment cette arborescence sous `back/Palier.Domain.Tests/`.

**Pas d'entités riches.** La spec d'architecture § 4 liste `Workout`, `Set`, `Exercise` — ils existent déjà en anglais dans `Palier.Infrastructure/Entites/Entites.cs`, comme miroir du schéma. Les dupliquer dans le domaine créerait deux vérités. Chaque calcul reçoit à la place un `record` minimal, défini avec lui, portant exactement ce qu'il consomme. YAGNI.

---

## Tâche 1 : les grandeurs corporelles

**Fichiers :**

- Créer : `back/Palier.Domain/Grandeurs/Masse.cs`, `Taille.cs`, `Age.cs`
- Test : `back/Palier.Domain.Tests/Grandeurs/GrandeursCorporellesTests.cs`

**Interfaces produites :**

```csharp
public readonly record struct Masse
{
    public decimal Kilogrammes { get; }
    public static Masse DepuisKilogrammes(decimal kilogrammes);
}
public readonly record struct Taille
{
    public decimal Centimetres { get; }
    public decimal Metres { get; }          // utile aux équations en mètres
    public static Taille DepuisCentimetres(decimal centimetres);
}
public readonly record struct Age
{
    public decimal Annees { get; }
    public static Age DepuisAnnees(decimal annees);
}
```

**Bornes, et pourquoi elles diffèrent :** `Masse` est un poids corporel — `0 < x ≤ 500`, au-delà duquel aucun humain n'a jamais été pesé. `Charge` (tâche 3) monte à 1 000 kg parce que c'est une barre. C'est exactement ce qu'un type générique unique n'aurait pas su exprimer.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Grandeurs;

public sealed class GrandeursCorporellesTests
{
    [Fact]
    public void Masse_conserve_la_valeur_donnee()
    {
        Assert.Equal(73.5m, Masse.DepuisKilogrammes(73.5m).Kilogrammes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500.1)]
    public void Masse_refuse_hors_bornes(decimal kilogrammes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Masse.DepuisKilogrammes(kilogrammes));
    }

    [Fact]
    public void Masse_accepte_la_borne_haute()
    {
        Assert.Equal(500m, Masse.DepuisKilogrammes(500m).Kilogrammes);
    }

    [Fact]
    public void Taille_expose_les_centimetres_et_les_metres()
    {
        var t = Taille.DepuisCentimetres(178m);
        Assert.Equal(178m, t.Centimetres);
        Assert.Equal(1.78m, t.Metres);
    }

    [Theory]
    [InlineData(29.9)]
    [InlineData(300.1)]
    public void Taille_refuse_hors_bornes(decimal centimetres)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Taille.DepuisCentimetres(centimetres));
    }

    [Fact]
    public void Age_accepte_zero()
    {
        Assert.Equal(0m, Age.DepuisAnnees(0m).Annees);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(130.1)]
    public void Age_refuse_hors_bornes(decimal annees)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Age.DepuisAnnees(annees));
    }
}
```

- [ ] **Étape 2 — lancer, constater le rouge**

`dotnet test back/Palier.Domain.Tests/Palier.Domain.Tests.csproj --filter "FullyQualifiedName~GrandeursCorporelles"`
Attendu : échec de compilation, `Masse` introuvable.

- [ ] **Étape 3 — implémenter**

```csharp
namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un poids corporel, en kilogrammes. La borne haute est celle du corps
/// humain, pas celle d'une barre : <see cref="Charge"/> monte bien plus haut.
/// </summary>
public readonly record struct Masse
{
    private Masse(decimal kilogrammes) => Kilogrammes = kilogrammes;

    public decimal Kilogrammes { get; }

    public static Masse DepuisKilogrammes(decimal kilogrammes) =>
        kilogrammes is > 0m and <= 500m
            ? new Masse(kilogrammes)
            : throw new ArgumentOutOfRangeException(nameof(kilogrammes), kilogrammes, null);
}
```

`Taille` et `Age` suivent la même forme, avec leurs bornes propres. `Taille.Metres` rend `Centimetres / 100m`.

- [ ] **Étape 4 — lancer, constater le vert**

- [ ] **Étape 5 — commiter**

```bash
git add back/Palier.Domain/Grandeurs back/Palier.Domain.Tests/Grandeurs
git commit -m "typent le poids, la taille et l'âge, chacun avec ses propres bornes"
```

---

## Tâche 2 : énergie et composition corporelle

**Fichiers :**

- Créer : `Grandeurs/Energie.cs`, `Grandeurs/PourcentageMasseGrasse.cs`
- Test : `Tests/Grandeurs/EnergieEtCompositionTests.cs`

**Interfaces produites :**

```csharp
public readonly record struct Energie
{
    public decimal Kilocalories { get; }
    public static Energie DepuisKilocalories(decimal kilocalories);   // ≥ 0
}

public enum ProvenanceMesure { MesureFiable, Impedancemetrie, Declaratif }

public readonly record struct PourcentageMasseGrasse
{
    public decimal Pourcentage { get; }
    public ProvenanceMesure Provenance { get; }
    public static PourcentageMasseGrasse De(decimal pourcentage, ProvenanceMesure provenance);
    public Masse MasseMaigreDe(Masse masseTotale);
}
```

**Pourquoi la provenance est portée par le type.** La spec § 3 tranche que Katch-McArdle n'est retenue que sur une mesure fiable, jamais sur une balance à impédance qui dévie de 4,4 points sous la DXA. Si la provenance vivait à côté de la valeur, un appelant pourrait l'oublier. Ici elle voyage avec elle.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void Energie_accepte_zero()
    => Assert.Equal(0m, Energie.DepuisKilocalories(0m).Kilocalories);

[Fact]
public void Energie_refuse_une_valeur_negative()
    => Assert.Throws<ArgumentOutOfRangeException>(() => Energie.DepuisKilocalories(-1m));

[Fact]
public void MasseGrasse_calcule_la_masse_maigre()
{
    var mg = PourcentageMasseGrasse.De(20m, ProvenanceMesure.MesureFiable);
    Assert.Equal(60m, mg.MasseMaigreDe(Masse.DepuisKilogrammes(75m)).Kilogrammes);
}

[Fact]
public void MasseGrasse_conserve_la_provenance()
{
    var mg = PourcentageMasseGrasse.De(22m, ProvenanceMesure.Impedancemetrie);
    Assert.Equal(ProvenanceMesure.Impedancemetrie, mg.Provenance);
}

[Theory]
[InlineData(0)]
[InlineData(100)]
[InlineData(-1)]
public void MasseGrasse_refuse_hors_bornes(decimal pourcentage)
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => PourcentageMasseGrasse.De(pourcentage, ProvenanceMesure.MesureFiable));

[Fact]
public void MasseGrasse_refuse_une_provenance_hors_enumeration()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => PourcentageMasseGrasse.De(20m, (ProvenanceMesure)99));
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**

- [ ] **Étape 5 — commiter**

```bash
git commit -m "fait voyager la provenance de la masse grasse avec sa valeur"
```

---

## Tâche 3 : les grandeurs d'entraînement

**Fichiers :** `Grandeurs/Charge.cs`, `Repetitions.cs`, `Rir.cs` · `Tests/Grandeurs/GrandeursEntrainementTests.cs`

**Interfaces produites :**

```csharp
public readonly record struct Charge
{
    public decimal Kilogrammes { get; }
    public static Charge DepuisKilogrammes(decimal kilogrammes);      // 0 ≤ x ≤ 1000
}
public readonly record struct Repetitions
{
    public int Nombre { get; }
    public static Repetitions De(int nombre);                          // 1 ≤ x ≤ 1000
}
public readonly record struct Rir
{
    public int Nombre { get; }
    public static Rir De(int nombre);                                  // 0 ≤ x ≤ 10
    public static Rir ParDefaut { get; }                               // 2 — hypothèse prudente
}
```

`Charge` accepte **zéro** : le poids de corps est une charge légitime. `Rir.ParDefaut` vaut 2, ce qu'impose `05-entrainement.md` § 3 — « si le RIR n'est pas renseigné, supposer 2 ». L'ACSM 2026 confirme que 2-3 RIR produit les mêmes gains que l'échec, avec moins de risque.

- [ ] **Étape 1 — tests rouges** : conservation de valeur, refus hors bornes pour les trois, `Charge` accepte 0, `Rir.ParDefaut` vaut 2.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "typent la charge, les répétitions et le RIR"`

---

## Tâche 4 : nutriments et hydratation

**Fichiers :** `Grandeurs/MasseNutriment.cs`, `VolumeEau.cs` · `Tests/Grandeurs/NutrimentsTests.cs`

**Interfaces produites :**

```csharp
public enum UniteNutriment { Gramme, Milligramme, Microgramme }

public readonly record struct MasseNutriment
{
    public decimal Valeur { get; }
    public UniteNutriment Unite { get; }
    public static MasseNutriment De(decimal valeur, UniteNutriment unite);   // ≥ 0
    public MasseNutriment ConvertieEn(UniteNutriment cible);
    public static MasseNutriment operator +(MasseNutriment a, MasseNutriment b);
}

public readonly record struct VolumeEau
{
    public decimal Millilitres { get; }
    public static VolumeEau DepuisMillilitres(decimal millilitres);           // ≥ 0
}
```

**L'unité est portée, pas supposée.** Une limite haute s'exprime en mg pour le zinc et en µg pour le sélénium ; additionner sans convertir se trompe d'un facteur mille. L'addition convertit vers l'unité **la plus fine** des deux opérandes, pour ne jamais perdre de précision.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void Nutriment_convertit_les_grammes_en_milligrammes()
    => Assert.Equal(1000m, MasseNutriment.De(1m, UniteNutriment.Gramme)
        .ConvertieEn(UniteNutriment.Milligramme).Valeur);

[Fact]
public void Nutriment_convertit_les_microgrammes_en_milligrammes()
    => Assert.Equal(0.5m, MasseNutriment.De(500m, UniteNutriment.Microgramme)
        .ConvertieEn(UniteNutriment.Milligramme).Valeur);

[Fact]
public void Nutriment_additionne_dans_l_unite_la_plus_fine()
{
    var somme = MasseNutriment.De(17.2m, UniteNutriment.Milligramme)
              + MasseNutriment.De(25000m, UniteNutriment.Microgramme);
    Assert.Equal(UniteNutriment.Microgramme, somme.Unite);
    Assert.Equal(42200m, somme.Valeur);   // l'exemple du zinc de 04-nutrition.md § 4
}

[Fact]
public void Nutriment_refuse_une_valeur_negative() { /* ArgumentOutOfRangeException */ }

[Fact]
public void Nutriment_refuse_une_unite_hors_enumeration() { /* (UniteNutriment)99 */ }
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "fait porter son unité à chaque masse de nutriment"`

---

## Tâche 5 : métabolisme de base retypé, et Katch-McArdle

**Fichiers :** modifier `Energie/MetabolismeDeBase.cs` et son test · créer rien d'autre

**Interfaces produites :**

```csharp
public static class MetabolismeDeBase
{
    public static Energie MifflinStJeor(Sexe sexe, Masse masse, Taille taille, Age age);
    public static Energie KatchMcArdle(Masse masseMaigre);
    public static Energie Estimer(Sexe sexe, Masse masse, Taille taille, Age age,
                                  PourcentageMasseGrasse? masseGrasse);
}
```

**`Estimer` porte la décision de la spec § 3 :** Katch-McArdle si — et seulement si — la masse grasse provient d'une `MesureFiable`. Dans tous les autres cas, Mifflin-St Jeor. Une balance à impédance ne déclenche donc jamais Katch-McArdle.

Les gardes de bornes disparaissent du corps : `Masse`, `Taille` et `Age` ne peuvent plus être invalides. Les tests de refus correspondants migrent vers la tâche 1 — c'est le gain du typage, et il faut le constater plutôt que le supposer.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
{
    var r = MetabolismeDeBase.MifflinStJeor(
        Sexe.Homme, Masse.DepuisKilogrammes(73m),
        Taille.DepuisCentimetres(178m), Age.DepuisAnnees(35m));
    Assert.Equal(1672.5m, r.Kilocalories);   // 730 + 1112,5 − 175 + 5
}

[Fact]
public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
{
    var r = MetabolismeDeBase.MifflinStJeor(
        Sexe.Femme, Masse.DepuisKilogrammes(60m),
        Taille.DepuisCentimetres(165m), Age.DepuisAnnees(30m));
    Assert.Equal(1320.25m, r.Kilocalories);  // 600 + 1031,25 − 150 − 161
}

[Fact]
public void MifflinStJeor_refuse_un_sexe_hors_enumeration() { /* (Sexe)99 */ }

[Fact]
public void KatchMcArdle_applique_la_formule()
    => Assert.Equal(1666m, MetabolismeDeBase
        .KatchMcArdle(Masse.DepuisKilogrammes(60m)).Kilocalories);   // 370 + 21,6 × 60

[Fact]
public void Estimer_prend_Katch_McArdle_sur_une_mesure_fiable()
{
    var r = MetabolismeDeBase.Estimer(
        Sexe.Homme, Masse.DepuisKilogrammes(75m), Taille.DepuisCentimetres(178m),
        Age.DepuisAnnees(35m),
        PourcentageMasseGrasse.De(20m, ProvenanceMesure.MesureFiable));
    Assert.Equal(1666m, r.Kilocalories);     // masse maigre 60 kg
}

[Theory]
[InlineData(ProvenanceMesure.Impedancemetrie)]
[InlineData(ProvenanceMesure.Declaratif)]
public void Estimer_refuse_Katch_McArdle_hors_mesure_fiable(ProvenanceMesure provenance)
{
    var r = MetabolismeDeBase.Estimer(
        Sexe.Homme, Masse.DepuisKilogrammes(73m), Taille.DepuisCentimetres(178m),
        Age.DepuisAnnees(35m), PourcentageMasseGrasse.De(20m, provenance));
    Assert.Equal(1672.5m, r.Kilocalories);   // Mifflin, pas Katch
}

[Fact]
public void Estimer_prend_Mifflin_sans_masse_grasse()
{
    var r = MetabolismeDeBase.Estimer(
        Sexe.Homme, Masse.DepuisKilogrammes(73m), Taille.DepuisCentimetres(178m),
        Age.DepuisAnnees(35m), null);
    Assert.Equal(1672.5m, r.Kilocalories);
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "réserve Katch-McArdle aux mesures fiables de masse grasse"`

---

## Tâche 6 : la dépense totale, et sa fourchette indissociable

**Fichiers :** `Energie/NiveauActivite.cs`, `DepenseEstimee.cs`, `DepenseTotale.cs` · `Tests/Energie/DepenseTotaleTests.cs`

**Interfaces produites :**

```csharp
public enum NiveauActivite { BureauPeuDeMarche, BureauMarcheModeree, MixteMarcheReguliere, MetierDebout }

public readonly record struct DepenseEstimee
{
    public Energie Estimation { get; }
    public Energie BorneBasse { get; }
    public Energie BorneHaute { get; }
    public static DepenseEstimee Autour(Energie estimation);
}

public static class DepenseTotale
{
    public static decimal Facteur(NiveauActivite niveau);
    public static Energie CoutDeSeance(decimal met, Masse masse, TimeSpan duree);
    public static DepenseEstimee Calculer(Energie metabolismeDeBase, NiveauActivite niveau,
                                          Energie coutDesSeances);
}
```

**La fourchette n'est pas optionnelle.** `04-nutrition.md` § 1 : « cette marge est affichée avec le chiffre, systématiquement », et le motif est écrit — un utilisateur qui prend le chiffre pour une vérité se désabonne. `DepenseEstimee` la porte, donc on ne peut pas afficher l'estimation sans elle.

**La marge retenue est ±12,5 %**, milieu de la fourchette « ±10 à 15 % » du document. Elle reproduit exactement la borne basse de l'exemple qui y figure : 2 400 × 0,875 = 2 100.

**Le MET est un paramètre, jamais une constante.** Les sources donnent 3,5 à 9,0 selon la version du Compendium et le code ; la valeur vit en base avec sa version et sa date (spec § 3).

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Theory]
[InlineData(NiveauActivite.BureauPeuDeMarche, 1.25)]
[InlineData(NiveauActivite.BureauMarcheModeree, 1.35)]
[InlineData(NiveauActivite.MixteMarcheReguliere, 1.45)]
[InlineData(NiveauActivite.MetierDebout, 1.60)]
public void Facteur_suit_le_tableau_de_04_nutrition(NiveauActivite n, decimal attendu)
    => Assert.Equal(attendu, DepenseTotale.Facteur(n));

[Fact]
public void Facteur_refuse_un_niveau_hors_enumeration()
    => Assert.Throws<ArgumentOutOfRangeException>(() => DepenseTotale.Facteur((NiveauActivite)99));

[Fact]
public void CoutDeSeance_multiplie_le_MET_par_la_masse_et_la_duree()
{
    var r = DepenseTotale.CoutDeSeance(5.0m, Masse.DepuisKilogrammes(75m), TimeSpan.FromHours(1));
    Assert.Equal(375m, r.Kilocalories);       // 1 MET = 1 kcal/kg/h
}

[Fact]
public void CoutDeSeance_tient_compte_de_la_masse()
{
    var r = DepenseTotale.CoutDeSeance(5.0m, Masse.DepuisKilogrammes(55m), TimeSpan.FromHours(1));
    Assert.Equal(275m, r.Kilocalories);       // le forfait de 5 kcal/min en aurait facturé 300
}

[Fact]
public void CoutDeSeance_refuse_un_MET_non_positif()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => DepenseTotale.CoutDeSeance(0m, Masse.DepuisKilogrammes(75m), TimeSpan.FromHours(1)));

[Fact]
public void CoutDeSeance_refuse_une_duree_negative()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => DepenseTotale.CoutDeSeance(5m, Masse.DepuisKilogrammes(75m), TimeSpan.FromHours(-1)));

[Fact]
public void Calculer_ajoute_le_cout_des_seances_au_metabolisme_module()
{
    var r = DepenseTotale.Calculer(
        Energie.DepuisKilocalories(1600m),
        NiveauActivite.BureauMarcheModeree,
        Energie.DepuisKilocalories(240m));
    Assert.Equal(2400m, r.Estimation.Kilocalories);   // 1600 × 1,35 + 240
}

[Fact]
public void La_fourchette_encadre_l_estimation_a_douze_et_demi_pour_cent()
{
    var r = DepenseEstimee.Autour(Energie.DepuisKilocalories(2400m));
    Assert.Equal(2100m, r.BorneBasse.Kilocalories);
    Assert.Equal(2700m, r.BorneHaute.Kilocalories);
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "rend la dépense inséparable de sa fourchette d'incertitude"`

---

## Tâche 7 : le TDEE adaptatif, qui refuse de conclure trop tôt

**Fichiers :** `Energie/TdeeAdaptatif.cs` · `Tests/Energie/TdeeAdaptatifTests.cs`

**Interfaces produites :**

```csharp
public sealed record FenetreDeMesure(
    int JoursDeSaisieComplete, int NombreDePesees, int JoursCouverts,
    Energie ApportMoyenQuotidien, decimal VariationDePoidsKg);

public static class TdeeAdaptatif
{
    public const int JoursDeSaisieMinimum = 14;
    public const int PeseesMinimum = 10;
    public const decimal KilocaloriesParKilogramme = 7700m;

    public static Energie? Calculer(FenetreDeMesure fenetre);
}
```

**Il rend `null` plutôt qu'un chiffre faible.** Sous 14 jours de saisie ou 10 pesées, `04-nutrition.md` § 1 interdit de conclure. Un `Energie` approximatif serait indiscernable d'un `Energie` mesuré une fois sorti de la fonction.

**Le coefficient de 7 700 est exposé en constante publique** parce que la spec § 3 signale qu'il est contesté : la règle de Wishnofsky sous-estime l'adaptation métabolique. L'usage est ici **rétrospectif** — inférer une dépense passée, non prédire une perte future — ce qui est plus défendable, mais le rendre visible permet de le corriger sans fouiller le code.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void Calculer_infere_la_depense_sur_une_perte_de_poids()
{
    var r = TdeeAdaptatif.Calculer(new FenetreDeMesure(
        JoursDeSaisieComplete: 21, NombreDePesees: 15, JoursCouverts: 21,
        ApportMoyenQuotidien: Energie.DepuisKilocalories(2400m),
        VariationDePoidsKg: -0.5m));
    // 2400 − (−0,5 × 7700 / 21) = 2400 + 183,333…
    Assert.Equal(2583.33m, Math.Round(r!.Value.Kilocalories, 2));
}

[Fact]
public void Calculer_infere_la_depense_sur_une_prise_de_poids()
{
    var r = TdeeAdaptatif.Calculer(new FenetreDeMesure(21, 15, 21,
        Energie.DepuisKilocalories(3000m), 0.7m));
    // 3000 − (0,7 × 7700 / 21) = 3000 − 256,666…
    Assert.Equal(2743.33m, Math.Round(r!.Value.Kilocalories, 2));
}

[Fact]
public void Calculer_refuse_de_conclure_sous_quatorze_jours_de_saisie()
    => Assert.Null(TdeeAdaptatif.Calculer(new FenetreDeMesure(13, 15, 21,
        Energie.DepuisKilocalories(2400m), -0.5m)));

[Fact]
public void Calculer_refuse_de_conclure_sous_dix_pesees()
    => Assert.Null(TdeeAdaptatif.Calculer(new FenetreDeMesure(21, 9, 21,
        Energie.DepuisKilocalories(2400m), -0.5m)));

[Fact]
public void Calculer_refuse_une_fenetre_sans_jour_couvert()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => TdeeAdaptatif.Calculer(new FenetreDeMesure(21, 15, 0,
            Energie.DepuisKilocalories(2400m), -0.5m)));

[Fact]
public void Calculer_ne_rend_jamais_une_depense_negative()
{
    // une prise de poids massive sur une courte fenêtre pousserait le calcul
    // sous zéro ; Energie refuse le négatif, et le domaine doit le dire.
    Assert.Null(TdeeAdaptatif.Calculer(new FenetreDeMesure(14, 10, 14,
        Energie.DepuisKilocalories(1000m), 5m)));
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "fait taire le TDEE adaptatif tant que la mesure ne suffit pas"`

---

## Tâche 8 : les cibles de macronutriments

**Fichiers :** `Objectifs/CibleMacronutriments.cs` · `Tests/Objectifs/CibleMacronutrimentsTests.cs`

**Interfaces produites :**

```csharp
public sealed record CibleMacronutriments(
    decimal ProteinesG, decimal LipidesG, decimal GlucidesG, decimal FibresG, VolumeEau Eau);

public static class Macronutriments
{
    public const decimal ProteinesParKgMin = 1.6m,  ProteinesParKgDefaut = 1.8m,  ProteinesParKgMax = 2.2m;
    public const decimal LipidesParKgMin   = 0.8m,  LipidesParKgDefaut   = 1.0m,  LipidesParKgMax   = 1.2m;
    public const decimal FibresGMin = 25m, FibresGDefaut = 30m, FibresGMax = 35m;
    public const decimal EauMlParKg = 35m;

    public static CibleMacronutriments Calculer(
        Masse masse, Energie apportCible,
        decimal? proteinesParKg = null, decimal? lipidesParKg = null, decimal? fibresG = null);
}
```

**Les fourchettes sont exposées** parce que `04-nutrition.md` § 2 impose d'afficher chaque objectif avec sa source et sa fourchette de référence. Elles sont **fondées** : Morton 2018 place le point d'inflexion des protéines à 1,62 g/kg, l'intervalle de confiance montant à 2,2.

**Les glucides sont le reste**, jamais une cible propre : `(apport − protéines×4 − lipides×9) / 4`.

**Le refus d'un macronutriment éliminé** appartient à la tâche 9, pas ici : c'est un plancher de sécurité, pas un calcul.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void Calculer_applique_les_valeurs_par_defaut()
{
    var c = Macronutriments.Calculer(Masse.DepuisKilogrammes(73m),
                                     Energie.DepuisKilocalories(2400m));
    Assert.Equal(131.4m, c.ProteinesG);        // 1,8 × 73 — l'exemple du document
    Assert.Equal(73m, c.LipidesG);             // 1,0 × 73
    Assert.Equal(30m, c.FibresG);
    Assert.Equal(2555m, c.Eau.Millilitres);    // 35 × 73
    // 2400 − 525,6 − 657 = 1217,4 → /4
    Assert.Equal(304.35m, c.GlucidesG);
}

[Fact]
public void Calculer_accepte_une_valeur_choisie_dans_la_fourchette()
{
    var c = Macronutriments.Calculer(Masse.DepuisKilogrammes(73m),
                                     Energie.DepuisKilocalories(2400m), proteinesParKg: 2.2m);
    Assert.Equal(160.6m, c.ProteinesG);
}

[Theory]
[InlineData(1.5)]
[InlineData(2.3)]
public void Calculer_refuse_des_proteines_hors_fourchette(decimal parKg)
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => Macronutriments.Calculer(Masse.DepuisKilogrammes(73m),
              Energie.DepuisKilocalories(2400m), proteinesParKg: parKg));

[Theory]
[InlineData(0.7)]
[InlineData(1.3)]
public void Calculer_refuse_des_lipides_hors_fourchette(decimal parKg) { /* idem */ }

[Theory]
[InlineData(24)]
[InlineData(36)]
public void Calculer_refuse_des_fibres_hors_fourchette(decimal grammes) { /* idem */ }

[Fact]
public void Calculer_refuse_un_apport_que_les_macros_depassent()
{
    // 2,2 g/kg de protéines et 1,2 g/kg de lipides sur 120 kg dépassent 1200 kcal :
    // les glucides seraient négatifs, ce qui n'a aucun sens physiologique.
    Assert.Throws<ArgumentOutOfRangeException>(
        () => Macronutriments.Calculer(Masse.DepuisKilogrammes(120m),
              Energie.DepuisKilocalories(1200m), proteinesParKg: 2.2m, lipidesParKg: 1.2m));
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "calcule les cibles de macronutriments dans leurs fourchettes de référence"`

---

## Tâche 9 : le plancher calorique, et l'apport cible

**Fichiers :** `Objectifs/PlancherCalorique.cs`, `ApportCible.cs` · `Tests/Objectifs/ApportCibleTests.cs`

**Interfaces produites :**

```csharp
public static class PlancherCalorique
{
    public const decimal FemmeKcal = 1200m;
    public const decimal HommeKcal = 1500m;
    public static Energie Pour(Sexe sexe);
}

public sealed record ApportCible(Energie Valeur, bool PlancherAtteint);

public static class CalculApportCible
{
    public const decimal DeficitMaximal = -0.20m;
    public const decimal SurplusMaximal = 0.15m;

    public static ApportCible Calculer(Energie maintenance, Sexe sexe, decimal ajustement = 0m);
}
```

**Le plancher s'applique en dernier et ne se contourne pas.** `01-conformite.md` § 5 : « impossible de fixer un objectif sous 1 200 kcal (femme) ou 1 500 kcal (homme). Non contournable » et « ces règles ne peuvent pas être désactivées par configuration ». Il n'y a donc **aucun paramètre** permettant de le lever — c'est le point du test `PlancherAtteint`.

`PlancherAtteint` signale que le plancher a mordu, pour que l'interface puisse le dire. Ce n'est pas une phrase : c'est un booléen, et le libellé vit ailleurs.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Theory]
[InlineData(Sexe.Femme, 1200)]
[InlineData(Sexe.Homme, 1500)]
public void Le_plancher_suit_01_conformite(Sexe sexe, decimal attendu)
    => Assert.Equal(attendu, PlancherCalorique.Pour(sexe).Kilocalories);

[Fact]
public void Le_plancher_refuse_un_sexe_hors_enumeration()
    => Assert.Throws<ArgumentOutOfRangeException>(() => PlancherCalorique.Pour((Sexe)99));

[Fact]
public void Sans_ajustement_l_apport_vaut_la_maintenance()
{
    var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme);
    Assert.Equal(2400m, r.Valeur.Kilocalories);
    Assert.False(r.PlancherAtteint);
}

[Fact]
public void Un_deficit_de_vingt_pour_cent_est_applique()
{
    var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme, -0.20m);
    Assert.Equal(1920m, r.Valeur.Kilocalories);
}

[Fact]
public void Un_surplus_de_quinze_pour_cent_est_applique()
{
    var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme, 0.15m);
    Assert.Equal(2760m, r.Valeur.Kilocalories);
}

[Theory]
[InlineData(-0.21)]
[InlineData(0.16)]
public void Un_ajustement_hors_bornes_est_refuse(decimal ajustement)
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme, ajustement));

[Fact]
public void Le_plancher_mord_sur_une_maintenance_basse()
{
    // 1700 × 0,80 = 1360, sous le plancher homme de 1500
    var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(1700m), Sexe.Homme, -0.20m);
    Assert.Equal(1500m, r.Valeur.Kilocalories);
    Assert.True(r.PlancherAtteint);
}

[Fact]
public void Le_plancher_mord_aussi_sans_aucun_deficit()
{
    // une maintenance mesurée sous le plancher ne descend pas l'objectif
    var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(1100m), Sexe.Femme);
    Assert.Equal(1200m, r.Valeur.Kilocalories);
    Assert.True(r.PlancherAtteint);
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "applique le plancher calorique en dernier, et sans échappatoire"`

---

## Tâche 10 : la comparaison aux références, à quatre issues

**Fichiers :** `Nutriments/StatutReference.cs`, `ReferenceNutriment.cs`, `ApportAgrege.cs`, `ComparaisonReference.cs` · `Tests/Nutriments/ComparaisonReferenceTests.cs`

**Interfaces produites :**

```csharp
public enum StatutReference { LimiteHauteEtablie, NiveauSurDApport, NonDerivable, JamaisEvalue }

public sealed record ReferenceNutriment(
    string Cle, StatutReference Statut, MasseNutriment? Valeur, string Source, int? Annee);

public sealed record ApportAgrege(string Cle, MasseNutriment Alimentation,
                                  MasseNutriment Complements)
{
    public MasseNutriment Total { get; }
}

public enum IssueComparaison { SousLaReference, AuDessusDeLaReference, ReferenceIndicative, AucuneReference }

public sealed record ResultatComparaison(IssueComparaison Issue, MasseNutriment Total,
                                          MasseNutriment? Reference);

public static class ComparaisonReference
{
    public static ResultatComparaison Comparer(ApportAgrege apport, ReferenceNutriment reference);
}
```

**Pourquoi quatre issues et pas deux.** La spec § 2 l'établit : l'EFSA produit des UL, des _safe levels of intake_, et parfois rien. Un _safe level_ — le fer, le manganèse, le DHA — **n'autorise pas** à parler de dépassement, car l'avis dit que « le niveau où le risque commence à augmenter n'est pas défini ». Les confondre annoncerait un danger là où la science n'en définit aucun.

**Le domaine ne connaît aucune valeur.** Elles arrivent par `ReferenceNutriment`. `04-nutrition.md` § 3 : « il est formellement interdit d'inventer un seuil haut ».

**Une référence de statut `LimiteHauteEtablie` sans valeur est une incohérence** : la construction doit la refuser, sinon la comparaison se ferait contre `null` en croyant comparer.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
private static ApportAgrege Zinc(decimal alimentationMg, decimal complementsMg) =>
    new("zinc_mg",
        MasseNutriment.De(alimentationMg, UniteNutriment.Milligramme),
        MasseNutriment.De(complementsMg, UniteNutriment.Milligramme));

[Fact]
public void Le_total_additionne_alimentation_et_complements()
{
    // l'exemple de 04-nutrition.md § 4
    Assert.Equal(42.2m, Zinc(17.2m, 25m).Total.Valeur);
}

[Fact]
public void Un_depassement_de_limite_haute_est_signale()
{
    var r = ComparaisonReference.Comparer(Zinc(17.2m, 25m), new ReferenceNutriment(
        "zinc_mg", StatutReference.LimiteHauteEtablie,
        MasseNutriment.De(25m, UniteNutriment.Milligramme), "EFSA", 2006));
    Assert.Equal(IssueComparaison.AuDessusDeLaReference, r.Issue);
}

[Fact]
public void Un_apport_sous_la_limite_haute_ne_signale_rien()
{
    var r = ComparaisonReference.Comparer(Zinc(8m, 5m), new ReferenceNutriment(
        "zinc_mg", StatutReference.LimiteHauteEtablie,
        MasseNutriment.De(25m, UniteNutriment.Milligramme), "EFSA", 2006));
    Assert.Equal(IssueComparaison.SousLaReference, r.Issue);
}

[Fact]
public void L_egalite_stricte_n_est_pas_un_depassement()
{
    var r = ComparaisonReference.Comparer(Zinc(20m, 5m), new ReferenceNutriment(
        "zinc_mg", StatutReference.LimiteHauteEtablie,
        MasseNutriment.De(25m, UniteNutriment.Milligramme), "EFSA", 2006));
    Assert.Equal(IssueComparaison.SousLaReference, r.Issue);
}

[Fact]
public void Un_niveau_sur_d_apport_ne_produit_jamais_un_depassement()
{
    // le fer n'a AUCUNE UL : un safe level de 40 mg/j, dont l'avis EFSA dit que
    // « le niveau où le risque commence à augmenter n'est pas défini ».
    var apport = new ApportAgrege("iron_mg",
        MasseNutriment.De(30m, UniteNutriment.Milligramme),
        MasseNutriment.De(25m, UniteNutriment.Milligramme));
    var r = ComparaisonReference.Comparer(apport, new ReferenceNutriment(
        "iron_mg", StatutReference.NiveauSurDApport,
        MasseNutriment.De(40m, UniteNutriment.Milligramme), "EFSA", 2024));
    Assert.Equal(IssueComparaison.ReferenceIndicative, r.Issue);
}

[Theory]
[InlineData(StatutReference.NonDerivable)]
[InlineData(StatutReference.JamaisEvalue)]
public void Sans_reference_le_domaine_n_invente_rien(StatutReference statut)
{
    var r = ComparaisonReference.Comparer(Zinc(1m, 1m),
        new ReferenceNutriment("vitamin_c_mg", statut, null, "EFSA", 2004));
    Assert.Equal(IssueComparaison.AucuneReference, r.Issue);
    Assert.Null(r.Reference);
}

[Fact]
public void Une_limite_haute_sans_valeur_est_une_reference_incoherente()
    => Assert.Throws<ArgumentException>(() => new ReferenceNutriment(
        "zinc_mg", StatutReference.LimiteHauteEtablie, null, "EFSA", 2006));

[Fact]
public void La_comparaison_convertit_les_unites()
{
    // sélénium : apport en µg, référence en µg — mais l'alimentation arrive en mg
    var apport = new ApportAgrege("selenium_ug",
        MasseNutriment.De(0.1m, UniteNutriment.Milligramme),      // 100 µg
        MasseNutriment.De(200m, UniteNutriment.Microgramme));
    var r = ComparaisonReference.Comparer(apport, new ReferenceNutriment(
        "selenium_ug", StatutReference.NiveauSurDApport,
        MasseNutriment.De(255m, UniteNutriment.Microgramme), "EFSA", 2023));
    Assert.Equal(300m, r.Total.Valeur);
    Assert.Equal(UniteNutriment.Microgramme, r.Total.Unite);
}

[Fact]
public void La_comparaison_refuse_un_statut_hors_enumeration() { /* (StatutReference)99 */ }

[Fact]
public void La_comparaison_refuse_une_reference_d_un_autre_nutriment()
    => Assert.Throws<ArgumentException>(() => ComparaisonReference.Comparer(
        Zinc(1m, 1m), new ReferenceNutriment("magnesium_mg",
            StatutReference.LimiteHauteEtablie,
            MasseNutriment.De(250m, UniteNutriment.Milligramme), "SCF", 2001)));
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "distingue une limite haute d'un simple niveau sûr d'apport"`

---

## Tâche 11 : la force estimée, avec sa fiabilité ou rien

**Fichiers :** `Entrainement/Fiabilite.cs`, `ForceEstimee.cs` · `Tests/Entrainement/ForceEstimeeTests.cs`

**Interfaces produites :**

```csharp
public enum Fiabilite { Bonne, Moyenne, Faible }

public sealed record EstimationForce(Charge UnRepetitionMaximum, Fiabilite Fiabilite);

public static class ForceEstimee
{
    public const int RepetitionsEffectivesFiables = 8;
    public const int RepetitionsEffectivesMoyennes = 12;
    public const int RepetitionsEffectivesMaximum = 15;

    public static EstimationForce? Epley(Charge charge, Repetitions repetitions, Rir? rir);
}
```

**Elle rend `null` au-delà de quinze répétitions effectives.** `05-entrainement.md` § 3 : « au-delà de 15, ne rien afficher ». Un `Charge` rendu quand même serait indiscernable d'une estimation valide. La littérature confirme le seuil : erreur sous 0,03 entre 3 et 8 répétitions, dégradation au-delà de 10, et « beyond 12 reps, prediction error increases significantly ».

**Le RIR absent vaut 2**, hypothèse prudente imposée par le document, et confortée par l'ACSM 2026 qui fait de 2-3 RIR la recommandation de référence.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
[Fact]
public void Epley_applique_la_formule_avec_le_RIR()
{
    var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), Rir.De(2));
    // répétitions effectives 10 → 100 × (1 + 10/30) = 133,33…
    Assert.Equal(133.33m, Math.Round(r!.UnRepetitionMaximum.Kilogrammes, 2));
    Assert.Equal(Fiabilite.Moyenne, r.Fiabilite);   // 10 effectives : au-delà de 8
}

[Fact]
public void Epley_suppose_un_RIR_de_deux_quand_il_manque()
{
    var avec = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), Rir.De(2));
    var sans = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), null);
    Assert.Equal(avec!.UnRepetitionMaximum, sans!.UnRepetitionMaximum);
}

[Fact]
public void Une_serie_courte_donne_une_bonne_fiabilite()
{
    var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(120m), Repetitions.De(5), Rir.De(1));
    Assert.Equal(Fiabilite.Bonne, r!.Fiabilite);    // 6 effectives
}

[Fact]
public void Au_dela_de_douze_effectives_la_fiabilite_devient_faible()
{
    var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(60m), Repetitions.De(12), Rir.De(2));
    Assert.Equal(Fiabilite.Faible, r!.Fiabilite);   // 14 effectives
}

[Fact]
public void Au_dela_de_quinze_effectives_rien_n_est_rendu()
{
    Assert.Null(ForceEstimee.Epley(Charge.DepuisKilogrammes(40m), Repetitions.De(14), Rir.De(2)));
}

[Fact]
public void La_borne_de_quinze_est_incluse()
{
    var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(40m), Repetitions.De(13), Rir.De(2));
    Assert.NotNull(r);                               // 15 effectives, encore rendu
    Assert.Equal(Fiabilite.Faible, r.Fiabilite);
}

[Fact]
public void Epley_refuse_une_charge_nulle()
{
    // le poids de corps est une charge légitime, mais 0 kg ne permet aucune
    // estimation de force : 0 × quoi que ce soit reste 0.
    Assert.Throws<ArgumentOutOfRangeException>(
        () => ForceEstimee.Epley(Charge.DepuisKilogrammes(0m), Repetitions.De(5), Rir.De(2)));
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "fait taire la force estimée quand la série est trop longue"`

---

## Tâche 12 : le volume par groupe musculaire

**Fichiers :** `Entrainement/VolumeParGroupe.cs` · `Tests/Entrainement/VolumeParGroupeTests.cs`

**Interfaces produites :**

```csharp
public enum RoleMouvement { Tirage, Poussee, NiTirageNiPoussee }

public sealed record SerieEffectuee(string GroupeMusculaire, RoleMouvement Role, DateOnly Jour);

public sealed record BilanVolume(
    IReadOnlyDictionary<string, int> SeriesParGroupe,
    decimal? RatioTiragePoussee,
    IReadOnlyCollection<string> GroupesSousLaCible);

public static class VolumeParGroupe
{
    public static BilanVolume SurSeptJours(IReadOnlyCollection<SerieEffectuee> series,
                                            DateOnly finDeFenetre,
                                            int cibleSeriesParGroupe,
                                            decimal ratioTiragePousseeCible);
}
```

**Les deux références arrivent en paramètre**, jamais en constante. La cible de séries vient de l'ACSM 2026 — environ 10 par groupe et par semaine, chaque groupe travaillé deux fois — et le ratio de 1,3 est un repère d'équilibre que la spec § 3 a explicitement **déclassé** : ce n'est pas un seuil de santé, et le domaine ne doit pas le figer.

**`NiTirageNiPoussee` existe pour le deltoïde latéral**, que `05-entrainement.md` § 4 exclut du ratio : « il ne tire ni ne pousse ».

**Le ratio est `null` quand il n'y a aucune poussée** — une division par zéro n'est pas un ratio infini, c'est une absence de mesure.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
private static readonly DateOnly Fin = new(2026, 8, 21);
private static SerieEffectuee S(string groupe, RoleMouvement role, int joursAvant) =>
    new(groupe, role, Fin.AddDays(-joursAvant));

[Fact]
public void Les_series_sont_comptees_par_groupe()
{
    var b = VolumeParGroupe.SurSeptJours(
        [S("dos", RoleMouvement.Tirage, 0), S("dos", RoleMouvement.Tirage, 2),
         S("pectoraux", RoleMouvement.Poussee, 1)], Fin, 10, 1.3m);
    Assert.Equal(2, b.SeriesParGroupe["dos"]);
    Assert.Equal(1, b.SeriesParGroupe["pectoraux"]);
}

[Fact]
public void Une_serie_hors_fenetre_de_sept_jours_est_ignoree()
{
    var b = VolumeParGroupe.SurSeptJours(
        [S("dos", RoleMouvement.Tirage, 7)], Fin, 10, 1.3m);
    Assert.Empty(b.SeriesParGroupe);
}

[Fact]
public void La_borne_de_six_jours_reste_dans_la_fenetre()
{
    var b = VolumeParGroupe.SurSeptJours(
        [S("dos", RoleMouvement.Tirage, 6)], Fin, 10, 1.3m);
    Assert.Equal(1, b.SeriesParGroupe["dos"]);
}

[Fact]
public void Le_ratio_tirage_poussee_est_calcule()
{
    var series = Enumerable.Range(0, 13).Select(_ => S("dos", RoleMouvement.Tirage, 1))
        .Concat(Enumerable.Range(0, 10).Select(_ => S("pectoraux", RoleMouvement.Poussee, 1)))
        .ToArray();
    var b = VolumeParGroupe.SurSeptJours(series, Fin, 10, 1.3m);
    Assert.Equal(1.3m, b.RatioTiragePoussee);
}

[Fact]
public void Le_deltoide_lateral_est_exclu_du_ratio()
{
    var b = VolumeParGroupe.SurSeptJours(
        [S("dos", RoleMouvement.Tirage, 1), S("pectoraux", RoleMouvement.Poussee, 1),
         S("deltoide_lateral", RoleMouvement.NiTirageNiPoussee, 1)], Fin, 10, 1.3m);
    Assert.Equal(1m, b.RatioTiragePoussee);
    Assert.Equal(1, b.SeriesParGroupe["deltoide_lateral"]);   // compté au volume
}

[Fact]
public void Sans_poussee_le_ratio_n_existe_pas()
{
    var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, 1)], Fin, 10, 1.3m);
    Assert.Null(b.RatioTiragePoussee);
}

[Fact]
public void Les_groupes_sous_la_cible_sont_nommes()
{
    var series = Enumerable.Range(0, 10).Select(_ => S("dos", RoleMouvement.Tirage, 1))
        .Append(S("pectoraux", RoleMouvement.Poussee, 1)).ToArray();
    var b = VolumeParGroupe.SurSeptJours(series, Fin, 10, 1.3m);
    Assert.DoesNotContain("dos", b.GroupesSousLaCible);        // 10, la cible est atteinte
    Assert.Contains("pectoraux", b.GroupesSousLaCible);        // 1
}

[Fact]
public void Une_semaine_vide_ne_fait_pas_echouer_le_bilan()
{
    var b = VolumeParGroupe.SurSeptJours([], Fin, 10, 1.3m);
    Assert.Empty(b.SeriesParGroupe);
    Assert.Null(b.RatioTiragePoussee);
    Assert.Empty(b.GroupesSousLaCible);
}

[Fact]
public void Une_cible_non_positive_est_refusee()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => VolumeParGroupe.SurSeptJours([], Fin, 0, 1.3m));

[Fact]
public void Un_role_hors_enumeration_est_refuse() { /* (RoleMouvement)99 */ }
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "compte le volume par groupe et exclut le deltoïde du ratio"`

---

## Tâche 13 : la détection de plateau

**Fichiers :** `Entrainement/DetectionPlateau.cs` · `Tests/Entrainement/DetectionPlateauTests.cs`

**Interfaces produites :**

```csharp
public sealed record SeanceDExercice(DateOnly Jour, Charge ChargeMaximale, int RepetitionsALaChargeMaximale);

public static class DetectionPlateau
{
    public const int SeancesConsecutives = 3;
    public static bool EstEnPlateau(IReadOnlyList<SeanceDExercice> seances);
}
```

**La règle, mot pour mot :** `05-entrainement.md` § 2 — « même charge maximale sur trois séances consécutives sans progression du nombre de répétitions ». Les deux conditions sont nécessaires : une charge identique dont les répétitions montent est une progression, pas un plateau.

Le domaine **constate**, il ne prescrit pas : la fonction rend un booléen, et le document précise que le système « rappelle les leviers disponibles et laisse l'utilisateur décider ».

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
private static SeanceDExercice Seance(int jour, decimal charge, int repetitions) =>
    new(new DateOnly(2026, 8, jour), Charge.DepuisKilogrammes(charge), repetitions);

[Fact]
public void Trois_seances_identiques_sont_un_plateau()
    => Assert.True(DetectionPlateau.EstEnPlateau(
        [Seance(10, 60m, 8), Seance(14, 60m, 8), Seance(18, 60m, 8)]));

[Fact]
public void Une_progression_des_repetitions_n_est_pas_un_plateau()
    => Assert.False(DetectionPlateau.EstEnPlateau(
        [Seance(10, 60m, 8), Seance(14, 60m, 9), Seance(18, 60m, 10)]));

[Fact]
public void Une_progression_de_charge_n_est_pas_un_plateau()
    => Assert.False(DetectionPlateau.EstEnPlateau(
        [Seance(10, 60m, 8), Seance(14, 62.5m, 8), Seance(18, 65m, 8)]));

[Fact]
public void Seules_les_trois_dernieres_seances_comptent()
    => Assert.True(DetectionPlateau.EstEnPlateau(
        [Seance(2, 50m, 6), Seance(10, 60m, 8), Seance(14, 60m, 8), Seance(18, 60m, 8)]));

[Fact]
public void Un_recul_des_repetitions_est_aussi_un_plateau()
    // « sans progression » couvre l'égalité ET la baisse : la charge stagne et
    // les répétitions ne montent pas.
    => Assert.True(DetectionPlateau.EstEnPlateau(
        [Seance(10, 60m, 9), Seance(14, 60m, 8), Seance(18, 60m, 8)]));

[Fact]
public void Moins_de_trois_seances_ne_conclut_pas()
    => Assert.False(DetectionPlateau.EstEnPlateau([Seance(14, 60m, 8), Seance(18, 60m, 8)]));

[Fact]
public void Une_liste_vide_ne_conclut_pas()
    => Assert.False(DetectionPlateau.EstEnPlateau([]));
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "détecte le plateau sur charge stable et répétitions qui ne montent plus"`

---

## Tâche 14 : les deux détections de sécurité

**Fichiers :** `Securite/PerteDePoidsRapide.cs`, `RestrictionSevere.cs` · `Tests/Securite/DetectionsDeSecuriteTests.cs`

**Interfaces produites :**

```csharp
public sealed record Pesee(DateOnly Jour, Masse Poids);

public static class PerteDePoidsRapide
{
    public const decimal SeuilHebdomadaire = 0.01m;      // 1 % du poids
    public const int SemainesConsecutives = 3;
    public static bool EstDetectee(IReadOnlyList<Pesee> pesees);
}

public sealed record JourneeDApport(DateOnly Jour, Energie Apport);

public static class RestrictionSevere
{
    public const decimal FractionDuMetabolismeDeBase = 0.8m;
    public const int JoursConsecutifs = 5;
    public static bool EstDetectee(IReadOnlyList<JourneeDApport> journees, Energie metabolismeDeBase);
}
```

**Les seuils viennent de `01-conformite.md` § 5** : « plus de 1 % du poids par semaine sur trois semaines » et « apports très en dessous du métabolisme de base répétés ». Le second n'est pas chiffré par le document ; **80 % du métabolisme de base sur cinq jours consécutifs** est le chiffrage retenu, exposé en constante publique pour qu'un diététicien puisse le corriger sans lire le code.

**Ces fonctions rendent un booléen, jamais un message.** Le document impose une « orientation vers un professionnel » et interdit « tout renforcement de la restriction » — c'est une affaire de libellé, donc de base et d'i18next, pas de domaine.

- [ ] **Étape 1 — écrire les tests qui échouent**

```csharp
private static Pesee P(int jour, decimal kg) =>
    new(new DateOnly(2026, 8, jour), Masse.DepuisKilogrammes(kg));

[Fact]
public void Une_perte_de_plus_d_un_pour_cent_par_semaine_sur_trois_semaines_est_detectee()
    // 80 → 78,4 → 76,8 → 75,2 : environ 2 % par semaine
    => Assert.True(PerteDePoidsRapide.EstDetectee(
        [P(1, 80m), P(8, 78.4m), P(15, 76.8m), P(22, 75.2m)]));

[Fact]
public void Une_perte_lente_n_est_pas_detectee()
    // 80 → 79,6 → 79,2 → 78,8 : 0,5 % par semaine
    => Assert.False(PerteDePoidsRapide.EstDetectee(
        [P(1, 80m), P(8, 79.6m), P(15, 79.2m), P(22, 78.8m)]));

[Fact]
public void Une_seule_semaine_rapide_ne_declenche_pas()
    => Assert.False(PerteDePoidsRapide.EstDetectee(
        [P(1, 80m), P(8, 78m), P(15, 77.8m), P(22, 77.6m)]));

[Fact]
public void Une_prise_de_poids_n_est_jamais_detectee()
    => Assert.False(PerteDePoidsRapide.EstDetectee(
        [P(1, 75m), P(8, 76m), P(15, 77m), P(22, 78m)]));

[Fact]
public void Moins_de_trois_semaines_ne_conclut_pas()
    => Assert.False(PerteDePoidsRapide.EstDetectee([P(1, 80m), P(8, 78m)]));

[Fact]
public void Une_liste_vide_ne_conclut_pas()
    => Assert.False(PerteDePoidsRapide.EstDetectee([]));

[Fact]
public void Cinq_jours_sous_quatre_vingts_pour_cent_du_metabolisme_sont_detectes()
{
    var journees = Enumerable.Range(1, 5)
        .Select(j => new JourneeDApport(new DateOnly(2026, 8, j),
                                        Energie.DepuisKilocalories(1200m)))
        .ToArray();
    Assert.True(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
}

[Fact]
public void Un_apport_juste_au_dessus_du_seuil_n_est_pas_detecte()
{
    var journees = Enumerable.Range(1, 5)
        .Select(j => new JourneeDApport(new DateOnly(2026, 8, j),
                                        Energie.DepuisKilocalories(1281m)))
        .ToArray();   // 1600 × 0,8 = 1280
    Assert.False(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
}

[Fact]
public void Une_journee_normale_interrompt_la_serie()
{
    var journees = new[]
    {
        new JourneeDApport(new DateOnly(2026, 8, 1), Energie.DepuisKilocalories(1200m)),
        new JourneeDApport(new DateOnly(2026, 8, 2), Energie.DepuisKilocalories(1200m)),
        new JourneeDApport(new DateOnly(2026, 8, 3), Energie.DepuisKilocalories(2000m)),
        new JourneeDApport(new DateOnly(2026, 8, 4), Energie.DepuisKilocalories(1200m)),
        new JourneeDApport(new DateOnly(2026, 8, 5), Energie.DepuisKilocalories(1200m)),
        new JourneeDApport(new DateOnly(2026, 8, 6), Energie.DepuisKilocalories(1200m)),
    };
    Assert.False(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
}

[Fact]
public void Moins_de_cinq_journees_ne_conclut_pas()
    => Assert.False(RestrictionSevere.EstDetectee([], Energie.DepuisKilocalories(1600m)));

[Fact]
public void Un_metabolisme_de_base_nul_est_refuse()
    => Assert.Throws<ArgumentOutOfRangeException>(
        () => RestrictionSevere.EstDetectee([], Energie.DepuisKilocalories(0m)));
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** - [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — commiter** : `git commit -m "détecte la perte rapide et la restriction sévère, sans rédiger le message"`

---

## Tâche 15 : le garde-fou qui interdit les constantes de référence

**Fichiers :** `back/tests-harness/` — ajouter à la suite d'épreuves du backend · fixture dédiée

**Ce qu'il éprouve.** La spec § 2 pose que « `Palier.Domain` ne connaît aucune valeur de limite haute » et qu'« une épreuve du harnais doit refuser qu'on en écrive une ». Sans elle, la règle est une intention, pas une garantie — et le lot 1 a payé quatre fois pour apprendre qu'une règle écrite n'est pas une règle en vigueur.

**Ce qu'il cherche :** dans `back/Palier.Domain/Nutriments/`, toute valeur numérique d'UL connue — 25, 12, 255, 3000, 100, 1000, 2500, 600, 250, 900 — accompagnée d'un nom de nutriment. Le détecteur lit **l'index git**, jamais le disque : c'est la correction du 21/08 sur `tests-harness/db.test.mjs`, et la répéter ici éviterait de refaire le même faux positif.

- [ ] **Étape 1 — écrire l'épreuve, et la faire échouer sur une fixture qui viole**

Créer un fichier de fixture portant `public const decimal ZincUlMg = 25m;`, vérifier que l'épreuve rougit **avec un motif propre à la cause**, puis retirer la fixture et vérifier le vert. Deux assertions : code de sortie **et** motif.

- [ ] **Étape 2 — vérifier la branche « aucune cible »**

Renommer temporairement le dossier surveillé et constater que l'épreuve **crie** au lieu de passer au vert en ne lisant rien.

- [ ] **Étape 3 — commiter**

```bash
git commit -m "refuse qu'une limite haute soit écrite en dur dans le domaine"
```

---

## Tâche 16 : clôture

- [ ] **Étape 1 — `npm run verify`**, les seize étapes. La couverture de `Palier.Domain` doit être à 100 % en lignes, branches et méthodes ; le seuil fait échouer `dotnet test` sinon.
- [ ] **Étape 2 — consigner les décisions** dans `docs/decisions.md` : le choix de Mifflin-St Jeor, la restriction de Katch-McArdle aux mesures fiables, le passage au MET, les quatre statuts de référence, la cible de volume ACSM 2026. Chacune avec son motif et **ce qui la rouvrirait**.
- [ ] **Étape 3 — remplir la ligne du lot 3** dans le tableau de `docs/07-roadmap.md` § « Étapes et lots », par mesure et non par souvenir (D43).
- [ ] **Étape 4 — écrire le journal de bord** dans `.superpowers/sdd/2026-08-21-lot-3-domaine/progress.md`, aux quatre sections du contrat de sortie : ce qui a changé, ce qui a cassé, ce que je signale sans y avoir touché, le franchissement.
- [ ] **Étape 5 — pousser.**

---

## Auto-revue du plan

**Couverture de la spec.** § 2 (quatre statuts, aucune valeur en dur) → tâches 10 et 15. § 3 écart 1 (Mifflin conservée, Katch sur mesure fiable) → tâches 2 et 5. § 3 écart 2 (MET) → tâche 6. § 3 écart 3 (ratio déclassé) → tâche 12. § 3 ACSM 2026 (volume) → tâche 12. § 4 (chaîne de l'apport) → tâches 5, 6, 7, 9. § 5 (grandeurs) → tâches 1 à 4. § 6 (trois retours protecteurs) → `DepenseEstimee` en tâche 6, `ResultatComparaison` en tâche 10, `EstimationForce?` en tâche 11. § 7 (quatre planchers) → tâches 8, 9, 14. § 9 (ce qui casse) → tâche 5.

**Aucun reste.** Les entités riches sont explicitement écartées, avec leur motif.

**Cohérence des types.** `Energie` est le retour de tous les calculs énergétiques, jamais un `decimal` nu. `Masse` sert au poids corporel et à la masse maigre ; `Charge` à la barre. `MasseNutriment` porte toujours son unité. `Sexe` reste dans `Palier.Domain.Energie`, où il vit déjà — les autres espaces de noms l'importent plutôt que de le déplacer, pour ne pas casser l'existant.
