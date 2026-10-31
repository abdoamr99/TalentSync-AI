using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var existingUser = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            return BadRequest("A user with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            UserType = request.UserType,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true
        };

        // If Candidate (NextRole B2C), automatically create their independent workspace/profile
        if (request.UserType == UserType.Candidate)
        {
            var candidateProfile = new CandidateProfile
            {
                UserId = user.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Phone = request.PhoneNumber
            };

            _context.CandidateProfiles.Add(candidateProfile);
        }

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType.ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid email or password.");
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);
        user.LastLoginAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType.ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, cancellationToken);

        if (user == null || user.RefreshTokenExpiryUtc <= DateTime.UtcNow)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType.ToString(),
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
    }
}
