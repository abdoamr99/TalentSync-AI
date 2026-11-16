using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TalentSyncAI.API.Middleware;
using TalentSyncAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p =>
    {
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod();
    });
});

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "SUPER_SECRET_KEY_FOR_LOCAL_DEV_TALENTSYNCAI_PLATFORM_2026_CHANGE_IN_PROD";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TalentSyncAI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TalentSyncAI_Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token only (without 'Bearer ' prefix, Swagger UI adds it automatically)"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

var app = builder.Build();

// Auto-apply EF Core database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<TalentSyncAI.Infrastructure.Data.ApplicationDbContext>();
        context.Database.Migrate();

        // Seed default development testing accounts if not present
        var devCandidateId = Guid.Parse("01a1925a-1825-7eaa-acb4-416264bf7202");
        if (!context.Users.Any(u => u.Id == devCandidateId))
        {
            context.Users.Add(new TalentSyncAI.Domain.Entities.User
            {
                Id = devCandidateId,
                Email = "candidate@talentsync.ai",
                NormalizedEmail = "CANDIDATE@TALENTSYNC.AI",
                PasswordHash = "DEV_MOCK_PASSWORD_HASH",
                FirstName = "Dev",
                LastName = "Candidate",
                UserType = TalentSyncAI.Domain.Enums.UserType.Candidate,
                IsActive = true
            });
        }

        var devCompanyId = Guid.Parse("01A1920B-BBBB-7000-8000-000000000001");
        if (!context.Companies.Any(c => c.Id == devCompanyId))
        {
            context.Companies.Add(new TalentSyncAI.Domain.Entities.Company
            {
                Id = devCompanyId,
                Name = "TechCorp Solutions",
                Industry = "Technology",
                Description = "Default company for local development and testing"
            });
        }

        var devRecruiterId = Guid.Parse("01A1920B-BB54-78EE-AA25-895CE7C1248C");
        if (!context.Users.Any(u => u.Id == devRecruiterId))
        {
            context.Users.Add(new TalentSyncAI.Domain.Entities.User
            {
                Id = devRecruiterId,
                Email = "sara.recruiter@techcorp.com",
                NormalizedEmail = "SARA.RECRUITER@TECHCORP.COM",
                PasswordHash = "DEV_MOCK_PASSWORD_HASH",
                FirstName = "Sara",
                LastName = "Recruiter",
                UserType = TalentSyncAI.Domain.Enums.UserType.Recruiter,
                IsActive = true
            });

            context.CompanyMembers.Add(new TalentSyncAI.Domain.Entities.CompanyMember
            {
                Id = Guid.NewGuid(),
                CompanyId = devCompanyId,
                UserId = devRecruiterId,
                Role = TalentSyncAI.Domain.Enums.CompanyMemberRole.Admin
            });
        }

        context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not apply database migrations on startup. If running without database, verify connection string.");
    }
}

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect("/swagger"));
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
