// A student account works on one device only. The API needs an id for "this device".
// On the web we create a random id once and keep it in this browser.
// (The mobile app will send its own device id instead.)

const STORAGE_KEY = "elearning.deviceId";

export function getDeviceId(): string {
  let deviceId = window.localStorage.getItem(STORAGE_KEY);
  if (!deviceId) {
    deviceId = createRandomId();
    window.localStorage.setItem(STORAGE_KEY, deviceId);
  }
  return deviceId;
}

function createRandomId(): string {
  // 16 random bytes from the browser's secure random source, written as 32 hex characters.
  const bytes = new Uint8Array(16);
  window.crypto.getRandomValues(bytes);
  return Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");
}
