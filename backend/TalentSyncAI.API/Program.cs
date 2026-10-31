using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TalentSyncAI.API.Middleware;
using TalentSyncAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure Services (SQL Server, EF Core, Repositories, Security, Stubs)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Add HttpContextAccessor for user context resolution
builder.Services.AddHttpContextAccessor();

// 3. Add ProblemDetails & Global Exception Handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// 4. Add Authentication & JWT Bearer
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

// 5. Add Controllers
builder.Services.AddControllers();

// 6. Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Multi-Tenancy Middleware (resolves X-Company-Id for B2B requests)
app.UseMiddleware<TenantMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
