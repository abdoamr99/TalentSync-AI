using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Exceptions;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.Infrastructure.Services;

public class CandidateCvService : ICandidateCvService
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public CandidateCvService(IApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<CandidateCvDto> UploadCvAsync(
        Guid userId,
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        bool setAsPrimary = true,
        CancellationToken cancellationToken = default)
    {
        var (isValid, errorMessage) = FileValidationHelper.ValidateFile(stream, fileName, fileLength);
        if (!isValid)
        {
            throw new ArgumentException(errorMessage);
        }

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var fileHash = await FileValidationHelper.ComputeSha256HashAsync(stream, cancellationToken);

        var existingDuplicate = await _context.CandidateResumes
            .FirstOrDefaultAsync(cv => cv.CandidateProfileId == candidateProfile.Id && cv.FileHash == fileHash, cancellationToken);

        if (existingDuplicate != null)
        {
            throw new ConflictException("This exact CV file has already been uploaded.");
        }

        var storedRelativePath = await _fileStorageService.UploadFileAsync(
            stream,
            fileName,
            contentType,
            subFolder: $"candidates/{candidateProfile.Id}",
            cancellationToken: cancellationToken);

        if (setAsPrimary)
        {
            var previousPrimaries = await _context.CandidateResumes
                .Where(cv => cv.CandidateProfileId == candidateProfile.Id && cv.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var prev in previousPrimaries)
            {
                prev.IsPrimary = false;
            }
        }
        else
        {
            var hasAnyCv = await _context.CandidateResumes
                .AnyAsync(cv => cv.CandidateProfileId == candidateProfile.Id, cancellationToken);
            if (!hasAnyCv)
            {
                setAsPrimary = true;
            }
        }

        var rawTextContent = await FileValidationHelper.TryExtractTextAsync(stream, fileName);

        var candidateCv = new CandidateCv
        {
            CandidateProfileId = candidateProfile.Id,
            FileName = Path.GetFileName(fileName),
            StoredFilePath = storedRelativePath,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSizeBytes = fileLength,
            IsPrimary = setAsPrimary,
            FileHash = fileHash,
            RawTextContent = rawTextContent
        };

        _context.CandidateResumes.Add(candidateCv);
        await _context.SaveChangesAsync(cancellationToken);

        return new CandidateCvDto
        {
            Id = candidateCv.Id,
            FileName = candidateCv.FileName,
            ContentType = candidateCv.ContentType,
            FileSizeBytes = candidateCv.FileSizeBytes,
            IsPrimary = candidateCv.IsPrimary,
            FileHash = candidateCv.FileHash,
            CreatedAtUtc = candidateCv.CreatedAtUtc,
            RawTextPreview = candidateCv.RawTextContent
        };
    }

    public async Task<List<CandidateCvDto>> GetAllMyCvsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        return await _context.CandidateResumes
            .AsNoTracking()
            .Where(cv => cv.CandidateProfileId == candidateProfile.Id)
            .OrderByDescending(cv => cv.IsPrimary)
            .ThenByDescending(cv => cv.CreatedAtUtc)
            .Take(100)
            .Select(cv => new CandidateCvDto
            {
                Id = cv.Id,
                FileName = cv.FileName,
                ContentType = cv.ContentType,
                FileSizeBytes = cv.FileSizeBytes,
                IsPrimary = cv.IsPrimary,
                FileHash = cv.FileHash,
                CreatedAtUtc = cv.CreatedAtUtc,
                RawTextPreview = cv.RawTextContent
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidateCvDto?> GetCvByIdAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default)
    {
        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        return await _context.CandidateResumes
            .AsNoTracking()
            .Where(c => c.Id == cvId && c.CandidateProfileId == candidateProfile.Id)
            .Select(c => new CandidateCvDto
            {
                Id = c.Id,
                FileName = c.FileName,
                ContentType = c.ContentType,
                FileSizeBytes = c.FileSizeBytes,
                IsPrimary = c.IsPrimary,
                FileHash = c.FileHash,
                CreatedAtUtc = c.CreatedAtUtc,
                RawTextPreview = c.RawTextContent
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadCvAsync(
        Guid userId, 
        Guid cvId, 
        CancellationToken cancellationToken = default)
    {
        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var cv = await _context.CandidateResumes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cvId && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return null;

        var exists = await _fileStorageService.FileExistsAsync(cv.StoredFilePath, cancellationToken);
        if (!exists) return null;

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return (stream, cv.ContentType, cv.FileName);
    }

    public async Task<bool> SetPrimaryCvAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default)
    {
        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var targetCv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == cvId && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (targetCv == null) return false;

        var allUserCvs = await _context.CandidateResumes
            .Where(c => c.CandidateProfileId == candidateProfile.Id)
            .ToListAsync(cancellationToken);

        foreach (var cv in allUserCvs)
        {
            cv.IsPrimary = (cv.Id == cvId);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCvAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default)
    {
        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var targetCv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == cvId && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (targetCv == null) return false;

        var wasPrimary = targetCv.IsPrimary;
        _context.CandidateResumes.Remove(targetCv);

        try
        {
            await _fileStorageService.DeleteFileAsync(targetCv.StoredFilePath, cancellationToken);
        }
        catch
        {
            // Suppress physical file deletion failure so database deletion succeeds
        }

        if (wasPrimary)
        {
            var nextCv = await _context.CandidateResumes
                .Where(c => c.CandidateProfileId == candidateProfile.Id && c.Id != cvId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextCv != null)
            {
                nextCv.IsPrimary = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CandidateProfile> GetOrCreateCandidateProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (profile != null) return profile;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Authenticated user does not exist in the database.");
        }

        profile = new CandidateProfile
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber
        };

        _context.CandidateProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        return profile;
    }
}
