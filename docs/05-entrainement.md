# 05 — Logique d'entraînement

Modules purs et testés. Le LLM explique ces règles, il ne les invente pas.

---

## 1. Principes de programmation

| Paramètre | Règle |
|---|---|
| Volume | 10 à 20 séries dures par groupe musculaire et par semaine. Démarrer bas |
| Fréquence | Chaque muscle stimulé 2 fois par semaine minimum |
| Structure | Selon la fréquence réelle, voir le tableau ci-dessous |
| Intensité | 0 à 3 RIR en régime normal, 3 à 4 en reprise |
| Progression | Charge, puis répétitions, puis séries, puis densité — dans cet ordre |
| Bloc | 4 à 6 semaines, suivies d'un allègement |
| Force | 3 à 6 répétitions, 85 % et plus, 3 à 5 min de repos |
| Hypertrophie | 6 à 15 répétitions, 90 à 180 s de repos |
| Endurance | 15 à 30 répétitions, repos court, en fin de séance |

### Structures selon la fréquence

| Séances/semaine | Structure | Fréquence par muscle |
|---|---|---|
| 3 | Full body | 3× |
| 4 | Upper / Lower | 2× |
| 5 | Upper / Lower / Push / Pull / Legs | 1,7× |
| 6 | Push / Pull / Legs ×2 | 2× |
| 6-7 | Split spécialisé, avec priorisation | 1-2× |

Le split par groupe musculaire devient légitime à partir de cinq séances hebdomadaires. En dessous, la fréquence par muscle tombe sous 2 et le produit le signale — **comme une information chiffrée, jamais comme un reproche** : « avec cette structure, chaque muscle est travaillé 1 fois par semaine ».

Le produit propose la structure adaptée à la fréquence déclarée, et laisse l'utilisateur en choisir une autre.

Le comptage du volume attribue 1 série au muscle primaire et 0,5 aux secondaires.

---

## 2. Progression automatique

Règle par défaut, exposée et modifiable :

```
si (toutes les séries atteignent la cible de répétitions)
   et (RIR moyen ≥ RIR cible)
alors proposer charge + increment
```

L'incrément vient de `exercises.default_increment` : 5 kg à la presse, 2,5 kg aux poulies et machines, 2 kg aux haltères, 1 kg sur les élévations et les mouvements de rotateurs.

**Plateau :** même charge maximale sur trois séances consécutives sans progression du nombre de répétitions. Le système le signale, sans prescrire de solution — il rappelle les leviers disponibles (tempo, série supplémentaire, allègement) et laisse l'utilisateur décider.

**Formulation :** « Aujourd'hui : 24 kg (+2) ». Jamais « tu dois passer à 24 kg ».

---

## 3. Force estimée

Epley corrigé par le RIR, pour ne jamais demander de test maximal :

```
répétitions effectives = répétitions réalisées + RIR
1RM estimé = charge × (1 + répétitions effectives / 30)
```

Au-delà de 12 répétitions effectives, la marge d'erreur dépasse 10 % : le signaler à l'affichage. Au-delà de 15, ne rien afficher.

Si le RIR n'est pas renseigné, supposer 2 — hypothèse prudente — et le mentionner.

**Aucun test de charge maximale n'est jamais proposé par le produit.** Les tissus conjonctifs s'adaptent en semaines, le système nerveux en séances : c'est précisément l'écart qui blesse les pratiquants en reprise.

---

## 4. Adaptation par contrainte — le second différenciateur

Chaque exercice porte `contraindicated_for`. Les contraintes déclarées par l'utilisateur filtrent automatiquement le catalogue.

| Contrainte | Exclusions | Priorités |
|---|---|---|
| Cervicale | Charge axiale, développés au-dessus de la tête, shrugs | Face pull, Y-T-W, tirages buste soutenu |
| Lombaire | Soulevé de terre lourd, squat barre, good morning chargé | Charnière légère, gainage antirotation |
| Épaule | Développé militaire, dips lestés, écarté en amplitude maximale | Rotateurs externes, angles neutres |
| Genou | Fentes profondes, extensions lourdes en fin d'amplitude | Charnière de hanche, amplitude partielle contrôlée |

**Cas cervical, à traiter comme référence :** pas de charge axiale, rien au-dessus de la tête, travail direct des trapèzes moyens et inférieurs à chaque séance. Les shrugs sont exclus — ils renforcent en position raccourcie des trapèzes supérieurs déjà tendus.

**Ratio tirage/poussée :** dos et trapèzes contre pectoraux et triceps, sur 7 jours. Cible minimale 1,3 pour une population de travail sédentaire. Le deltoïde latéral est exclu du calcul — il ne tire ni ne pousse.

---

## 5. Ressenti par exercice

Trois états : `good`, `meh`, `pain`.

| Signal | Réponse du système |
|---|---|
| 1 `pain` sur les 3 dernières séances | Rappel des leviers : charge, amplitude, technique |
| 2 `pain` consécutifs | Retrait proposé, remplacement par une variante, **et orientation vers un professionnel** |
| 3 `meh` consécutifs | Proposition de variante — un exercice qu'on n'aime pas est un exercice qu'on cesse de faire |

Le ressenti pilote l'adaptation. Il vaut mieux qu'une rotation calendaire : l'utilisateur change ce qui ne va pas, pas ce qui fonctionne.

---

## 6. Variation des exercices

**Les mouvements socles ne tournent pas.** Presse, charnière de hanche, développés, tirages restent fixes sur tout un bloc : c'est sur eux que se mesure la progression, et changer de mouvement détruit le repère de surcharge.

Seuls les accessoires proposent des variantes, à partir de 4 semaines. La variation aide surtout quand le volume est déjà élevé et le pratiquant avancé ; en reprise, la répétition des schémas moteurs prime.

---

## 7. Sécurité

**Règle d'arrêt affichée en permanence pendant une séance :**

> Fourmillement, engourdissement ou perte de force dans un bras ou une jambe : arrêter la séance et consulter avant la suivante.

Escalade immédiate, avec interruption du fil normal (voir `01-conformite.md`) : douleur thoracique, vertiges à l'effort, symptôme neurologique, douleur articulaire persistante.

**Allègement :** proposé en semaine 5 ou 6 d'un bloc — deux séries au lieu de trois, charges réduites de 40 %. Proposé, jamais imposé.
