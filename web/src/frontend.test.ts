// Unit tests for the small pieces of logic in the website (no browser needed).

import { describe, expect, it } from "vitest";
import { ApiError, toApiError } from "./api/client";
import { translate, translateError } from "./i18n";
import { ar } from "./i18n/ar";
import { en } from "./i18n/en";
import { isEgyptianMobile, isSixDigitCode, isStrongPassword, isValidSlug } from "./validation";

// Builds the small part of a fetch Response that toApiError reads.
function fakeResponse(status: number, body: unknown): Response {
  return {
    status,
    json: async () => {
      if (body === undefined) {
        throw new Error("no body");
      }
      return body;
    },
  } as unknown as Response;
}

describe("translations", () => {
  it("Arabic and English have exactly the same keys", () => {
    expect(Object.keys(en).sort()).toEqual(Object.keys(ar).sort());
  });

  it("no text is left empty in either language", () => {
    for (const key of Object.keys(ar) as (keyof typeof ar)[]) {
      expect(ar[key].trim().length, `ar: ${key}`).toBeGreaterThan(0);
      expect(en[key].trim().length, `en: ${key}`).toBeGreaterThan(0);
    }
  });

  it("puts values into the text", () => {
    expect(translate("en", "home.welcome", { name: "Mona" })).toBe("Welcome, Mona");
    expect(translate("ar", "home.welcome", { name: "منى" })).toBe("أهلًا، منى");
  });

  it("translates a known API error code", () => {
    const error = new ApiError(401, "Auth.InvalidCredentials");
    expect(translateError("en", error)).toBe("The mobile number or password is not correct.");
    expect(translateError("ar", error)).toBe("رقم الموبايل أو كلمة المرور غير صحيحة.");
  });

  it("gives a generic message for an unknown error code or a non-API error", () => {
    expect(translateError("en", new ApiError(500, "Something.New"))).toBe("Something went wrong. Please try again.");
    expect(translateError("en", new Error("boom"))).toBe("Something went wrong. Please try again.");
  });
});

describe("reading API errors", () => {
  it("takes the error code from the title", async () => {
    const error = await toApiError(fakeResponse(409, { title: "Tenant.SlugTaken", status: 409 }));
    expect(error.status).toBe(409);
    expect(error.code).toBe("Tenant.SlugTaken");
  });

  it("takes a service validation code from the errors object", async () => {
    const body = { title: "One or more validation errors occurred.", errors: { "Auth.WeakPassword": ["..."] } };
    const error = await toApiError(fakeResponse(400, body));
    expect(error.code).toBe("Auth.WeakPassword");
  });

  it("reports plain field errors as a validation error", async () => {
    const body = { title: "One or more validation errors occurred.", errors: { PhoneNumber: ["not valid"] } };
    const error = await toApiError(fakeResponse(400, body));
    expect(error.code).toBe("Validation.Invalid");
    expect(error.fieldErrors.PhoneNumber).toEqual(["not valid"]);
  });

  it("handles an empty body", async () => {
    expect((await toApiError(fakeResponse(401, undefined))).code).toBe("Auth.Unauthorized");
    expect((await toApiError(fakeResponse(403, undefined))).code).toBe("Auth.Forbidden");
    expect((await toApiError(fakeResponse(500, undefined))).code).toBe("General.Unexpected");
  });

  it("recognises rate limiting", async () => {
    expect((await toApiError(fakeResponse(429, undefined))).code).toBe("RateLimit.TooManyRequests");
  });
});

describe("input checks", () => {
  it("accepts Egyptian mobile numbers in the usual forms", () => {
    expect(isEgyptianMobile("01012345678")).toBe(true);
    expect(isEgyptianMobile("+201112345678")).toBe(true);
    expect(isEgyptianMobile("010 1234 5678")).toBe(true);
  });

  it("rejects other phone text", () => {
    expect(isEgyptianMobile("12345")).toBe(false);
    expect(isEgyptianMobile("01312345678")).toBe(false);
    expect(isEgyptianMobile("01012345678; drop")).toBe(false);
  });

  it("checks six-digit codes", () => {
    expect(isSixDigitCode("123456")).toBe(true);
    expect(isSixDigitCode("12345")).toBe(false);
    expect(isSixDigitCode("12345a")).toBe(false);
  });

  it("checks link names", () => {
    expect(isValidSlug("mr-ahmed")).toBe(true);
    expect(isValidSlug("ab")).toBe(false);
    expect(isValidSlug("Mr-Ahmed")).toBe(false);
    expect(isValidSlug("-ahmed")).toBe(false);
    expect(isValidSlug("mr ahmed")).toBe(false);
  });

  it("checks password strength", () => {
    expect(isStrongPassword("Teacher123")).toBe(true);
    expect(isStrongPassword("short1")).toBe(false);
    expect(isStrongPassword("onlyletters")).toBe(false);
    expect(isStrongPassword("1234567890")).toBe(false);
  });
});
