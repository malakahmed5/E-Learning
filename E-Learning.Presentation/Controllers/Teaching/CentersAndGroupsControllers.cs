using ELearning.Services.Abstraction;
using ELearning.Shared.DTOs.Teaching;
using ELearning.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Presentation.Controllers.Teaching
{
    // Teacher and assistants: the centers where lessons are given.
    // Students are refused here by the role check.
    [Authorize(Roles = AppRoles.Staff)]
    public class CentersController : ApiBaseController
    {
        private readonly ICenterService _centerService;

        public CentersController(ICenterService centerService)
        {
            _centerService = centerService;
        }

        [HttpGet]
        public async Task<ActionResult<List<CenterDto>>> List()
        {
            return Ok(await _centerService.ListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<CenterDto>> Create(SaveCenterRequest request)
        {
            return HandleResult(await _centerService.CreateAsync(request));
        }

        [HttpPut("{centerId:int}")]
        public async Task<ActionResult<CenterDto>> Update(int centerId, SaveCenterRequest request)
        {
            return HandleResult(await _centerService.UpdateAsync(centerId, request));
        }

        [HttpDelete("{centerId:int}")]
        public async Task<IActionResult> Delete(int centerId)
        {
            return HandleResult(await _centerService.DeleteAsync(centerId));
        }
    }

    // Teacher and assistants: groups and their weekly lesson times.
    [Authorize(Roles = AppRoles.Staff)]
    public class GroupsController : ApiBaseController
    {
        private readonly IGroupService _groupService;

        public GroupsController(IGroupService groupService)
        {
            _groupService = groupService;
        }

        [HttpGet]
        public async Task<ActionResult<List<GroupDto>>> List([FromQuery] bool includeInactive = false)
        {
            return Ok(await _groupService.ListAsync(includeInactive));
        }

        [HttpGet("{groupId:int}")]
        public async Task<ActionResult<GroupDto>> Get(int groupId)
        {
            return HandleResult(await _groupService.GetAsync(groupId));
        }

        [HttpPost]
        public async Task<ActionResult<GroupDto>> Create(SaveGroupRequest request)
        {
            return HandleResult(await _groupService.CreateAsync(request));
        }

        [HttpPut("{groupId:int}")]
        public async Task<ActionResult<GroupDto>> Update(int groupId, SaveGroupRequest request)
        {
            return HandleResult(await _groupService.UpdateAsync(groupId, request));
        }

        [HttpPut("{groupId:int}/status")]
        public async Task<IActionResult> SetActive(int groupId, SetActiveRequest request)
        {
            return HandleResult(await _groupService.SetActiveAsync(groupId, request.IsActive));
        }

        [HttpDelete("{groupId:int}")]
        public async Task<IActionResult> Delete(int groupId)
        {
            return HandleResult(await _groupService.DeleteAsync(groupId));
        }
    }
}
