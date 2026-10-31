using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.Infrastructure.Data.Configurations;

public class CompanySideConfigurations :
    IEntityTypeConfiguration<CompanyCandidate>,
    IEntityTypeConfiguration<CompanyCvDocument>,
    IEntityTypeConfiguration<CompanyCandidateSkill>,
    IEntityTypeConfiguration<CompanyCandidateExperience>,
    IEntityTypeConfiguration<CompanyCandidateEducation>,
    IEntityTypeConfiguration<CandidateSearch>,
    IEntityTypeConfiguration<CandidateSearchResult>,
    IEntityTypeConfiguration<CompanyShortlist>,
    IEntityTypeConfiguration<CompanyShortlistCandidate>,
    IEntityTypeConfiguration<AiProcessingRun>,
    IEntityTypeConfiguration<RecruiterAction>
{
    public void Configure(EntityTypeBuilder<CompanyCandidate> builder)
    {
        builder.ToTable("CompanyCandidates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.FullName).HasMaxLength(100).IsRequired();

        builder.HasIndex(c => new { c.CompanyId, c.CreatedAtUtc });

        builder.HasOne(c => c.Company)
            .WithMany(comp => comp.CandidatePool)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Skills)
            .WithOne(s => s.CompanyCandidate)
            .HasForeignKey(s => s.CompanyCandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Experiences)
            .WithOne(e => e.CompanyCandidate)
            .HasForeignKey(e => e.CompanyCandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Educations)
            .WithOne(ed => ed.CompanyCandidate)
            .HasForeignKey(ed => ed.CompanyCandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CompanyCvDocument> builder)
    {
        builder.ToTable("CompanyCvDocuments");
        builder.HasKey(cv => cv.Id);
        builder.Property(cv => cv.Id).ValueGeneratedNever();

        builder.Property(cv => cv.FileName).HasMaxLength(256).IsRequired();
        builder.Property(cv => cv.StoredFilePath).HasMaxLength(500).IsRequired();
        builder.Property(cv => cv.FileHash).HasMaxLength(64);

        builder.HasIndex(cv => new { cv.CompanyId, cv.CreatedAtUtc });

        builder.HasOne(cv => cv.Company)
            .WithMany(comp => comp.UploadedCvs)
            .HasForeignKey(cv => cv.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cv => cv.CompanyCandidate)
            .WithMany(cand => cand.CvFiles)
            .HasForeignKey(cv => cv.CompanyCandidateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(cv => cv.UploadedByUser)
            .WithMany()
            .HasForeignKey(cv => cv.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<CompanyCandidateSkill> builder)
    {
        builder.ToTable("CompanyCandidateSkills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
    }

    public void Configure(EntityTypeBuilder<CompanyCandidateExperience> builder)
    {
        builder.ToTable("CompanyCandidateExperiences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(200).IsRequired();
    }

    public void Configure(EntityTypeBuilder<CompanyCandidateEducation> builder)
    {
        builder.ToTable("CompanyCandidateEducations");
        builder.HasKey(ed => ed.Id);
        builder.Property(ed => ed.Id).ValueGeneratedNever();
        builder.Property(ed => ed.Institution).HasMaxLength(200).IsRequired();
        builder.Property(ed => ed.Degree).HasMaxLength(200).IsRequired();
    }

    public void Configure(EntityTypeBuilder<CandidateSearch> builder)
    {
        builder.ToTable("CandidateSearches");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.NaturalLanguageQuery).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.SavedSearchTitle).HasMaxLength(200);

        builder.HasIndex(s => new { s.CompanyId, s.PerformedAtUtc });

        builder.HasOne(s => s.Company)
            .WithMany(c => c.Searches)
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PerformedByUser)
            .WithMany()
            .HasForeignKey(s => s.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Results)
            .WithOne(r => r.CandidateSearch)
            .HasForeignKey(r => r.CandidateSearchId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CandidateSearchResult> builder)
    {
        builder.ToTable("CandidateSearchResults");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.MatchScore).HasPrecision(5, 2);

        builder.HasOne(r => r.CompanyCandidate)
            .WithMany()
            .HasForeignKey(r => r.CompanyCandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CompanyShortlist> builder)
    {
        builder.ToTable("CompanyShortlists");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();

        builder.HasIndex(s => new { s.CompanyId, s.CreatedAtUtc });

        builder.HasOne(s => s.Company)
            .WithMany(c => c.Shortlists)
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Candidates)
            .WithOne(c => c.CompanyShortlist)
            .HasForeignKey(c => c.CompanyShortlistId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CompanyShortlistCandidate> builder)
    {
        builder.ToTable("CompanyShortlistCandidates");
        builder.HasKey(sc => sc.Id);
        builder.Property(sc => sc.Id).ValueGeneratedNever();

        builder.HasIndex(sc => new { sc.CompanyShortlistId, sc.CompanyCandidateId }).IsUnique();

        builder.HasOne(sc => sc.CompanyCandidate)
            .WithMany()
            .HasForeignKey(sc => sc.CompanyCandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sc => sc.AddedByUser)
            .WithMany()
            .HasForeignKey(sc => sc.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<AiProcessingRun> builder)
    {
        builder.ToTable("AiProcessingRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EntityType).HasMaxLength(100).IsRequired();

        builder.HasIndex(r => new { r.CompanyId, r.Status });

        builder.HasOne(r => r.Company)
            .WithMany(c => c.AiRuns)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TriggeredByUser)
            .WithMany()
            .HasForeignKey(r => r.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<RecruiterAction> builder)
    {
        builder.ToTable("RecruiterActions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        builder.HasIndex(a => new { a.CompanyId, a.PerformedAtUtc });

        builder.HasOne(a => a.Company)
            .WithMany(c => c.RecruiterActions)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

