using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Common.Models;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var existingUser = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            UserType = request.UserType,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true
        };

        if (request.UserType == UserType.Candidate)
        {
            var candidateProfile = new CandidateProfile
            {
                UserId = user.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim(),
                Phone = request.PhoneNumber?.Trim()
            };

            _context.CandidateProfiles.Add(candidateProfile);
        }

        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        user.RefreshToken = HashRefreshToken(rawRefreshToken);
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        Guid? companyId = null;
        if (user.UserType == UserType.Recruiter)
        {
            var membership = await _context.CompanyMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cm => cm.UserId == user.Id, cancellationToken);
            companyId = membership?.CompanyId;
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user, companyId);
        return BuildAuthResponse(user, accessToken, rawRefreshToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        Guid? companyId = null;
        if (user.UserType == UserType.Recruiter)
        {
            var membership = await _context.CompanyMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cm => cm.UserId == user.Id, cancellationToken);
            companyId = membership?.CompanyId;
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user, companyId);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshToken = HashRefreshToken(rawRefreshToken);
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);
        user.LastLoginAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, accessToken, rawRefreshToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hashedIncomingToken = HashRefreshToken(request.RefreshToken.Trim());

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == hashedIncomingToken, cancellationToken);

        if (user == null || user.RefreshTokenExpiryUtc == null || user.RefreshTokenExpiryUtc <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        Guid? companyId = null;
        if (user.UserType == UserType.Recruiter)
        {
            var membership = await _context.CompanyMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cm => cm.UserId == user.Id, cancellationToken);
            companyId = membership?.CompanyId;
        }

        // Rotate refresh token
        var newRawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        user.RefreshToken = HashRefreshToken(newRawRefreshToken);
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, companyId);
        return BuildAuthResponse(user, accessToken, newRawRefreshToken);
    }

    private AuthResponse BuildAuthResponse(User user, string accessToken, string rawRefreshToken)
    {
        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType.ToString(),
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes)
        };
    }

    private static string HashRefreshToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
