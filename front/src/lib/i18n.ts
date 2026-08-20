/**
 * Câblage d'i18next. Le français et l'anglais existent dès la première ligne —
 * `CLAUDE.md` § 4 : aucune chaîne de caractères en dur, y compris les erreurs
 * et les états vides.
 *
 * Les deux catalogues sont importés STATIQUEMENT plutôt que chargés au vol :
 * un chargement asynchrone rendrait la clé brute pendant sa résolution, et un
 * catalogue manquant échouerait en silence à l'exécution au lieu d'échouer à
 * la compilation. `tests/harness/i18n.test.ts` vérifie par ailleurs que les
 * deux portent rigoureusement les mêmes clés, dans les deux sens.
 */
import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from '../locales/en.json'
import fr from '../locales/fr.json'

/** `VITE_DEFAULT_LOCALE` existe déjà dans `front/.env.example`. Le repli est le
 * français, jamais l'anglais : c'est la langue du produit. */
const LANGUE_PAR_DEFAUT = import.meta.env.VITE_DEFAULT_LOCALE ?? 'fr'

export const LANGUES = ['fr', 'en'] as const

await i18next.use(initReactI18next).init({
  resources: {
    fr: { translation: fr },
    en: { translation: en },
  },
  lng: LANGUES.includes(LANGUE_PAR_DEFAUT as (typeof LANGUES)[number])
    ? LANGUE_PAR_DEFAUT
    : LANGUES[0],
  fallbackLng: LANGUES[0],
  interpolation: { escapeValue: false },
  // Une clé absente doit se voir. Le défaut d'i18next est d'afficher la clé
  // elle-même, ce qui est exactement ce qu'on veut : « commun.vide » à l'écran
  // est un défaut visible, une chaîne vide ne l'est pas.
  returnEmptyString: false,
})

export default i18next
