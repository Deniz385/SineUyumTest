import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

// Every src/locales/<code>.json file is bundled. To add a language, drop in
// its translation file and list it here; nothing else needs to change.
// Labels are each language's own name so people can always find theirs.
export const SUPPORTED_LANGUAGES = [
    { code: 'tr', label: 'Türkçe' },
    { code: 'en', label: 'English' },
];

export const FALLBACK_LANGUAGE = 'en';
const STORAGE_KEY = 'language';

const localeFiles = import.meta.glob('./locales/*.json', { eager: true, import: 'default' });
const resources = Object.fromEntries(
    SUPPORTED_LANGUAGES.map(({ code }) => [code, { translation: localeFiles[`./locales/${code}.json`] }])
);

const isSupported = (code) => SUPPORTED_LANGUAGES.some((language) => language.code === code);

// Storage can throw (private mode, blocked site data), so the choice is
// simply not remembered in that case.
const readSavedLanguage = () => {
    try {
        return localStorage.getItem(STORAGE_KEY);
    } catch {
        return null;
    }
};

const saveLanguage = (code) => {
    try {
        localStorage.setItem(STORAGE_KEY, code);
    } catch {
        // Ignore: the language still changes for this visit.
    }
};

// Saved choice first, then the browser's preferred languages ("tr-TR" counts
// as "tr"), then English.
const detectLanguage = () => {
    const saved = readSavedLanguage();
    if (isSupported(saved)) return saved;

    const preferred = navigator.languages?.length ? navigator.languages : [navigator.language];
    const match = preferred
        .filter(Boolean)
        .map((tag) => tag.toLowerCase().split('-')[0])
        .find(isSupported);
    return match || FALLBACK_LANGUAGE;
};

// <html lang> must always match the active language: CSS text-transform
// (MUI buttons, overlines) uppercases by it, and Turkish turns i into a dotted capital I.
const applyToDocument = (code) => {
    document.documentElement.lang = code;
    document.title = i18n.t('app.documentTitle');
};

i18n.on('languageChanged', applyToDocument);

i18n.use(initReactI18next).init({
    resources,
    lng: detectLanguage(),
    fallbackLng: FALLBACK_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES.map(({ code }) => code),
    // Resources are bundled, so there is nothing to wait for.
    initAsync: false,
    // React already escapes rendered values.
    interpolation: { escapeValue: false },
});

applyToDocument(i18n.language);

// Explicit choice from a language switcher: remembered for next visits.
export const changeLanguage = (code) => {
    if (!isSupported(code)) return Promise.resolve();
    saveLanguage(code);
    return i18n.changeLanguage(code);
};

export default i18n;
