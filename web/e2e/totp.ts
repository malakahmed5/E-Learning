// Plays the role of the authenticator app in browser tests:
// turns the secret shown on the setup screen into the current 6-digit code.
// Same standard as the server (RFC 6238: SHA-1, 30-second steps, 6 digits).

import { createHmac } from "crypto";
import { readState, saveState } from "./state";

const BASE32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
const STEP_SECONDS = 30;

// The server refuses a code that was already used, so we remember the last step used per secret.
// It is kept in the shared state file because several test files log in with the same accounts.

function fromBase32(text: string): Buffer {
  const bytes: number[] = [];
  let buffer = 0;
  let bitsInBuffer = 0;

  for (const character of text.toUpperCase()) {
    buffer = (buffer << 5) | BASE32_ALPHABET.indexOf(character);
    bitsInBuffer += 5;
    if (bitsInBuffer >= 8) {
      bytes.push((buffer >> (bitsInBuffer - 8)) & 0xff);
      bitsInBuffer -= 8;
    }
  }
  return Buffer.from(bytes);
}

function codeForStep(secret: string, step: number): string {
  const stepBytes = Buffer.alloc(8);
  stepBytes.writeBigUInt64BE(BigInt(step));

  const hash = createHmac("sha1", fromBase32(secret)).update(stepBytes).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const number =
    ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];

  return String(number % 1_000_000).padStart(6, "0");
}

// Returns a code that was not used before for this secret.
// The server accepts the current step and one step ahead; if both were used, we wait for the next step.
export async function nextCode(secret: string): Promise<string> {
  for (;;) {
    const currentStep = Math.floor(Date.now() / 1000 / STEP_SECONDS);
    const lastUsedStep = readState().lastUsedStep ?? {};
    const previous = lastUsedStep[secret] ?? 0;
    const step = Math.max(currentStep, previous + 1);

    if (step <= currentStep + 1) {
      saveState({ lastUsedStep: { ...lastUsedStep, [secret]: step } });
      return codeForStep(secret, step);
    }
    await new Promise((resolve) => setTimeout(resolve, 2000));
  }
}
