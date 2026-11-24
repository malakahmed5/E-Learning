// Language support: Arabic (default, right-to-left) and English (left-to-right).
// Usage in a component:  const { t } = useI18n();  t("login.title")

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { ar, type TranslationKey } from "./ar";
import { en } from "./en";
import { ApiError } from "../api/client";

export type Language = "ar" | "en";

const STORAGE_KEY = "elearning.language";
const dictionaries: Record<Language, Record<TranslationKey, string>> = { ar, en };

interface I18n {
  language: Language;
  setLanguage: (language: Language) => void;
  // Returns the text for a key. Values like {name} are replaced from "values".
  t: (key: TranslationKey, values?: Record<string, string>) => string;
  // Turns any thrown error into a message the user can read, in the current language.
  errorMessage: (error: unknown) => string;
  formatDate: (isoDate: string) => string;
}

const I18nContext = createContext<I18n | null>(null);

// Pure function, exported so it can be unit tested.
export function translate(language: Language, key: TranslationKey, values?: Record<string, string>): string {
  let text = dictionaries[language][key] ?? key;
  if (values) {
    for (const name of Object.keys(values)) {
      text = text.replace(`{${name}}`, values[name]);
    }
  }
  return text;
}

// Pure function, exported so it can be unit tested.
export function translateError(language: Language, error: unknown): string {
  const code = error instanceof ApiError ? error.code : "General.Unexpected";
  const key = `errors.${code}` as TranslationKey;

  // An error code we have no text for still gets a readable generic message.
  if (!(key in dictionaries[language])) {
    return dictionaries[language]["errors.General.Unexpected"];
  }
  return dictionaries[language][key];
}

function readSavedLanguage(): Language {
  const saved = window.localStorage.getItem(STORAGE_KEY);
  return saved === "en" ? "en" : "ar";
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(readSavedLanguage);

  // Keep the page direction and the saved choice in step with the language.
  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = language === "ar" ? "rtl" : "ltr";
    document.title = dictionaries[language]["app.name"];
    window.localStorage.setItem(STORAGE_KEY, language);
  }, [language]);

  const value = useMemo<I18n>(
    () => ({
      language,
      setLanguage,
      t: (key, values) => translate(language, key, values),
      errorMessage: (error) => translateError(language, error),
      formatDate: (isoDate) =>
        new Date(isoDate).toLocaleDateString(language === "ar" ? "ar-EG" : "en-GB", {
          year: "numeric",
          month: "short",
          day: "numeric",
        }),
    }),
    [language],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18n {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error("useI18n must be used inside I18nProvider");
  }
  return context;
}
