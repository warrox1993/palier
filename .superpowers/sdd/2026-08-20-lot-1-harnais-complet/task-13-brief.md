# Brief — Tâche 13

> Extrait de `docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

- **Node 24** (`.nvmrc`) et **.NET 10** (`global.json`, SDK 10.0.303, `rollForward: latestFeature`).
- **npm** côté front, jamais pnpm ni yarn. En CI : `npm ci`, jamais `npm install`.
- **Aucune version de paquet figée dans ce plan.** Les installations se font sans numéro ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée doit passer la liste blanche de licences** — MIT, Apache-2.0, BSD, ISC, PostgreSQL. Décision D13 du journal. MediatR est exclu : sa licence RPL-1.5 obligerait à publier le code source d'un service commercial.
- **Messages de commit en français, à l'impératif** — `16-projet.md` § 2.
- **Nommage front** : fichiers `kebab-case.ts`, composants `PascalCase.tsx`, fonctions et variables `camelCase`, constantes `SCREAMING_SNAKE_CASE`, booléens en `is`/`has`/`can`.
- **Nommage C#** : fichiers et types en `PascalCase`, un type public par fichier, champs privés en `_camelCase`.
- **Tests à côté du source** côté front — `<fichier>.test.ts`.
- **Aucun secret dans le dépôt.** `.env.example` versionné, `.env` ignoré.
- **`front/tests/harness/fixtures/` et `back/tests-harness/fixtures/`** hébergent les violations délibérées, exclues du typecheck et du build.
- **`Palier.Domain` ne référence aucun projet** et aucun paquet d'accès aux données, réseau ou UI. Traduction de `01-conformite.md` § 3.
- **Couverture 100 % sur `Palier.Domain`** — `08-workflow.md` § 6. Aucun seuil global ailleurs.
- **Une seule branche de travail** : `feat/lot-1-harnais`, déjà active.
- Dépôt distant : `github.com/warrox1993/palier`, privé, branche par défaut `main`.

---

## Tâche 13 : xUnit et la première fonction du domaine

**Fichiers :**
- Créer : `back/Palier.Domain.Tests/`, `back/Palier.Domain/Energie/MetabolismeDeBase.cs`, son test
- Modifier : `back/Palier.sln`

**Interfaces :**
- Consomme : la solution, la rigueur
- Produit : `MetabolismeDeBase.MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, int age) → decimal`, consommée par le lot 3.

> Cette tâche installe l'outillage de test **et** la première fonction réelle, parce qu'un harnais de test sans rien à tester ne prouve rien.

- [ ] **Étape 1 : créer le projet de tests**

```bash
cd back
dotnet new xunit --name Palier.Domain.Tests --output Palier.Domain.Tests
dotnet sln add Palier.Domain.Tests
dotnet add Palier.Domain.Tests reference Palier.Domain
```

Supprimer le fichier de test généré par le template.

- [ ] **Étape 2 : écrire le test AVANT l'implémentation**

`back/Palier.Domain.Tests/Energie/MetabolismeDeBaseTests.cs` :

```csharp
using Palier.Domain.Energie;

namespace Palier.Domain.Tests.Energie;

public sealed class MetabolismeDeBaseTests
{
    // docs/04-nutrition.md § 1 :
    // Homme : 10 × poids + 6,25 × taille − 5 × âge + 5
    // Femme : 10 × poids + 6,25 × taille − 5 × âge − 161
    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poidsKg: 73m, tailleCm: 178m, age: 35m);
        // 730 + 1112,5 − 175 + 5 = 1672,5
        Assert.Equal(1672.5m, r);
    }

    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Femme, poidsKg: 60m, tailleCm: 165m, age: 30m);
        // 600 + 1031,25 − 150 − 161 = 1320,25
        Assert.Equal(1320.25m, r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MifflinStJeor_refuse_un_poids_non_positif(int poids)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poids, 178m, 35m));
    }
}
```

`decimal` et non `double` : `16-projet.md` § 2 interdit le flottant pour les quantités nutritionnelles, et la raison vaut aussi pour les calculs intermédiaires.

- [ ] **Étape 3 : lancer le test pour le voir échouer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : ÉCHEC de compilation — `MetabolismeDeBase` et `Sexe` n'existent pas.

- [ ] **Étape 4 : écrire l'implémentation minimale**

`back/Palier.Domain/Energie/Sexe.cs` :

```csharp
namespace Palier.Domain.Energie;

public enum Sexe
{
    Homme,
    Femme,
}
```

`back/Palier.Domain/Energie/MetabolismeDeBase.cs` :

```csharp
namespace Palier.Domain.Energie;

/// <summary>
/// Métabolisme de base. Voir docs/04-nutrition.md § 1.
/// Module pur : aucun accès réseau, base ou interface.
/// </summary>
public static class MetabolismeDeBase
{
    public static decimal MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, decimal age)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poidsKg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tailleCm);
        ArgumentOutOfRangeException.ThrowIfNegative(age);

        var socle = (10m * poidsKg) + (6.25m * tailleCm) - (5m * age);
        return sexe switch
        {
            Sexe.Homme => socle + 5m,
            Sexe.Femme => socle - 161m,
            _ => throw new ArgumentOutOfRangeException(nameof(sexe)),
        };
    }
}
```

- [ ] **Étape 5 : lancer le test pour le voir passer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : 4 tests passent (2 faits + 2 cas de théorie).

- [ ] **Étape 6 : éprouver que le harnais de test mord**

Ajouter temporairement un test qui échoue, vérifier que `dotnet test` sort en code non nul, puis le retirer. Consigner la sortie dans le rapport.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "installe xUnit et le calcul du métabolisme de base"
```

---

---
