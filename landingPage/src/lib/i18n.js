import { get, writable, derived } from "svelte/store";
import en from "./i18n/en.js";
import de from "./i18n/de.js";
import zh from "./i18n/zh.js";
import hi from "./i18n/hi.js";
import es from "./i18n/es.js";
import fr from "./i18n/fr.js";
import it from "./i18n/it.js";
import ar from "./i18n/ar.js";
import bn from "./i18n/bn.js";
import pt from "./i18n/pt.js";
import ro from "./i18n/ro.js";
import ru from "./i18n/ru.js";
import ur from "./i18n/ur.js";
import pl from "./i18n/pl.js";

export const translations = {
  en,
  de,
  es,
  fr,
  it,
  pt,
  ro,
  pl,
  ru,
  zh,
  hi,
  ar,
  bn,
  ur,
};

const fallbackLang = "en";
const rtlLangs = new Set(["ar", "ur"]);

export const langs = Object.keys(translations);
export const language = writable(fallbackLang);

function getByPath(obj, path) {
  return path.split(".").reduce((acc, part) => (acc != null ? acc[part] : undefined), obj);
}

function normalizeLang(input) {
  if (!input) return "";
  return input.toLowerCase().replace("_", "-").split("-")[0];
}

function syncDocumentLanguage(lang) {
  if (typeof document === "undefined") return;
  document.documentElement.lang = lang;
  document.documentElement.dir = rtlLangs.has(lang) ? "rtl" : "ltr";
}

export const t = derived(language, ($language) => {
  return (key) => {
    const activeValue = getByPath(translations[$language], key);
    if (activeValue !== undefined) {
      return activeValue;
    }

    const fallbackValue = getByPath(translations[fallbackLang], key);
    return fallbackValue !== undefined ? fallbackValue : key;
  };
});

export function setLanguage(nextLang) {
  const normalized = normalizeLang(nextLang);
  const lang = langs.includes(normalized) ? normalized : fallbackLang;
  language.set(lang);
  syncDocumentLanguage(lang);
  localStorage.setItem("wg-lang", lang);
}

function resolveBrowserLanguage() {
  const browserCandidates = [];

  if (Array.isArray(navigator.languages)) {
    browserCandidates.push(...navigator.languages);
  }

  if (navigator.language) {
    browserCandidates.push(navigator.language);
  }

  for (const candidate of browserCandidates) {
    const normalized = normalizeLang(candidate);
    if (langs.includes(normalized)) {
      return normalized;
    }
  }

  return fallbackLang;
}

export function initLanguage() {
  const saved = localStorage.getItem("wg-lang");
  const candidate = saved || resolveBrowserLanguage();
  setLanguage(candidate);
}

export function translateNow(key) {
  return get(t)(key);
}
