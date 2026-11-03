using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Company.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/shortlists")]
// [Authorize] // Temporarily disabled for Dev Testing
public class CompanyShortlistsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public CompanyShortlistsController(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Creates a new recruitment shortlist (candidate collection) for the company.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CompanyShortlistDto>> CreateShortlist(
        [FromBody] CreateShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Shortlist title is required.");

        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");
        var userId = _currentUserService.UserId ?? Guid.Parse("01A1920B-BB54-78EE-AA25-895CE7C1248C");

        var shortlist = new CompanyShortlist
        {
            CompanyId = companyId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim()
        };

        _context.CompanyShortlists.Add(shortlist);

        _context.RecruiterActions.Add(new RecruiterAction
        {
            CompanyId = companyId,
            UserId = userId,
            ActionType = RecruiterActionType.ShortlistCandidate,
            EntityType = "CompanyShortlist",
            EntityId = shortlist.Id,
            Metadata = $"{{\"action\":\"CreateShortlist\",\"title\":\"{shortlist.Title}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetShortlistById), new { id = shortlist.Id }, new CompanyShortlistDto
        {
            Id = shortlist.Id,
            Title = shortlist.Title,
            Description = shortlist.Description,
            CandidatesCount = 0,
            CreatedAtUtc = shortlist.CreatedAtUtc,
            Candidates = new List<ShortlistCandidateItemDto>()
        });
    }

    /// <summary>
    /// Lists all candidate shortlists for the company.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CompanyShortlistDto>>> ListShortlists(CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");

        var list = await _context.CompanyShortlists
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new CompanyShortlistDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                CandidatesCount = s.Candidates.Count,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>
    /// Gets a shortlist and all its associated candidates with recruiter notes.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyShortlistDto>> GetShortlistById(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");

        var shortlist = await _context.CompanyShortlists
            .Include(s => s.Candidates)
            .ThenInclude(c => c.CompanyCandidate)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (shortlist == null) return NotFound("Shortlist not found.");

        return Ok(new CompanyShortlistDto
        {
            Id = shortlist.Id,
            Title = shortlist.Title,
            Description = shortlist.Description,
            CandidatesCount = shortlist.Candidates.Count,
            CreatedAtUtc = shortlist.CreatedAtUtc,
            Candidates = shortlist.Candidates.Select(c => new ShortlistCandidateItemDto
            {
                CandidateId = c.CompanyCandidateId,
                FullName = c.CompanyCandidate?.FullName ?? "Candidate",
                CurrentTitle = c.CompanyCandidate?.CurrentTitle,
                Location = c.CompanyCandidate?.Location,
                YearsOfExperience = c.CompanyCandidate?.YearsOfExperience,
                Notes = c.Notes,
                AddedAtUtc = c.AddedAtUtc
            }).ToList()
        });
    }

    /// <summary>
    /// Adds a candidate from the pool into this shortlist.
    /// </summary>
    [HttpPost("{id:guid}/candidates")]
    public async Task<ActionResult<ShortlistCandidateItemDto>> AddCandidateToShortlist(
        Guid id,
        [FromBody] AddCandidateToShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");
        var userId = _currentUserService.UserId ?? Guid.Parse("01A1920B-BB54-78EE-AA25-895CE7C1248C");

        var shortlist = await _context.CompanyShortlists
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (shortlist == null) return NotFound("Shortlist not found.");

        var candidate = await _context.CompanyCandidates
            .FirstOrDefaultAsync(c => c.Id == request.CompanyCandidateId && c.CompanyId == companyId, cancellationToken);

        if (candidate == null) return NotFound("Company candidate not found in your company pool.");

        var alreadyInShortlist = await _context.CompanyShortlistCandidates
            .AnyAsync(c => c.CompanyShortlistId == id && c.CompanyCandidateId == candidate.Id, cancellationToken);

        if (alreadyInShortlist) return BadRequest("Candidate is already in this shortlist.");

        var entry = new CompanyShortlistCandidate
        {
            CompanyShortlistId = id,
            CompanyCandidateId = candidate.Id,
            AddedByUserId = userId,
            Notes = request.Notes?.Trim(),
            AddedAtUtc = DateTime.UtcNow
        };

        _context.CompanyShortlistCandidates.Add(entry);

        _context.RecruiterActions.Add(new RecruiterAction
        {
            CompanyId = companyId,
            UserId = userId,
            ActionType = RecruiterActionType.ShortlistCandidate,
            EntityType = "CompanyCandidate",
            EntityId = candidate.Id,
            Metadata = $"{{\"action\":\"AddCandidateToShortlist\",\"shortlistId\":\"{id}\",\"candidateName\":\"{candidate.FullName}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new ShortlistCandidateItemDto
        {
            CandidateId = candidate.Id,
            FullName = candidate.FullName,
            CurrentTitle = candidate.CurrentTitle,
            Location = candidate.Location,
            YearsOfExperience = candidate.YearsOfExperience,
            Notes = entry.Notes,
            AddedAtUtc = entry.AddedAtUtc
        });
    }

    /// <summary>
    /// Removes a candidate from this shortlist.
    /// </summary>
    [HttpDelete("{id:guid}/candidates/{candidateId:guid}")]
    public async Task<IActionResult> RemoveCandidateFromShortlist(
        Guid id,
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");

        var entry = await _context.CompanyShortlistCandidates
            .Include(e => e.CompanyShortlist)
            .FirstOrDefaultAsync(e => e.CompanyShortlistId == id && e.CompanyCandidateId == candidateId && e.CompanyShortlist.CompanyId == companyId, cancellationToken);

        if (entry == null) return NotFound("Candidate was not found in this shortlist.");

        _context.CompanyShortlistCandidates.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Soft deletes a shortlist.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteShortlist(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.CompanyId ?? Guid.Parse("01A191C8-1123-7744-8899-AABBCCDDEEFF");

        var shortlist = await _context.CompanyShortlists
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (shortlist == null) return NotFound("Shortlist not found.");

        shortlist.IsDeleted = true;
        shortlist.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
