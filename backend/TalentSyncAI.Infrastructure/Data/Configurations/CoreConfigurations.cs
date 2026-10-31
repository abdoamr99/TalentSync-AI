using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.Infrastructure.Data.Configurations;

public class CoreConfigurations :
    IEntityTypeConfiguration<User>,
    IEntityTypeConfiguration<Company>,
    IEntityTypeConfiguration<CompanyMember>,
    IEntityTypeConfiguration<CandidateProfile>,
    IEntityTypeConfiguration<CandidateCv>,
    IEntityTypeConfiguration<TargetJobApplication>,
    IEntityTypeConfiguration<JobMatchAnalysis>,
    IEntityTypeConfiguration<CompanyRoleResearch>,
    IEntityTypeConfiguration<TailoredResume>,
    IEntityTypeConfiguration<InterviewPreparation>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();

        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(u => u.CandidateProfile)
            .WithOne(c => c.User)
            .HasForeignKey<CandidateProfile>(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(100).IsRequired();

        builder.HasIndex(c => c.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("CompanyMembers");
        builder.HasKey(cm => cm.Id);
        builder.Property(cm => cm.Id).ValueGeneratedNever();

        builder.HasIndex(cm => new { cm.UserId, cm.CompanyId }).IsUnique();

        builder.HasOne(cm => cm.Company)
            .WithMany(c => c.Members)
            .HasForeignKey(cm => cm.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cm => cm.User)
            .WithMany(u => u.CompanyMemberships)
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasIndex(c => c.UserId).IsUnique();

        builder.HasMany(c => c.Resumes)
            .WithOne(r => r.CandidateProfile)
            .HasForeignKey(r => r.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.TargetApplications)
            .WithOne(t => t.CandidateProfile)
            .HasForeignKey(t => t.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CandidateCv> builder)
    {
        builder.ToTable("CandidateResumes");
        builder.HasKey(cv => cv.Id);
        builder.Property(cv => cv.Id).ValueGeneratedNever();

        builder.Property(cv => cv.FileName).HasMaxLength(256).IsRequired();
        builder.Property(cv => cv.StoredFilePath).HasMaxLength(500).IsRequired();
    }

    public void Configure(EntityTypeBuilder<TargetJobApplication> builder)
    {
        builder.ToTable("TargetJobApplications");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TargetCompany).HasMaxLength(200).IsRequired();
        builder.Property(t => t.TargetRole).HasMaxLength(200).IsRequired();
        builder.Property(t => t.RawJobDescription).IsRequired();

        builder.HasOne(t => t.MatchAnalysis)
            .WithOne(a => a.TargetJobApplication)
            .HasForeignKey<JobMatchAnalysis>(a => a.TargetJobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.CompanyResearch)
            .WithOne(r => r.TargetJobApplication)
            .HasForeignKey<CompanyRoleResearch>(r => r.TargetJobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.TailoredResume)
            .WithOne(tr => tr.TargetJobApplication)
            .HasForeignKey<TailoredResume>(tr => tr.TargetJobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.InterviewPrep)
            .WithOne(p => p.TargetJobApplication)
            .HasForeignKey<InterviewPreparation>(p => p.TargetJobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<JobMatchAnalysis> builder)
    {
        builder.ToTable("JobMatchAnalyses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.MatchScore).HasPrecision(5, 2);
    }

    public void Configure(EntityTypeBuilder<CompanyRoleResearch> builder)
    {
        builder.ToTable("CompanyRoleResearches");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
    }

    public void Configure(EntityTypeBuilder<TailoredResume> builder)
    {
        builder.ToTable("TailoredResumes");
        builder.HasKey(tr => tr.Id);
        builder.Property(tr => tr.Id).ValueGeneratedNever();
        builder.Property(tr => tr.GeneratedPdfPath).HasMaxLength(500);
    }

    public void Configure(EntityTypeBuilder<InterviewPreparation> builder)
    {
        builder.ToTable("InterviewPreparations");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
    }
}

