using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs;
using ELearning.Shared.DTOs.Teaching;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers.Teaching
{
    // Teacher and assistants: the students of this teacher.
    [Authorize(Roles = AppRoles.Staff)]
    public class StudentsController : ApiBaseController
    {
        private readonly IStudentService _studentService;

        public StudentsController(IStudentService studentService)
        {
            _studentService = studentService;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResult<StudentDto>>> List([FromQuery] StudentQuery query)
        {
            return Ok(await _studentService.ListAsync(query));
        }

        [HttpGet("{studentId:int}")]
        public async Task<ActionResult<StudentDto>> Get(int studentId)
        {
            return HandleResult(await _studentService.GetAsync(studentId));
        }

        [HttpPost]
        public async Task<ActionResult<StudentDto>> Create(CreateStudentRequest request)
        {
            return HandleResult(await _studentService.CreateAsync(request));
        }

        [HttpPut("{studentId:int}")]
        public async Task<ActionResult<StudentDto>> Update(int studentId, UpdateStudentRequest request)
        {
            return HandleResult(await _studentService.UpdateAsync(studentId, request));
        }

        [HttpPut("{studentId:int}/status")]
        public async Task<IActionResult> SetActive(int studentId, SetActiveRequest request)
        {
            return HandleResult(await _studentService.SetActiveAsync(studentId, request.IsActive));
        }

        // Lets the student log in from a new phone (at most twice per term).
        [HttpPost("{studentId:int}/device-reset")]
        public async Task<ActionResult<DeviceResetDto>> ResetDevice(int studentId)
        {
            return HandleResult(await _studentService.ResetDeviceAsync(studentId));
        }
    }

    // Teacher only. Assistants cannot add, suspend or even list assistants (it is a "settings" area).
    [Authorize(Roles = AppRoles.Teacher)]
    public class AssistantsController : ApiBaseController
    {
        private readonly IAssistantService _assistantService;

        public AssistantsController(IAssistantService assistantService)
        {
            _assistantService = assistantService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AssistantDto>>> List()
        {
            return Ok(await _assistantService.ListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<CreateAssistantResponse>> Create(CreateAssistantRequest request)
        {
            return HandleResult(await _assistantService.CreateAsync(request));
        }

        [HttpPut("{assistantId:int}/status")]
        public async Task<IActionResult> SetActive(int assistantId, SetActiveRequest request)
        {
            return HandleResult(await _assistantService.SetActiveAsync(assistantId, request.IsActive));
        }
    }

    // Teacher only: what the assistants did.
    [Authorize(Roles = AppRoles.Teacher)]
    [Route("api/audit-logs")]
    public class AuditLogsController : ApiBaseController
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResult<AuditLogDto>>> List([FromQuery] AuditLogQuery query)
        {
            return Ok(await _auditLogService.ListAsync(query));
        }
    }
}
