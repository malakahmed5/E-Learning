# Module 2: Teacher management

Short technical notes for developers. Status and test evidence are in `PROJECT_PROGRESS.md`.

## What this module does

Inside one teacher's platform, the teacher and the assistants manage centers, groups with weekly lesson times, and students. The teacher also manages assistants and reads a log of everything the assistants changed.

## Who may do what

| Area | Teacher | Assistant | Student |
|---|---|---|---|
| Centers, groups, students | yes | yes | no (403) |
| Unlock a student's device | yes | yes | no |
| Assistants (list, add, suspend) | yes | no (403) | no |
| Activity log | yes | no (403) | no |

The checks are on the controllers (`[Authorize(Roles = ...)]`). The website also hides the links, but that is only for convenience.

## Services

| Service | What it does | Main rules |
|---|---|---|
| `CenterService` | Places where lessons are given | Names are unique. A center with groups cannot be deleted. |
| `GroupService` | Groups and weekly lesson times | At least one lesson. A lesson ends after it starts. No repeated day and start time. Name unique inside a grade. A group with students cannot be deleted, only turned off. |
| `StudentService` | Add, list, edit, suspend students; unlock a device | Phone unique at the teacher. A student moves only between groups of the same grade. Parent phone is masked for assistants. Device unlock: at most 2 per term per account, counted across all teachers. |
| `AssistantService` | Add and suspend assistants | The assistant sets a password with a one-time invite code. Suspension works on the next request. |
| `AuditLogService` | Reads the activity log | Newest first, filter by assistant and date. |
| `AuditInterceptor` (Infrastructure) | Writes the activity log | Runs inside the database layer for every change an assistant saves. The change and its log row are saved in one transaction. |

## Two databases, no shared transaction

Adding a student or an assistant writes to the hub (login account and membership) and to the teacher's database (the person's data). The order is: hub account, then teacher database, then hub membership. If the last step fails, the rows of the teacher database are removed again.

Suspending changes the hub membership first, because that is what blocks access.

## Known limits

- The current school term is found with the UTC date, which is 2 to 3 hours behind Egypt time around midnight.
- Searching students uses `LIKE` on lower-case text. It is fine for one teacher's students; a very large teacher may need an index later.
- Assistants cannot be deleted, only suspended. This keeps the activity log readable.
