using ELearning.Services.Abstraction;
using ELearning.Shared.CommonResponses;
using ELearning.Shared.DTOs.Admin;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Services.Admin
{
    // Platform team only: the start and end date of each school term.
    // Terms are needed for the rule "a student's device can be changed at most twice per term".
    public class AcademicTermService : IAcademicTermService
    {
        private readonly IHubDbContext _hub;

        public AcademicTermService(IHubDbContext hub)
        {
            _hub = hub;
        }

        public async Task<List<AcademicTermDto>> ListAsync()
        {
            return await _hub.AcademicTerms
                .AsNoTracking()
                .OrderByDescending(t => t.StartDate)
                .Select(t => new AcademicTermDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                })
                .ToListAsync();
        }

        public async Task<Result<AcademicTermDto>> CreateAsync(CreateAcademicTermRequest request)
        {
            if (request.EndDate <= request.StartDate)
                return Error.Validation("AcademicTerm.InvalidDates", "The end date must be after the start date.");

            // Two terms must not share a day. Otherwise we could not tell which term a date belongs to.
            var overlapsAnotherTerm = await _hub.AcademicTerms.AnyAsync(t =>
                t.StartDate <= request.EndDate && request.StartDate <= t.EndDate);

            if (overlapsAnotherTerm)
                return Error.Conflict("AcademicTerm.Overlap", "These dates overlap with another term.");

            var term = new AcademicTerm
            {
                Name = request.Name.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
            };
            _hub.AcademicTerms.Add(term);
            await _hub.SaveChangesAsync();

            return new AcademicTermDto
            {
                Id = term.Id,
                Name = term.Name,
                StartDate = term.StartDate,
                EndDate = term.EndDate,
            };
        }
    }
}
