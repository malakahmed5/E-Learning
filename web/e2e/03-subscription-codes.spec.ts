// Browser tests for Module 3: subscription codes.
// The platform owner generates codes; a new student opens the account with a free-week code,
// then enters a 30-day code; the owner and the teacher see the result.
//
// This file runs after 02-teacher-management.spec.ts and uses the student added there
// (added by the teacher, never activated).

import { expect, test, type Browser, type Page } from "@playwright/test";
import { readState, saveState } from "./state";
import { nextCode } from "./totp";

const ADMIN_PHONE = "01000000009";
const ADMIN_PASSWORD = "Admin12345";
const TEACHER_PHONE = "01055550001";
const TEACHER_PASSWORD = "Teacher123";
const TEACHER_PLATFORM = "Mr Ahmed Physics";
const STUDENT_NAME = "Omar Khaled Hassan";
const STUDENT_PHONE = "01055550101";
const STUDENT_PASSWORD = "Student123";

test.describe.configure({ mode: "serial" });

async function loginWithCode(page: Page, phone: string, password: string, secret: string) {
  await page.goto("/login");
  await page.getByLabel("رقم الموبايل").fill(phone);
  await page.getByLabel("كلمة المرور").fill(password);
  await page.getByRole("button", { name: "دخول", exact: true }).click();
  await page.getByLabel("رمز التحقق").fill(await nextCode(secret));
  await page.getByRole("button", { name: "تأكيد" }).click();
}

async function openCodesPageAsOwner(browser: Browser): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await loginWithCode(page, ADMIN_PHONE, ADMIN_PASSWORD, readState().adminSecret!);
  await page.getByRole("link", { name: "أكواد الاشتراك" }).click();
  await expect(page.getByRole("heading", { name: "أكواد الاشتراك" })).toBeVisible();
  return page;
}

// Opens the generate dialog, fills it, and returns the codes shown afterwards.
async function generateCodes(page: Page, typeLabel: string, count: number, note: string): Promise<string[]> {
  await page.getByRole("button", { name: "توليد أكواد" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("نوع الكود").selectOption({ label: typeLabel });
  await dialog.getByLabel("عدد الأكواد").fill(String(count));
  await dialog.getByLabel("ملاحظة (اختياري)").fill(note);
  await dialog.getByRole("button", { name: "توليد أكواد" }).click();

  const result = page.getByRole("dialog", { name: `تم توليد ${count} كود` });
  await expect(result).toBeVisible();
  const codes = await result.locator(".code-list li").allInnerTexts();
  await result.getByRole("button", { name: "إغلاق" }).click();
  return codes.map((code) => code.trim());
}

test("the owner generates both kinds of codes, sees the numbers, and revokes one", async ({ browser }) => {
  const page = await openCodesPageAsOwner(browser);
  await expect(page.getByText("لا توجد أكواد بعد")).toBeVisible();

  // An impossible number of codes is caught in the form.
  await page.getByRole("button", { name: "توليد أكواد" }).click();
  let dialog = page.getByRole("dialog");
  await dialog.getByLabel("عدد الأكواد").fill("0");
  await dialog.getByRole("button", { name: "توليد أكواد" }).click();
  await expect(dialog.getByText("اكتب عددًا من 1 إلى 2000")).toBeVisible();
  await dialog.getByRole("button", { name: "إلغاء" }).click();

  const freeWeekCodes = await generateCodes(page, "أسبوع مجاني", 2, "دفعة التجربة");
  expect(freeWeekCodes).toHaveLength(2);
  expect(new Set(freeWeekCodes).size).toBe(2);
  for (const code of freeWeekCodes) {
    expect(code).toMatch(/^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/);
  }

  const paidCodes = await generateCodes(page, "اشتراك 30 يوم", 1, "دفعة الاشتراك");
  expect(paidCodes).toHaveLength(1);

  // The numbers per type.
  const freeWeekStats = page.getByTestId("stats-FreeWeek");
  await expect(freeWeekStats).toContainText("أسبوع مجاني");
  await expect(freeWeekStats.locator("dd").first()).toHaveText("2");
  await expect(page.getByTestId("stats-Subscription30").locator("dd").first()).toHaveText("1");

  // The table lists all three codes with the batch note.
  await expect(page.getByRole("row", { name: new RegExp(freeWeekCodes[0]) })).toContainText("متاح");
  await expect(page.getByRole("row", { name: new RegExp(paidCodes[0]) })).toContainText("دفعة الاشتراك");

  // Revoke the second free-week code. It asks for confirmation first.
  const rowToRevoke = page.getByRole("row", { name: new RegExp(freeWeekCodes[1]) });
  await rowToRevoke.getByRole("button", { name: "إلغاء الكود" }).click();
  dialog = page.getByRole("dialog");
  await expect(dialog).toContainText(freeWeekCodes[1]);
  await dialog.getByRole("button", { name: "إلغاء الكود" }).click();
  await expect(rowToRevoke).toContainText("ملغي");
  // A revoked code has no revoke button any more.
  await expect(rowToRevoke.getByRole("button", { name: "إلغاء الكود" })).toHaveCount(0);

  // The filter by status shows only the revoked code.
  await page.getByLabel("الحالة").selectOption({ label: "ملغي" });
  await expect(page.getByRole("row", { name: new RegExp(freeWeekCodes[1]) })).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(freeWeekCodes[0]) })).toHaveCount(0);

  saveState({ freeWeekCode: freeWeekCodes[0], revokedCode: freeWeekCodes[1], paidCode: paidCodes[0] });
  await page.context().close();
});

test("a new student opens the account with a free-week code, then adds 30 days with another code", async ({ browser }) => {
  const { freeWeekCode, revokedCode, paidCode } = readState();
  const context = await browser.newContext();
  const page = await context.newPage();

  // The login page points new students to the activation page.
  await page.goto("/login");
  await page.getByRole("link", { name: "طالب جديد؟ فعّل حسابك بالكود" }).click();
  await expect(page.getByRole("heading", { name: "تفعيل حساب الطالب" })).toBeVisible();

  // A code that does not exist, then a revoked code: both are refused with the reason.
  await page.getByLabel("رقم الموبايل").fill(STUDENT_PHONE);
  await page.getByLabel("الكود", { exact: true }).fill("AAAA-BBBB-CCCC");
  await page.getByLabel("كلمة المرور الجديدة").fill(STUDENT_PASSWORD);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByRole("alert")).toHaveText("الكود غير صحيح.");

  await page.getByLabel("الكود", { exact: true }).fill(revokedCode!);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByRole("alert")).toHaveText("هذا الكود ملغي.");

  // The real free-week code works and shows what the student got.
  await page.getByLabel("الكود", { exact: true }).fill(freeWeekCode!);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByRole("heading", { name: "تم تفعيل حسابك" })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("أسبوع مجاني");

  // The student logs in with the password they chose and lands at their teacher.
  await page.getByRole("link", { name: "العودة لتسجيل الدخول" }).click();
  await page.getByLabel("رقم الموبايل").fill(STUDENT_PHONE);
  await page.getByLabel("كلمة المرور").fill(STUDENT_PASSWORD);
  await page.getByRole("button", { name: "دخول", exact: true }).click();
  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM} بصفتك طالب`)).toBeVisible();

  const card = page.getByTestId("subscription-card");
  await expect(card).toContainText("أسبوع مجاني");
  await expect(card).toContainText("باقي 7 يوم");

  // Entering the 30-day code adds 30 days after the free week.
  await card.getByLabel("أدخل كود الاشتراك").fill(paidCode!);
  await card.getByRole("button", { name: "تفعيل الكود" }).click();
  await expect(card.getByText("تم تفعيل الكود")).toBeVisible();
  await expect(card).toContainText("اشتراك 30 يوم");
  await expect(card).toContainText("مشترك");
  await expect(card).toContainText("باقي 37 يوم");

  // The same code a second time is refused and the days do not change.
  await card.getByLabel("أدخل كود الاشتراك").fill(paidCode!);
  await card.getByRole("button", { name: "تفعيل الكود" }).click();
  await expect(card.getByRole("alert")).toHaveText("هذا الكود مستخدم من قبل.");
  await expect(card).toContainText("باقي 37 يوم");

  // A student has no staff menu and cannot open staff or owner pages by typing the address.
  await expect(page.getByRole("link", { name: "الطلاب" })).toHaveCount(0);
  await page.goto("/students");
  await expect(page).not.toHaveURL(/\/students$/);
  await page.goto("/admin/codes");
  await expect(page).not.toHaveURL(/\/admin/);

  await context.close();
});

test("the owner sees who redeemed each code and where", async ({ browser }) => {
  const { freeWeekCode, paidCode } = readState();
  const page = await openCodesPageAsOwner(browser);

  // Searching by the student's phone finds the two codes the student used.
  await page.getByRole("searchbox").fill(STUDENT_PHONE);
  const paidRow = page.getByRole("row", { name: new RegExp(paidCode!) });
  await expect(paidRow).toContainText("مستخدم");
  // The owner sees the name on the login account ("Omar Khaled"). The teacher's assistant later
  // changed the name only inside the teacher's own list.
  await expect(paidRow).toContainText("Omar Khaled");
  await expect(paidRow).toContainText(TEACHER_PLATFORM);
  await expect(page.getByRole("row", { name: new RegExp(freeWeekCode!) })).toContainText("مستخدم");

  // A used code cannot be revoked: the button is not there.
  await expect(paidRow.getByRole("button", { name: "إلغاء الكود" })).toHaveCount(0);

  // The numbers: free week = 2 total, 1 redeemed, 1 revoked. 30 days = 1 total, 1 redeemed.
  const freeWeekNumbers = await page.getByTestId("stats-FreeWeek").locator("dd").allInnerTexts();
  expect(freeWeekNumbers).toEqual(["2", "0", "1", "0", "1"]);
  const paidNumbers = await page.getByTestId("stats-Subscription30").locator("dd").allInnerTexts();
  expect(paidNumbers).toEqual(["1", "0", "1", "0", "0"]);

  // English: the same page, left-to-right, translated.
  await page.getByRole("button", { name: "English" }).click();
  await expect(page.getByRole("heading", { name: "Subscription codes" })).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(paidCode!) })).toContainText("Redeemed");
  await page.getByRole("button", { name: "العربية" }).click();

  await page.context().close();
});

test("the teacher sees the student's subscription in the students list", async ({ browser }) => {
  const context = await browser.newContext();
  const page = await context.newPage();
  await loginWithCode(page, TEACHER_PHONE, TEACHER_PASSWORD, readState().teacherSecret!);
  await page.getByRole("button", { name: new RegExp(TEACHER_PLATFORM) }).click();
  await page.getByRole("link", { name: "الطلاب" }).click();

  const studentRow = page.getByRole("row", { name: new RegExp(STUDENT_NAME) });
  await expect(studentRow).toContainText("مشترك");
  await expect(studentRow).toContainText("ينتهي في");

  // Nobody's days end within 3 days, so this filter shows the "nothing found" message.
  await page.getByLabel("الاشتراك").selectOption({ label: "ينتهي خلال 3 أيام" });
  await expect(page.getByText("لا يوجد طلاب بهذه المواصفات.")).toBeVisible();

  await context.close();
});
