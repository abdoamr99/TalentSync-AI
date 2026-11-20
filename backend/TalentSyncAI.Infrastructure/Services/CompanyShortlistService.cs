using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Exceptions;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Company.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Infrastructure.Services;

public class CompanyShortlistService : ICompanyShortlistService
{
    private readonly IApplicationDbContext _context;

    public CompanyShortlistService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyShortlistDto> CreateShortlistAsync(
        Guid companyId,
        Guid userId,
        CreateShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Shortlist title is required.");
        }

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

        return new CompanyShortlistDto
        {
            Id = shortlist.Id,
            Title = shortlist.Title,
            Description = shortlist.Description,
            CandidatesCount = 0,
            CreatedAtUtc = shortlist.CreatedAtUtc,
            Candidates = new List<ShortlistCandidateItemDto>()
        };
    }

    public async Task<List<CompanyShortlistDto>> ListShortlistsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.CompanyShortlists
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(100)
            .Select(s => new CompanyShortlistDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                CandidatesCount = s.Candidates.Count,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyShortlistDto?> GetShortlistByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var shortlist = await _context.CompanyShortlists
            .AsNoTracking()
            .Include(s => s.Candidates)
            .ThenInclude(c => c.CompanyCandidate)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

        if (shortlist == null) return null;

        return new CompanyShortlistDto
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
        };
    }

    public async Task<ShortlistCandidateItemDto> AddCandidateToShortlistAsync(
        Guid companyId,
        Guid userId,
        Guid shortlistId,
        AddCandidateToShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var shortlist = await _context.CompanyShortlists
            .FirstOrDefaultAsync(s => s.Id == shortlistId && s.CompanyId == companyId, cancellationToken);

        if (shortlist == null)
        {
            throw new KeyNotFoundException("Shortlist not found.");
        }

        var candidate = await _context.CompanyCandidates
            .FirstOrDefaultAsync(c => c.Id == request.CompanyCandidateId && c.CompanyId == companyId, cancellationToken);

        if (candidate == null)
        {
            throw new KeyNotFoundException("Company candidate not found in your company pool.");
        }

        var alreadyInShortlist = await _context.CompanyShortlistCandidates
            .AnyAsync(c => c.CompanyShortlistId == shortlistId && c.CompanyCandidateId == candidate.Id, cancellationToken);

        if (alreadyInShortlist)
        {
            throw new ConflictException("Candidate is already in this shortlist.");
        }

        var entry = new CompanyShortlistCandidate
        {
            CompanyShortlistId = shortlistId,
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
            Metadata = $"{{\"action\":\"AddCandidateToShortlist\",\"shortlistId\":\"{shortlistId}\",\"candidateName\":\"{candidate.FullName}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new ShortlistCandidateItemDto
        {
            CandidateId = candidate.Id,
            FullName = candidate.FullName,
            CurrentTitle = candidate.CurrentTitle,
            Location = candidate.Location,
            YearsOfExperience = candidate.YearsOfExperience,
            Notes = entry.Notes,
            AddedAtUtc = entry.AddedAtUtc
        };
    }
}
