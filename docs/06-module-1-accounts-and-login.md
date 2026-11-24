# Module 1: Accounts, login and platform admin

Short technical notes for developers. Status and test evidence are in `PROJECT_PROGRESS.md`.

## What this module does

- One account per person (mobile number). One login for all teachers the person belongs to.
- Students log in with a password, on one device only.
- Teachers, assistants and the platform team must also enter a 6-digit code from an authenticator app.
- The platform admin registers teachers. Each new teacher gets a new database.
- Every request is sent to the database of the teacher named in the login token.

## Login flow

1. `POST /api/auth/login` with phone and password.
2. Student: the answer has status `Authenticated` and the session.
3. Staff: the answer has status `TwoFactorRequired` (or `TwoFactorSetupRequired` the first time) and a short-lived challenge token. The client then calls `two-factor/setup` (first time only) and `two-factor/verify`.
4. A session has an access token (30 minutes), a refresh token (30 days, used once) and the list of teachers.
5. With one teacher, the session is already inside that teacher's platform. With several, the client calls `POST /api/auth/refresh` with the chosen `tenantId`. The same call switches teacher later.

## Services

| Service | What it does | Depends on | Main rules |
|---|---|---|---|
| `AuthService` | Login, two-factor, refresh, logout, invite | hub database, `TokenService`, password hasher, data protection | Same answer for unknown phone and wrong password. 5 wrong tries lock the account for 15 minutes. The counter resets only when login is complete. |
| `TokenService` | Creates access, refresh and challenge tokens | JWT settings, clock | A challenge token has a different audience, so the API refuses it as a login token. Only the hash of a refresh token is stored. |
| `Totp` | Authenticator codes (RFC 6238) | nothing | Accepts the current 30-second step and one step before and after. A code cannot be used twice. |
| `TenantAdminService` | Registers teachers, turns them on or off, new invite | hub database, `ITenantDatabaseManager` | Creates the database first. If a later step fails, the half-registered teacher is removed. |
| `AcademicTermService` | Term dates | hub database | Terms cannot overlap. |
| `ProfileService` | The user's own row at the chosen teacher | teacher database, `ICurrentUser` | The user id comes from the token, never from the request. |
| `TenantResolutionMiddleware` | Finds the teacher's database for each request | hub database | Checks on every request that the membership, the account and the teacher are all active. No caching, so a suspension works at once. |

## Security decisions

- Passwords are hashed with PBKDF2 (ASP.NET Identity hasher). Two-factor secrets are encrypted before saving. Invite codes and refresh tokens are stored as SHA-256 hashes.
- Every endpoint needs a login unless it is marked `[AllowAnonymous]`.
- Login endpoints are rate limited per IP address (20 calls per minute by default).
- Error answers carry a stable code in `title` (for example `Auth.InvalidCredentials`). The website translates the code to Arabic or English.
- The website keeps the access token in memory and the refresh token in `sessionStorage`. It never uses `localStorage` for tokens.

## Things to do before going to production

- Keep the ASP.NET Data Protection keys in a safe, persistent place. If they are lost, staff must set up two-factor again.
- Put real secrets (database passwords, JWT key, first admin password) in environment variables, not in files.
- Behind a reverse proxy, configure forwarded headers so rate limiting sees the real client address.
- Rate limiting is kept in memory. With more than one server it needs a shared store.
- One database per teacher means one connection pool per teacher. With many teachers, add a connection pooler such as PgBouncer.
- Consider moving the refresh token to an httpOnly cookie for the website.

## How to run

- API: set `E-Learning/appsettings.Development.json` (copy from the `.example` file), then run the `E-Learning.API` project.
- Website: `cd web`, `npm install`, `npm run dev`, open `http://localhost:5173`.
- Tests: `dotnet test E-Learning.sln`, then `cd web && npm test`, then `powershell -File scripts\run-e2e.ps1` for the browser tests.
