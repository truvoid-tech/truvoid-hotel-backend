using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new institution with an admin user. Returns JWT tokens.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.RegisterAsync(request, ct);
            return CreatedAtAction(nameof(GetProfile), null, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Login with email and password. Returns JWT tokens and user profile.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.LoginAsync(request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse
            {
                Code = ErrorCodes.Unauthorized,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Refresh an expired access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse
            {
                Code = ErrorCodes.Unauthorized,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Logout — revokes the refresh token.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>
    /// Get the current user's profile.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await _authService.GetUserProfileAsync(userId, ct);

        if (profile is null)
            return NotFound(new ErrorResponse
            {
                Code = ErrorCodes.NotFound,
                Message = "User profile not found."
            });

        return Ok(profile);
    }

    /// <summary>
    /// Request a password reset email.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        // Always return OK to prevent email enumeration
        await _authService.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { message = "If an account exists with that email, a reset link has been sent." });
    }

    /// <summary>
    /// Reset password using a valid reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _authService.ResetPasswordAsync(request, ct);
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Change password (requires current password).
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            await _authService.ChangePasswordAsync(userId, request, ct);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.Unauthorized,
                Message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("No user context found.");
        return Guid.Parse(claim.Value);
    }
}
