// Browser tests for Module 2: the teacher's centers, groups, students and assistants,
// what an assistant can and cannot see, and the assistant activity log.
//
// This file runs after 01-accounts-and-login.spec.ts and uses the teacher registered there.

import { expect, test, type Browser, type Page } from "@playwright/test";
import { readState } from "./state";
import { nextCode } from "./totp";

const TEACHER_PHONE = "01055550001";
const TEACHER_PASSWORD = "Teacher123";
const TEACHER_PLATFORM = "Mr Ahmed Physics";
const ASSISTANT_PHONE = "01055550077";
const ASSISTANT_PASSWORD = "Assistant123";
const ASSISTANT_NAME = "Sara Assistant";
const STUDENT_NAME = "Omar Khaled";
const STUDENT_PHONE = "01055550101";
const PARENT_PHONE = "01155501234";

let assistantInviteCode = "";
let assistantSecret = "";

// Each person logs in once and the same browser window is used by all the tests below.
// (Every staff login needs a fresh authenticator code, and a new code only appears every 30 seconds,
// so logging in again for each test would make the run much slower.)
let teacherPage: Page | null = null;
let assistantPage: Page | null = null;

test.afterAll(async () => {
  await teacherPage?.context().close();
  await assistantPage?.context().close();
});

test.describe.configure({ mode: "serial" });

// The teacher has two platforms (from the earlier file), so login ends on the "choose teacher" screen.
async function loginAsTeacher(browser: Browser): Promise<Page> {
  if (teacherPage) {
    await teacherPage.goto("/");
    return teacherPage;
  }
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto("/login");
  await page.getByLabel("رقم الموبايل").fill(TEACHER_PHONE);
  await page.getByLabel("كلمة المرور").fill(TEACHER_PASSWORD);
  await page.getByRole("button", { name: "دخول", exact: true }).click();
  await page.getByLabel("رمز التحقق").fill(await nextCode(readState().teacherSecret!));
  await page.getByRole("button", { name: "تأكيد" }).click();
  await page.getByRole("button", { name: new RegExp(TEACHER_PLATFORM) }).click();
  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM} بصفتك مدرس`)).toBeVisible();
  teacherPage = page;
  return page;
}

test("the teacher sets up a center, a group with two weekly lessons, and a student", async ({ browser }) => {
  const page = await loginAsTeacher(browser);

  // A student cannot be added before a group exists, and the page says why.
  await page.getByRole("link", { name: "الطلاب" }).click();
  await expect(page.getByText("أضف مجموعة أولًا")).toBeVisible();
  await expect(page.getByRole("button", { name: "طالب جديد" })).toBeDisabled();

  // Center.
  await page.getByRole("link", { name: "السناتر" }).click();
  await expect(page.getByText("لم تُضف أي سنتر بعد")).toBeVisible();
  await page.getByRole("button", { name: "سنتر جديد" }).click();
  let dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم السنتر").fill("سنتر النور");
  await dialog.getByLabel("العنوان").fill("12 شارع التحرير");
  await dialog.getByRole("button", { name: "حفظ" }).click();
  await expect(page.getByRole("cell", { name: "سنتر النور" })).toBeVisible();

  // Group with two lessons: Saturday and Tuesday.
  await page.getByRole("link", { name: "المجموعات" }).click();
  await page.getByRole("button", { name: "مجموعة جديدة" }).first().click();
  dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم المجموعة").fill("مجموعة السبت والثلاثاء");
  await dialog.getByLabel("السنة الدراسية").selectOption({ label: "الثالث الإعدادي" });
  await dialog.getByLabel("المكان").selectOption({ label: "سنتر النور" });
  await dialog.getByRole("button", { name: "إضافة موعد" }).click();
  await expect(dialog.getByLabel("اليوم")).toHaveCount(2);
  await dialog.getByRole("button", { name: "حفظ" }).click();

  const groupRow = page.getByRole("row", { name: /مجموعة السبت والثلاثاء/ });
  await expect(groupRow).toBeVisible();
  await expect(groupRow).toContainText("الثالث الإعدادي");
  await expect(groupRow).toContainText("سنتر النور");
  await expect(groupRow).toContainText("السبت");
  await expect(groupRow).toContainText("الثلاثاء");

  // A lesson that ends before it starts is refused, with the reason shown in the dialog.
  await page.getByRole("button", { name: "مجموعة جديدة" }).first().click();
  dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم المجموعة").fill("مجموعة خطأ");
  await dialog.getByLabel("من").fill("18:00");
  await dialog.getByLabel("إلى").fill("16:00");
  await dialog.getByRole("button", { name: "حفظ" }).click();
  await expect(dialog.getByRole("alert")).toHaveText("وقت نهاية الحصة يجب أن يكون بعد وقت بدايتها.");
  await dialog.getByRole("button", { name: "إلغاء" }).click();

  // The center now has a group, so it cannot be deleted.
  await page.getByRole("link", { name: "السناتر" }).click();
  await page.getByRole("row", { name: /سنتر النور/ }).getByRole("button", { name: "حذف" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "حذف" }).click();
  await expect(page.getByRole("alert")).toHaveText("هذا السنتر به مجموعات. انقلها أو احذفها أولًا.");

  // Student.
  await page.getByRole("link", { name: "الطلاب" }).click();
  await page.getByRole("button", { name: "طالب جديد" }).click();
  dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم الطالب").fill(STUDENT_NAME);
  await dialog.getByLabel("موبايل الطالب").fill(STUDENT_PHONE);
  await dialog.getByLabel("اسم ولي الأمر").fill("Khaled Hassan");
  await dialog.getByLabel("واتساب ولي الأمر").fill(PARENT_PHONE);
  await dialog.getByLabel("ولي الأمر موافق على استقبال رسائل واتساب").check();
  await dialog.getByRole("button", { name: "حفظ" }).click();

  const studentRow = page.getByRole("row", { name: new RegExp(STUDENT_NAME) });
  await expect(studentRow).toBeVisible();
  await expect(studentRow).toContainText("مجموعة السبت والثلاثاء");
  await expect(studentRow).toContainText("لم يفعّل الحساب");
  // The teacher sees the parent's full number.
  await expect(studentRow).toContainText(PARENT_PHONE);

  // The same phone cannot be added twice.
  await page.getByRole("button", { name: "طالب جديد" }).click();
  dialog = page.getByRole("dialog");
  await dialog.getByLabel("اسم الطالب").fill("Someone Else");
  await dialog.getByLabel("موبايل الطالب").fill(STUDENT_PHONE);
  await dialog.getByLabel("اسم ولي الأمر").fill("Another Parent");
  await dialog.getByLabel("واتساب ولي الأمر").fill(PARENT_PHONE);
  await dialog.getByRole("button", { name: "حفظ" }).click();
  await expect(dialog.getByRole("alert")).toHaveText("رقم الموبايل مسجل عندك بالفعل.");
  await dialog.getByRole("button", { name: "إلغاء" }).click();

});

test("the teacher finds, suspends and restores a student, and sees why a device cannot be unlocked", async ({ browser }) => {
  const page = await loginAsTeacher(browser);
  await page.getByRole("link", { name: "الطلاب" }).click();
  const studentRow = page.getByRole("row", { name: new RegExp(STUDENT_NAME) });
  await expect(studentRow).toBeVisible();

  // Search that matches nothing shows a clear message; clearing it brings the student back.
  await page.getByRole("searchbox").fill("zzzz-no-such-student");
  await expect(page.getByText("لا يوجد طلاب بهذه المواصفات.")).toBeVisible();
  await page.getByRole("searchbox").fill("omar");
  await expect(studentRow).toBeVisible();

  // Suspending asks for confirmation first.
  await studentRow.getByRole("button", { name: "إيقاف" }).click();
  const confirm = page.getByRole("dialog");
  await expect(confirm).toContainText("إيقاف الطالب؟");
  await confirm.getByRole("button", { name: "إيقاف" }).click();
  await expect(studentRow.getByText("موقوف")).toBeVisible();

  await studentRow.getByRole("button", { name: "إعادة التفعيل" }).click();
  await expect(studentRow.getByText("لم يفعّل الحساب")).toBeVisible();

  // No school term covers today's date in this test database, so the unlock is refused with the reason.
  await studentRow.getByRole("button", { name: "فتح على جهاز جديد" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "فتح على جهاز جديد" }).click();
  await expect(page.getByRole("alert")).toHaveText("لا يوجد ترم دراسي يشمل تاريخ اليوم. تواصل مع إدارة المنصة.");

  // English: the same page in left-to-right with translated texts.
  await page.getByRole("button", { name: "English" }).click();
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expect(page.getByRole("heading", { name: "Students" })).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(STUDENT_NAME) })).toContainText("3rd preparatory");
  await page.getByRole("button", { name: "العربية" }).click();

});

test("the teacher adds an assistant, who activates the account and logs in", async ({ browser }) => {
  const teacher = await loginAsTeacher(browser);
  await teacher.getByRole("link", { name: "المساعدون" }).click();
  await expect(teacher.getByText("لا يوجد مساعدون بعد.")).toBeVisible();

  await teacher.getByRole("button", { name: "مساعد جديد" }).click();
  const dialog = teacher.getByRole("dialog");
  await dialog.getByLabel("اسم المساعد").fill(ASSISTANT_NAME);
  await dialog.getByLabel("رقم الموبايل").fill(ASSISTANT_PHONE);
  await dialog.getByRole("button", { name: "حفظ" }).click();

  const inviteDialog = teacher.getByRole("dialog", { name: "كود الدعوة" });
  await expect(inviteDialog).toBeVisible();
  assistantInviteCode = (await inviteDialog.locator(".invite-code").innerText()).trim();
  await inviteDialog.getByRole("button", { name: "إغلاق" }).click();
  await expect(teacher.getByRole("row", { name: new RegExp(ASSISTANT_NAME) })).toContainText("مفعّل");

  // The assistant activates the account and sets up two-step verification.
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto("/accept-invite");
  await page.getByLabel("رقم الموبايل").fill(ASSISTANT_PHONE);
  await page.getByLabel("كود الدعوة").fill(assistantInviteCode);
  await page.getByLabel("كلمة المرور الجديدة").fill(ASSISTANT_PASSWORD);
  await page.getByRole("button", { name: "تفعيل الحساب" }).click();
  await expect(page.getByText("تم تفعيل حسابك")).toBeVisible();

  await page.goto("/login");
  await page.getByLabel("رقم الموبايل").fill(ASSISTANT_PHONE);
  await page.getByLabel("كلمة المرور").fill(ASSISTANT_PASSWORD);
  await page.getByRole("button", { name: "دخول", exact: true }).click();
  await expect(page.getByRole("heading", { name: "تفعيل التحقق بخطوتين" })).toBeVisible();
  assistantSecret = (await page.locator(".secret-line code").innerText()).trim();
  await page.getByLabel("رمز التحقق").fill(await nextCode(assistantSecret));
  await page.getByRole("button", { name: "تأكيد" }).click();

  await expect(page.getByText(`أنت داخل منصة ${TEACHER_PLATFORM} بصفتك مساعد`)).toBeVisible();
  assistantPage = page;
});

test("an assistant does daily work, never sees the parent's full number, and cannot open the teacher-only pages", async () => {
  const page = assistantPage!;

  // The menu has the daily pages but not the two teacher-only pages.
  await expect(page.getByRole("link", { name: "الطلاب" })).toBeVisible();
  await expect(page.getByRole("link", { name: "المساعدون" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "سجل المساعدين" })).toHaveCount(0);

  // Typing the address by hand does not help either.
  await page.goto("/assistants");
  await expect(page).not.toHaveURL(/\/assistants$/);
  await page.goto("/activity");
  await expect(page).not.toHaveURL(/\/activity$/);

  // The parent's number is shown with hidden digits.
  await page.getByRole("link", { name: "الطلاب" }).click();
  const studentRow = page.getByRole("row", { name: new RegExp(STUDENT_NAME) });
  await expect(studentRow).toContainText("011****1234");
  await expect(page.getByText(PARENT_PHONE)).toHaveCount(0);

  // The assistant renames the student. The parent number field is left empty and must be kept.
  await studentRow.getByRole("button", { name: "تعديل" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("موبايل الطالب")).toBeDisabled();
  await dialog.getByLabel("اسم الطالب").fill("Omar Khaled Hassan");
  await dialog.getByRole("button", { name: "حفظ" }).click();
  await expect(page.getByRole("row", { name: /Omar Khaled Hassan/ })).toContainText("011****1234");

});

test("the teacher reviews what the assistant did, then suspends the assistant", async ({ browser }) => {
  const teacher = await loginAsTeacher(browser);

  // The assistant's rename is in the activity log, with the assistant's name.
  await teacher.getByRole("link", { name: "سجل المساعدين" }).click();
  const logRow = teacher.getByRole("row", { name: new RegExp(ASSISTANT_NAME) }).first();
  await expect(logRow).toBeVisible();
  await expect(logRow).toContainText("تعديل");
  await expect(logRow).toContainText("Omar Khaled Hassan");

  // The parent's number was kept after the assistant's edit.
  await teacher.getByRole("link", { name: "الطلاب" }).click();
  await expect(teacher.getByRole("row", { name: /Omar Khaled Hassan/ })).toContainText(PARENT_PHONE);

  // An assistant who is logged in right now...
  const assistant = assistantPage!;
  await assistant.getByRole("link", { name: "الطلاب" }).click();
  await expect(assistant.getByRole("row", { name: /Omar Khaled Hassan/ })).toBeVisible();

  // ...is suspended by the teacher...
  await teacher.getByRole("link", { name: "المساعدون" }).click();
  const assistantRow = teacher.getByRole("row", { name: new RegExp(ASSISTANT_NAME) });
  await assistantRow.getByRole("button", { name: "إيقاف" }).click();
  await teacher.getByRole("dialog").getByRole("button", { name: "إيقاف" }).click();
  await expect(assistantRow.getByText("موقوف")).toBeVisible();

  // ...and loses access on the next thing they do.
  await assistant.getByRole("link", { name: "المجموعات" }).click();
  await expect(assistant.getByRole("alert")).toHaveText("لا يمكنك دخول منصة هذا المدرس.");

});

test("the students page fits a phone screen without sideways page scrolling", async ({ browser }) => {
  const page = await loginAsTeacher(browser);
  await page.setViewportSize({ width: 360, height: 740 });
  await page.getByRole("link", { name: "الطلاب" }).click();
  await expect(page.getByRole("row", { name: /Omar Khaled Hassan/ })).toBeVisible();

  // The table scrolls inside its own box; the page itself must not scroll sideways.
  const pageOverflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(pageOverflows).toBe(false);
});
