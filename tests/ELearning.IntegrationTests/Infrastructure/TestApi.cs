using ELearning.Core.Contracts;
using ELearning.Core.Entities.Identity;
using ELearning.Services.Security;
using ELearning.Shared.DTOs.Admin;
using ELearning.Shared.DTOs.Auth;
using ELearning.Shared.DTOs.Codes;
using ELearning.Shared.DTOs.Teaching;
using ELearningHub.Core.Entities;
using ELearningHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ELearning.IntegrationTests.Infrastructure
{
    // A staff account the tests can log in with (the secret is what the authenticator app would hold).
    public record StaffLogin(string Phone, string Password, string TwoFactorSecret);

    // Session is the teacher's logged-in session, ready to use (so tests do not need to log in again).
    public record CreatedTeacher(TenantDto Tenant, StaffLogin Login, SessionDto Session);

    // Helper used by the tests: short ways to call the API and to prepare data.
    // Tests should read like a story; the plumbing lives here.
    public class TestApi
    {
        public const string DefaultPassword = "Password123";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private static int _phoneCounter;
        private static int _slugCounter;
        private static readonly SemaphoreSlim AdminLock = new(1, 1);
        private static SessionDto? _adminSession;
        private static string? _adminSecret;
        private static readonly SemaphoreSlim SharedTeacherLock = new(1, 1);
        private static CreatedTeacher? _sharedTeacher;

        // Last two-factor time step used per secret, so a helper never sends the same code twice.
        private static readonly Dictionary<string, long> LastUsedStep = new();

        private readonly ApiFixture _fixture;

        public TestApi(ApiFixture fixture)
        {
            _fixture = fixture;
        }

        // ---------- Calling the API ----------

        public HttpClient Anonymous()
        {
            return _fixture.CreateClient();
        }

        public HttpClient WithToken(string accessToken)
        {
            var client = _fixture.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body)
        {
            return client.PostAsJsonAsync(url, body, JsonOptions);
        }

        public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return value ?? throw new InvalidOperationException("The response body was empty.");
        }

        // Asserts the HTTP status and, for errors, the error code in the "title" field.
        public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == status, $"Expected {(int)status} but got {(int)response.StatusCode}. Body: {body}");
            Assert.Contains(errorCode, body);
        }

        // ---------- Unique test values ----------

        public static string NextPhone()
        {
            var number = 20000000 + Interlocked.Increment(ref _phoneCounter);
            return "010" + number;
        }

        public static string NextSlug()
        {
            return "teacher-" + Interlocked.Increment(ref _slugCounter);
        }

        // ---------- Logging in ----------

        public async Task<HttpResponseMessage> LoginAsync(string phone, string password, string? deviceId = null)
        {
            return await PostAsync(Anonymous(), "/api/auth/login", new LoginRequest
            {
                PhoneNumber = phone,
                Password = password,
                DeviceId = deviceId,
            });
        }

        public async Task<SessionDto> LoginStudentAsync(string phone, string deviceId, string password = DefaultPassword)
        {
            var response = await LoginAsync(phone, password, deviceId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var login = await ReadAsync<LoginResponse>(response);
            Assert.Equal(LoginStatus.Authenticated, login.Status);
            return login.Session!;
        }

        // Full staff login: password, then the code from the "authenticator app".
        public async Task<SessionDto> LoginStaffAsync(StaffLogin staff)
        {
            var loginResponse = await LoginAsync(staff.Phone, staff.Password);
            var login = await ReadAsync<LoginResponse>(loginResponse);
            Assert.Equal(LoginStatus.TwoFactorRequired, login.Status);

            var verifyResponse = await VerifyTwoFactorAsync(login.ChallengeToken!, NextCode(staff.TwoFactorSecret));
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            return await ReadAsync<SessionDto>(verifyResponse);
        }

        public Task<HttpResponseMessage> VerifyTwoFactorAsync(string challengeToken, string code)
        {
            return PostAsync(Anonymous(), "/api/auth/two-factor/verify", new TwoFactorVerifyRequest
            {
                ChallengeToken = challengeToken,
                Code = code,
            });
        }

        // First-time staff login: sets up the authenticator secret and returns it with the session.
        public async Task<(SessionDto Session, string Secret)> SetUpTwoFactorAndLoginAsync(string phone, string password)
        {
            var loginResponse = await LoginAsync(phone, password);
            var login = await ReadAsync<LoginResponse>(loginResponse);
            Assert.Equal(LoginStatus.TwoFactorSetupRequired, login.Status);

            var setupResponse = await PostAsync(Anonymous(), "/api/auth/two-factor/setup",
                new TwoFactorSetupRequest { ChallengeToken = login.ChallengeToken! });
            Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
            var setup = await ReadAsync<TwoFactorSetupResponse>(setupResponse);

            var verifyResponse = await VerifyTwoFactorAsync(login.ChallengeToken!, NextCode(setup.Secret));
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            return (await ReadAsync<SessionDto>(verifyResponse), setup.Secret);
        }

        // Gives a code that was not used before for this secret.
        // The API refuses a code twice, so the second login inside the same 30 seconds uses the next step's code
        // (the API accepts one step ahead).
        public string NextCode(string secret)
        {
            var step = Totp.GetStep(_fixture.Clock.GetUtcNow());

            lock (LastUsedStep)
            {
                if (LastUsedStep.TryGetValue(secret, out var lastStep) && lastStep >= step)
                    step = lastStep + 1;

                LastUsedStep[secret] = step;
            }
            return Totp.GetCode(secret, step);
        }

        public string CurrentCode(string secret)
        {
            return Totp.GetCode(secret, Totp.GetStep(_fixture.Clock.GetUtcNow()));
        }

        // The platform admin session, created once and shared by all tests.
        public async Task<SessionDto> AdminSessionAsync()
        {
            await AdminLock.WaitAsync();
            try
            {
                if (_adminSession is null)
                {
                    var (session, secret) = await SetUpTwoFactorAndLoginAsync(ApiFixture.AdminPhone, ApiFixture.AdminPassword);
                    _adminSession = session;
                    _adminSecret = secret;
                }
                return _adminSession;
            }
            finally
            {
                AdminLock.Release();
            }
        }

        public async Task<StaffLogin> AdminLoginDetailsAsync()
        {
            await AdminSessionAsync();
            return new StaffLogin(ApiFixture.AdminPhone, ApiFixture.AdminPassword, _adminSecret!);
        }

        public async Task<HttpClient> AdminClientAsync()
        {
            var session = await AdminSessionAsync();
            return WithToken(session.AccessToken);
        }

        // ---------- Preparing data ----------

        // Registers a teacher through the real admin endpoint (this creates a real database),
        // then accepts the invite and sets up two-factor, like a real teacher would.
        public async Task<CreatedTeacher> CreateTeacherAsync(string? displayName = null)
        {
            var admin = await AdminClientAsync();
            var phone = NextPhone();
            var slug = NextSlug();

            var createResponse = await PostAsync(admin, "/api/admin/tenants", new CreateTenantRequest
            {
                Slug = slug,
                DisplayName = displayName ?? "Teacher " + slug,
                TeacherFullName = "Mr " + slug,
                TeacherPhoneNumber = phone,
            });
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var created = await ReadAsync<CreateTenantResponse>(createResponse);

            var acceptResponse = await PostAsync(Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = phone,
                InviteCode = created.Invite!.Code,
                Password = DefaultPassword,
            });
            Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

            var (session, secret) = await SetUpTwoFactorAndLoginAsync(phone, DefaultPassword);
            return new CreatedTeacher(created.Tenant, new StaffLogin(phone, DefaultPassword, secret), session);
        }

        // ---------- Teacher management helpers (Module 2) ----------

        public HttpClient TeacherClient(CreatedTeacher teacher)
        {
            return WithToken(teacher.Session.AccessToken);
        }

        // The teacher adds an assistant through the API; the assistant accepts the invite and sets up two-factor.
        public async Task<(StaffLogin Login, SessionDto Session, AssistantDto Assistant)> CreateAssistantAsync(CreatedTeacher teacher)
        {
            var phone = NextPhone();
            var response = await PostAsync(TeacherClient(teacher), "/api/assistants",
                new CreateAssistantRequest { FullName = "Assistant " + phone, PhoneNumber = phone });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var created = await ReadAsync<CreateAssistantResponse>(response);

            var accept = await PostAsync(Anonymous(), "/api/auth/accept-invite", new AcceptInviteRequest
            {
                PhoneNumber = phone,
                InviteCode = created.Invite!.Code,
                Password = DefaultPassword,
            });
            Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

            var (session, secret) = await SetUpTwoFactorAndLoginAsync(phone, DefaultPassword);
            return (new StaffLogin(phone, DefaultPassword, secret), session, created.Assistant);
        }

        public async Task<GroupDto> CreateGroupAsync(HttpClient staff, string? name = null, int gradeLevel = 1, int? centerId = null)
        {
            var response = await PostAsync(staff, "/api/groups", new SaveGroupRequest
            {
                Name = name ?? "Group " + Interlocked.Increment(ref _slugCounter),
                GradeLevel = gradeLevel,
                CenterId = centerId,
                Schedules =
                [
                    new ScheduleDto { DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(18, 0) },
                    new ScheduleDto { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(18, 0) },
                ],
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await ReadAsync<GroupDto>(response);
        }

        public static CreateStudentRequest NewStudentRequest(int groupId, string? phone = null, string? fullName = null)
        {
            phone ??= NextPhone();
            return new CreateStudentRequest
            {
                FullName = fullName ?? "Student " + phone,
                PhoneNumber = phone,
                GroupId = groupId,
                ParentName = "Parent of " + phone,
                ParentWhatsApp = "01155501234",
                ParentConsent = true,
            };
        }

        public async Task<StudentDto> CreateStudentAsync(HttpClient staff, int groupId, string? phone = null, string? fullName = null)
        {
            var response = await PostAsync(staff, "/api/students", NewStudentRequest(groupId, phone, fullName));
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Creating a student failed: {(int)response.StatusCode} {body}");
            return JsonSerializer.Deserialize<StudentDto>(body, JsonOptions)!;
        }

        // Gives an account a password directly, as if the student had activated it
        // (student activation belongs to a later module).
        public async Task SetPasswordAsync(string phone)
        {
            using var scope = _fixture.Services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();

            var account = await hub.Accounts.SingleAsync(a => a.PhoneNumber == phone);
            account.PasswordHash = passwordHasher.HashPassword(account, DefaultPassword);
            await hub.SaveChangesAsync();
        }

        // Makes sure a school term covers today's real date (device changes are counted per term).
        public async Task EnsureCurrentTermAsync()
        {
            await ChangeHubAsync(async hub =>
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (await hub.AcademicTerms.AnyAsync(t => t.StartDate <= today && today <= t.EndDate))
                    return;

                hub.AcademicTerms.Add(new AcademicTerm
                {
                    Name = "Current test term",
                    StartDate = today.AddDays(-30),
                    EndDate = today.AddDays(60),
                });
            });
        }

        // One teacher shared by tests that only need "a student at some teacher".
        // Creating a teacher creates a whole database, so we avoid doing it when a test does not need its own.
        public async Task<CreatedTeacher> SharedTeacherAsync()
        {
            await SharedTeacherLock.WaitAsync();
            try
            {
                _sharedTeacher ??= await CreateTeacherAsync("Shared Teacher");
                return _sharedTeacher;
            }
            finally
            {
                SharedTeacherLock.Release();
            }
        }

        // Adds a person directly to the databases (faster than going through endpoints that belong to later modules).
        // Creates the hub account when the phone is new, the row inside the teacher's database, and the link between them.
        public async Task<string> AddMemberAsync(int tenantId, MembershipRole role, string? phone = null, string? fullName = null)
        {
            phone ??= NextPhone();
            fullName ??= role + " " + phone;

            using var scope = _fixture.Services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();
            var tenantDatabases = scope.ServiceProvider.GetRequiredService<ITenantDatabaseManager>();

            var account = await hub.Accounts.FirstOrDefaultAsync(a => a.PhoneNumber == phone);
            if (account is null)
            {
                account = new Account { FullName = fullName, PhoneNumber = phone };
                account.PasswordHash = passwordHasher.HashPassword(account, DefaultPassword);
                hub.Accounts.Add(account);
                await hub.SaveChangesAsync();
            }

            var tenant = await hub.Tenants.SingleAsync(t => t.Id == tenantId);

            await using var tenantDatabase = tenantDatabases.Open(tenant.ConnectionString);
            var user = new User
            {
                HubAccountId = account.Id,
                Role = Enum.Parse<UserRole>(role.ToString()),
                FullName = fullName,
                PhoneNumber = phone,
            };
            tenantDatabase.Users.Add(user);
            await tenantDatabase.SaveChangesAsync();

            hub.Memberships.Add(new Membership
            {
                AccountId = account.Id,
                TenantId = tenantId,
                Role = role,
                TenantUserId = user.Id,
            });
            await hub.SaveChangesAsync();
            return phone;
        }

        // Changes hub data directly, for example to suspend a membership or disable an account.
        public async Task ChangeHubAsync(Func<HubDbContext, Task> change)
        {
            using var scope = _fixture.Services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await change(hub);
            await hub.SaveChangesAsync();
        }

        public async Task<T> ReadHubAsync<T>(Func<HubDbContext, Task<T>> query)
        {
            using var scope = _fixture.Services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            return await query(hub);
        }

        // ---------- Subscription code helpers (Module 3) ----------

        // The platform admin generates codes through the real endpoint. Returns them with dashes.
        public async Task<GeneratedBatchDto> GenerateCodesAsync(CodeKind type, int count = 1, DateTime? redeemBefore = null, string? note = null)
        {
            var admin = await AdminClientAsync();
            var response = await PostAsync(admin, "/api/admin/codes/batches", new GenerateCodesRequest
            {
                Type = type,
                Count = count,
                RedeemBefore = redeemBefore,
                Note = note,
            });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Generating codes failed: {(int)response.StatusCode} {body}");
            return JsonSerializer.Deserialize<GeneratedBatchDto>(body, JsonOptions)!;
        }

        public async Task<string> NewCodeAsync(CodeKind type)
        {
            return (await GenerateCodesAsync(type)).Codes.Single();
        }

        // A student added by the teacher, with a password, already logged in on "phone-A".
        public async Task<(StudentDto Student, HttpClient Client)> NewLoggedInStudentAsync(CreatedTeacher teacher, string? phone = null)
        {
            var teacherClient = TeacherClient(teacher);
            var group = await CreateGroupAsync(teacherClient);
            var student = await CreateStudentAsync(teacherClient, group.Id, phone);
            await SetPasswordAsync(student.PhoneNumber);

            var session = await LoginStudentAsync(student.PhoneNumber, "phone-A");

            // A student of several teachers has to choose one after login.
            if (session.ActiveMembership is null)
            {
                var chooseResponse = await PostAsync(Anonymous(), "/api/auth/refresh", new RefreshRequest
                {
                    RefreshToken = session.RefreshToken,
                    DeviceId = "phone-A",
                    TenantId = teacher.Tenant.Id,
                });
                session = await ReadAsync<SessionDto>(chooseResponse);
            }
            return (student, WithToken(session.AccessToken));
        }

        public static Task<HttpResponseMessage> RedeemAsync(HttpClient studentClient, string code)
        {
            return PostAsync(studentClient, "/api/subscription/redeem", new RedeemCodeRequest { Code = code });
        }

        public static async Task<SubscriptionStatusDto> SubscriptionStatusAsync(HttpClient studentClient)
        {
            var response = await studentClient.GetAsync("/api/subscription");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await ReadAsync<SubscriptionStatusDto>(response);
        }

        // The current time as the API sees it (tests can move the API's clock).
        public DateTime ApiNow()
        {
            return _fixture.Clock.GetUtcNow().UtcDateTime;
        }
    }
}