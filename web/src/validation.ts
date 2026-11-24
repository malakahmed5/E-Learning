// Input checks done in the browser so the user gets a fast answer.
// They are only for convenience: the API checks everything again and is the real judge.

export function isEgyptianMobile(value: string): boolean {
  const digits = value.replace(/[\s-]/g, "").replace(/^(\+20|0020)/, "0");
  return /^01[0125]\d{8}$/.test(digits);
}

export function isSixDigitCode(value: string): boolean {
  return /^\d{6}$/.test(value);
}

export function isValidSlug(value: string): boolean {
  return /^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$/.test(value);
}

export function isStrongPassword(value: string): boolean {
  return value.length >= 8 && /[A-Za-z؀-ۿ]/.test(value) && /\d/.test(value);
}
