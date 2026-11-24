// Browser tests for Module 1: login, two-step verification, languages, and the platform admin screens.
// The tests run in order and share data: the admin registers a teacher, then that teacher logs in.

import { expect, test, type Page } from "@playwright/test";
import { readState, saveState } from "./state";
import { nextCode } from "./totp";

const ADMIN_PHONE = "01000000009";
const ADMIN_PASSWORD = "Admin12345";
const TEACHER_PHONE = "01055550001";
const TEACHER_PASSWORD = "Teacher123";
const TEACHER_SLUG = "mr-ahmed";
const TEACHER_PLATFORM = "Mr Ahmed Physics";

// A value created by one test and needed by a later one in the same session.
// (Authenticator secrets are kept in the shared state file instead, see state.ts.)
let teacherInviteCode = "";

test.describe.configure({ mode: "serial" });

async function switchToEnglish(page: Page) {
  await page.getByRole("button", { name: "English" }).click();
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
}

async function fillLogin(page: Page, phone: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("رقم الموبايل").fill(phone);
  await page.getByLabel("كلمة المرور").fill(password);
  await page.getByRole("button", { name: "دخول", exact: true }).click();
}

// Reads the secret from the setup screen and types the matching code.
async function completeTwoFactorSetup(page: Page): Promise<string> {
  await expect(page.getByRole("heading", { name: "تفعيل التحقق بخطوتين" })).toBeVisible();
  await expect(page.locator(".qr-box svg")).toBeVisible();

  const secret = (await page.locator(".secret-line code").innerText()).trim();
  expect(secret).toMatch(/^[A-Z2-7]{32}$/);

  await page.getByLabel("رمز التحقق").fill(await nextCode(secret));
  await page.getByRole("button", { name: "تأكيد" }).click();
  return secret;
}

async function loginWithCode(page: Page, phone: string, password: string, secret: string) {
  await fillLogin(page, phone, password);
  await expect(page.getByRole("heading", { name: "رمز التحقق" })).toBeVisible();
  await page.getByLabel("رمز التحقق").fill(await nextCode(secret));
  await page.getByRole("button", { name: "تأكيد" }).click();
}

test("the site opens in Arabic, right-to-left, and can switch to English and back", async ({ page }) => {
  await page.goto("/");

  // Not logged in: we land on the login page.
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.locator("html")).toHaveAttribute("lang", "ar");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expect(page.getByRole("heading", { name: "تسجيل الدخول" })).toBeVisible();

  await switchToEnglish(page);
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(page.getByRole("heading", { name: "Log in" })).toBeVisible();
  await expect(page.getByLabel("Mobile number")).toBeVisible();

  // The choice is remembered after a reload.
  await page.reload();
  await expect(page.getByRole("heading", { name: "Log in" })).toBeVisible();

  await page.getByRole("button", { name: "العربية" }).click();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
});

test("an invalid phone number is caught before anything is sent", async ({ page }) => {
  await fillLogin(page, "12345", "whatever1");

  await expect(page.getByText("اكتب رقم موبايل مصري صحيح")).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test("a wrong password shows a clear message in the current language", async ({ page }) => {
  await fillLogin(page, ADMIN_PHONE, "WrongPassword1");
  await expect(page.getByRole("alert")).toHaveText("رقم الموبايل أو كلمة المرور غير صحيحة.");

  await switchToEnglish(page);
  await page.getByLabel("Password").fill("WrongPassword2");
  await page.getByRole("button", { name: "Log in", exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText("The mobile number or password is not correct.");
});

test("protected pages send a visitor to the login page", async ({ page }) => {
  await page.goto("/admin/teachers");
  await expect(page).toHaveURL(/\/login$/);
});

test("the admin sets up two-step verification on first login and registers a teacher", async ({ page }) => {
  await fillLogin(page, ADMIN_PHONE, ADMIN_PASSWORD);
  saveState({ adminSecret: await completeTwoFactorSetup(page) });

  // The admin lands on the teachers page, which is empty at first.
  await expect(page).toHaveURL(/\/admin\/teachers$/);
  await expect(page.getByRole("heading", { name: "المدرسون" })).toBeVisible();
  await expect(page.getByText("لا يوجد مدرسون بعد")).toBeVisible();

  // Register a teacher.
  await page.getByRole("button", { name: "تسجيل مدرس" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم المنصة كما يراه الطلاب").fill(TEACHER_PLATFORM);
  await dialog.getByLabel("اسم الرابط").fill(TEACHER_SLUG);
  await dialog.getByLabel("اسم المدرس").fill("Ahmed Hassan");
  await dialog.getByLabel("رقم موبايل المدرس").fill(TEACHER_PHONE);
  await dialog.getByRole("button", { name: "حفظ" }).click();

  // The invite code is shown once.
  const inviteDialog = page.getByRole("dialog", { name: "كود الدعوة" });
  await expect(inviteDialog).toBeVisible({ timeout: 30_000 });
  teacherInviteCode = (await inviteDialog.locator(".invite-code").innerText()).trim();
  expect(teacherInviteCode).toMatch(/^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/);
  await inviteDialog.getByRole("button", { name: "إغلاق" }).click();

  // The teacher is now in the table and active.
  const row = page.getByRole("row", { name: new RegExp(TEACHER_PLATFORM) });
  await expect(row).toBeVisible();
  await expect(row.getByText("مفعّل")).toBeVisible();
  await expect(row.getByText(TEACHER_SLUG)).toBeVisible();
});

test("registering a second teacher with the same link name shows the reason", async ({ page }) => {
  await loginWithCode(page, ADMIN_PHONE, ADMIN_PASSWORD, readState().adminSecret!);
  await expect(page).toHaveURL(/\/admin\/teachers$/);

  await page.getByRole("button", { name: "تسجيل مدرس" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم المنصة كما يراه الطلاب").fill("Another Teacher");
  await dialog.getByLabel("اسم الرابط").fill(TEACHER_SLUG);
  await dialog.getByLabel("اسم المدرس").fill("Someone Else");
  await dialog.getByLabel("رقم موبايل المدرس").fill("01055550002");
  await dialog.getByRole("button", { name: "حفظ" }).click();

  await expect(dialog.getByRole("alert")).toHaveText("اسم الرابط مستخدم من مدرس آخر.");

  // A link name with capital letters or spaces is refused in the browser already.
  await dialog.getByLabel("اسم الرابط").fill("x");
  await dialog.getByRole("button", { name: "حفظ" }).click();
  await expect(dialog.getByText("من 3 إلى 50")).toBeVisible();
  await dialog.getByRole("button", { name: "إلغاء" }).click();

  // Academic terms: add one, then an overlapping one is refused with a clear message.
  await page.getByRole("link", { name: "الفصول الدراسية" }).click();
  await expect(page.getByText("لم تُضف أي فصول دراسية بعد.")).toBeVisible();

  await page.getByRole("button", { name: "إضافة ترم" }).click();
  let termDialog = page.getByRole("dialog");
  await termDialog.getByLabel("الاسم").fill("الترم الأول");
  await termDialog.getByLabel("البداية").fill("2027-09-18");
  await termDialog.getByLabel("النهاية").fill("2028-01-20");
  await termDialog.getByRole("button", { name: "حفظ" }).click();
  await expect(page.getByRole("cell", { name: "الترم الأول" })).toBeVisible();

  await page.getByRole("button", { name: "إضافة ترم" }).click();
  termDialog = page.getByRole("dialog");
  await termDialog.getByLabel("الاسم").fill("ترم متداخل");
  await termDialog.getByLabel("البداية").fill("2028-01-01");
  await termDialog.getByLabel("النهاية").fill("2028-05-30");
  await termDialog.getByRole("button", { name: "حفظ" }).click();
  await expect(termDialog.getByRole("alert")).toHaveText("هذه التواريخ تتداخل مع ترم آخر.");
});

test("the teacher activates the account with the invite code and reaches their own platform", async ({ page }) => {
  // A wrong invite code is refused.
  await page.goto("/accept-invite");
  await page.getByLabel("رقم الموبايل").fill(TEACHER_PHONE);
  await page.getByLabel("كود الدعوة").fill("AAAA-BBBB-CCCC");
  await page.getByLabel("كلمة المرور الجديدة").fill(TEACHER_PASSWORD);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByRole("alert")).toContainText("كود الدعوة غير صحيح");

  // A weak password is refused in the browser.
  await page.getByLabel("كود الدعوة").fill(teacherInviteCode);
  await page.getByLabel("كلمة المرور الجديدة").fill("weak");
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.locator(".field-error")).toContainText("8 أحرف على الأقل");

  // The right code and a good password work.
  await page.getByLabel("كلمة المرور الجديدة").fill(TEACHER_PASSWORD);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByText("تم تفعيل حسابك")).toBeVisible();

  // First login: the teacher must set up two-step verification.
  await fillLogin(page, TEACHER_PHONE, TEACHER_PASSWORD);
  saveState({ teacherSecret: await completeTwoFactorSetup(page) });

  // The teacher lands inside their own platform, with their own details read from their own database.
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM} بصفتك مدرس`)).toBeVisible();
  await expect(page.locator(".details")).toContainText(TEACHER_PHONE);
  await expect(page.locator(".details")).toContainText("مدرس");

  // A teacher cannot open the admin pages.
  await page.goto("/admin/teachers");
  await expect(page).not.toHaveURL(/\/admin/);

  // Reloading keeps the teacher logged in.
  await page.reload();
  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM}`)).toBeVisible();

  // Logging out returns to the login page, and going back does not show the platform.
  await page.getByRole("button", { name: "تسجيل الخروج" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});

test("when the admin turns a teacher off, the teacher loses access until it is turned on again", async ({ browser }) => {
  // Two separate browsers: one for the teacher, one for the admin.
  const teacherContext = await browser.newContext();
  const teacherPage = await teacherContext.newPage();
  await loginWithCode(teacherPage, TEACHER_PHONE, TEACHER_PASSWORD, readState().teacherSecret!);
  await expect(teacherPage.getByText(`أنت داخل منصة ${TEACHER_PLATFORM}`)).toBeVisible();

  const adminContext = await browser.newContext();
  const adminPage = await adminContext.newPage();
  await loginWithCode(adminPage, ADMIN_PHONE, ADMIN_PASSWORD, readState().adminSecret!);
  const row = adminPage.getByRole("row", { name: new RegExp(TEACHER_PLATFORM) });

  // Turning off asks for confirmation first.
  await row.getByRole("button", { name: "إيقاف" }).click();
  const confirm = adminPage.getByRole("dialog");
  await expect(confirm).toContainText("إيقاف منصة المدرس؟");
  await confirm.getByRole("button", { name: "إيقاف" }).click();
  await expect(row.getByText("متوقف")).toBeVisible();

  // The teacher's open page no longer shows the platform after a reload.
  await teacherPage.reload();
  await expect(teacherPage).toHaveURL(/\/(login|choose-teacher)$/);
  await expect(teacherPage.getByText(`أنت داخل منصة ${TEACHER_PLATFORM}`)).toHaveCount(0);

  // Turn it on again: the teacher can log in again.
  await row.getByRole("button", { name: "تفعيل" }).click();
  await expect(row.getByText("مفعّل")).toBeVisible();

  await teacherContext.close();
  await adminContext.close();
});

test("a person with two platforms logs in once and chooses between them", async ({ browser }) => {
  const SECOND_PLATFORM = "Mr Ahmed Chemistry";

  // The admin registers a second platform for the same teacher (same phone number).
  const adminContext = await browser.newContext();
  const adminPage = await adminContext.newPage();
  await loginWithCode(adminPage, ADMIN_PHONE, ADMIN_PASSWORD, readState().adminSecret!);
  await adminPage.getByRole("button", { name: "تسجيل مدرس" }).first().click();
  const dialog = adminPage.getByRole("dialog");
  await dialog.getByLabel("اسم المنصة كما يراه الطلاب").fill(SECOND_PLATFORM);
  await dialog.getByLabel("اسم الرابط").fill("mr-ahmed-chemistry");
  await dialog.getByLabel("اسم المدرس").fill("Ahmed Hassan");
  await dialog.getByLabel("رقم موبايل المدرس").fill(TEACHER_PHONE);
  await dialog.getByRole("button", { name: "حفظ" }).click();

  // The teacher already has a password, so no invite code is needed.
  await expect(adminPage.getByText("لا يحتاج كود دعوة")).toBeVisible({ timeout: 30_000 });
  await adminContext.close();

  // The teacher logs in once and is asked which platform to open.
  const teacherContext = await browser.newContext();
  const page = await teacherContext.newPage();
  await loginWithCode(page, TEACHER_PHONE, TEACHER_PASSWORD, readState().teacherSecret!);
  await expect(page).toHaveURL(/\/choose-teacher$/);
  await expect(page.getByRole("heading", { name: "اختر المدرس" })).toBeVisible();
  await expect(page.getByRole("button", { name: new RegExp(TEACHER_PLATFORM) })).toBeVisible();

  await page.getByRole("button", { name: new RegExp(SECOND_PLATFORM) }).click();
  await expect(page.getByText(`أنت داخل منصة ${SECOND_PLATFORM} بصفتك مدرس`)).toBeVisible();

  // Switching to the other platform needs no password.
  await page.getByRole("button", { name: "تبديل المدرس" }).click();
  await page.getByRole("button", { name: new RegExp(TEACHER_PLATFORM) }).click();
  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM} بصفتك مدرس`)).toBeVisible();

  await teacherContext.close();
});

test("the phone layout has no sideways scrolling on the login page", async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 360, height: 740 } });
  const page = await context.newPage();
  await page.goto("/login");

  await expect(page.getByRole("heading", { name: "تسجيل الدخول" })).toBeVisible();
  const overflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(overflows).toBe(false);

  await context.close();
});
