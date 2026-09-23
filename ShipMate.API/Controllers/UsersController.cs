using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Users;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IUserService userService, ICurrentUserService currentUserService)
    {
        _userService = userService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var result = await _userService.GetProfileAsync(_currentUserService.UserId!.Value);
        return Ok(result);
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var result = await _userService.UpdateProfileAsync(_currentUserService.UserId!.Value, request);
        return Ok(result);
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await _userService.ChangePasswordAsync(_currentUserService.UserId!.Value, request);
        return Ok(new { message = "Password changed successfully. Please log in again." });
    }
}
