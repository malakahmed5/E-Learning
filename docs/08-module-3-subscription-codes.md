# Module 3: Subscription codes

Short technical notes for developers. Status and test evidence are in `PROJECT_PROGRESS.md`.

## What this module does

Students pay the platform fee by entering a code instead of paying online. The platform owner generates codes and hands them out. There are two kinds:

| Kind | Gives | Notes |
|---|---|---|
| Free week | 7 days | A new student also uses it to open the account for the first time. Once per student at each teacher. |
| 30-day subscription | 30 days | Can also open a new account. |

Codes give access to the **platform** only. They do not pay the teacher's own lesson fee (that is tracked in a later module).

## Decisions

- "Activation code" and "free week code" are the same code (owner's decision, 2026-11-23).
- Days start when the code is entered. If the student still has days, the new days are added after them, so renewing early loses nothing.
- The optional "redeem before" date only limits when a code can be entered. It does not shorten the days.
- A code is not tied to a teacher. It is tied to one when it is used.
- Codes are stored as plain text in the hub, because the owner must be able to list, copy and export them. Anyone who can read the hub database can therefore read unused codes; protect that database accordingly.

## Statuses

Stored: Available, Used, Voided. Shown to the owner: Available, Redeemed, Expired, Revoked. "Expired" is worked out: still Available, but the batch's redeem-before date has passed.

## How a code is redeemed (two databases)

The code is in the hub; the student's days are in the teacher's database. They cannot share one transaction, so `CodeRedeemer` does this:

1. Check the code (exists, not used, not revoked, not past its redeem date, free week not already used at this teacher).
2. Claim it with one SQL command: `UPDATE codes SET status = Used ... WHERE id = @id AND status = Available`. Only one request can change the row, so two people sending the same code at the same moment cannot both win.
3. Add a `subscription_periods` row and move `students.access_ends_at` in the teacher's database.
4. If step 3 fails, give the code back (set it to Available again).
5. Write a hub audit row (never the code text).

## Services

| Service | What it does |
|---|---|
| `CodeAdminService` | Owner: generate a batch, list with filters and search, numbers per type, revoke, export a batch, read the platform audit log. |
| `CodeRedeemer` | The steps above. Used by the two services below. |
| `SubscriptionService` | Logged-in student: enter a code, see the status. Blocks code entry for 15 minutes after 5 wrong codes. |
| `ActivationService` | New student without a password: phone + code + new password. Refuses accounts that already have a password, so a code can never replace someone's password. Wrong codes count towards the account lockout. |
| `SubscriptionRules` | Pure calculation of the status (not activated, trial, active, expired, days left, expiring soon). |

## Endpoints

| Endpoint | Who |
|---|---|
| `POST /api/admin/codes/batches` | owner |
| `GET /api/admin/codes` (type, status, batch, search, pages) | owner |
| `GET /api/admin/codes/stats` | owner |
| `POST /api/admin/codes/{id}/revoke` | owner |
| `GET /api/admin/codes/batches/{id}/export` | owner |
| `GET /api/admin/audit-logs` | owner |
| `POST /api/auth/activate` | anyone (rate limited) |
| `GET /api/subscription`, `POST /api/subscription/redeem` | student |
| `GET /api/students?subscription=Expired|ExpiringSoon` | teacher and assistant |

## Known limits

- Nothing is blocked yet when a student's days run out, because there is no student content yet (exams, videos). Each later module must check the subscription on its student endpoints. The status and the rule are ready (`SubscriptionRules`).
- The owner's table shows the name from the login account. If a teacher renames a student, the owner still sees the first name.
- The platform audit log has an API but no screen yet (it belongs to the owner dashboard module).
- Export is a plain text file built in the browser, one code per line.
