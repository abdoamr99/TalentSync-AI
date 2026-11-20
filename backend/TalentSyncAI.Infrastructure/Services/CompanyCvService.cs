using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Exceptions;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Common.Models;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Infrastructure.Services;

public class CompanyCvService : ICompanyCvService
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public CompanyCvService(IApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<CompanyBulkUploadResponseDto> UploadCompanyCvsAsync(
        Guid companyId,
        Guid userId,
        IReadOnlyList<CompanyCvUploadItem> files,
        CancellationToken cancellationToken = default)
    {
        var hasMembership = await _context.CompanyMembers
            .AnyAsync(m => m.CompanyId == companyId && m.UserId == userId && m.IsActive, cancellationToken);

        if (!hasMembership)
        {
            throw new ForbiddenException("User is not an authorized active member of this company.");
        }

        if (files == null || files.Count == 0)
        {
            throw new ArgumentException("Please provide at least one CV file to upload.");
        }

        if (files.Count > FileValidationHelper.MaxBulkUploadFiles)
        {
            throw new ArgumentException($"A maximum of {FileValidationHelper.MaxBulkUploadFiles} files can be uploaded per batch. Received {files.Count} files.");
        }

        var response = new CompanyBulkUploadResponseDto
        {
            TotalReceived = files.Count
        };

        foreach (var file in files)
        {
            var (isValid, errorMessage) = FileValidationHelper.ValidateFile(file.Stream, file.FileName, file.Length);
            if (!isValid)
            {
                response.SkippedFileNames.Add($"{file.FileName} ({errorMessage})");
                continue;
            }

            var fileHash = await FileValidationHelper.ComputeSha256HashAsync(file.Stream, cancellationToken);

            var isDuplicate = await _context.CompanyCvDocuments
                .AnyAsync(c => c.CompanyId == companyId && c.FileHash == fileHash, cancellationToken);

            if (isDuplicate)
            {
                response.TotalDuplicatesSkipped++;
                response.SkippedFileNames.Add($"{file.FileName} (Duplicate already exists in company pool)");
                continue;
            }

            var storedPath = await _fileStorageService.UploadFileAsync(
                file.Stream,
                file.FileName,
                file.ContentType,
                subFolder: $"companies/{companyId}",
                cancellationToken: cancellationToken);

            var cvDocument = new CompanyCvDocument
            {
                CompanyId = companyId,
                FileName = Path.GetFileName(file.FileName),
                StoredFilePath = storedPath,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSizeBytes = file.Length,
                ParsingStatus = CvParsingStatus.Uploaded,
                FileHash = fileHash,
                UploadedByUserId = userId
            };

            _context.CompanyCvDocuments.Add(cvDocument);
            response.TotalUploaded++;

            response.UploadedFiles.Add(new CompanyCvDocumentDto
            {
                Id = cvDocument.Id,
                CompanyId = cvDocument.CompanyId,
                FileName = cvDocument.FileName,
                ContentType = cvDocument.ContentType,
                FileSizeBytes = cvDocument.FileSizeBytes,
                ParsingStatus = cvDocument.ParsingStatus.ToString(),
                FileHash = cvDocument.FileHash,
                CandidateName = null,
                UploadedAtUtc = cvDocument.CreatedAtUtc
            });
        }

        if (response.TotalUploaded > 0)
        {
            var auditLog = new RecruiterAction
            {
                CompanyId = companyId,
                UserId = userId,
                ActionType = RecruiterActionType.UploadCv,
                EntityType = "CompanyCvDocument",
                EntityId = companyId,
                Metadata = $"Uploaded {response.TotalUploaded} CV(s) to company pool. {response.TotalDuplicatesSkipped} duplicate(s) skipped."
            };

            _context.RecruiterActions.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    public async Task<PagedResult<CompanyCvDocumentDto>> GetCompanyCvPoolAsync(
        Guid companyId,
        int pageIndex,
        int pageSize,
        CvParsingStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CompanyCvDocuments
            .AsNoTracking()
            .Include(c => c.CompanyCandidate)
            .Where(c => c.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(c => c.ParsingStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyCvDocumentDto
            {
                Id = c.Id,
                CompanyId = c.CompanyId,
                FileName = c.FileName,
                ContentType = c.ContentType,
                FileSizeBytes = c.FileSizeBytes,
                ParsingStatus = c.ParsingStatus.ToString(),
                FileHash = c.FileHash,
                CandidateName = c.CompanyCandidate != null ? c.CompanyCandidate.FullName : null,
                UploadedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyCvDocumentDto>(items, totalCount, pageIndex, pageSize);
    }

    public async Task<CompanyCvDocumentDto?> GetCompanyCvByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.CompanyCvDocuments
            .AsNoTracking()
            .Include(c => c.CompanyCandidate)
            .Where(c => c.Id == id && c.CompanyId == companyId)
            .Select(c => new CompanyCvDocumentDto
            {
                Id = c.Id,
                CompanyId = c.CompanyId,
                FileName = c.FileName,
                ContentType = c.ContentType,
                FileSizeBytes = c.FileSizeBytes,
                ParsingStatus = c.ParsingStatus.ToString(),
                FileHash = c.FileHash,
                CandidateName = c.CompanyCandidate != null ? c.CompanyCandidate.FullName : null,
                UploadedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadCompanyCvAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cv = await _context.CompanyCvDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, cancellationToken);

        if (cv == null) return null;

        var exists = await _fileStorageService.FileExistsAsync(cv.StoredFilePath, cancellationToken);
        if (!exists) return null;

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return (stream, cv.ContentType, cv.FileName);
    }
}
