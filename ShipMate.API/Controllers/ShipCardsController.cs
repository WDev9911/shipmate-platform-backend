using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

/// <summary>Ship Cards a team made public. Anyone with the link can read one and check it wasn't altered.</summary>
[ApiController]
[Route("api/ship-cards")]
[AllowAnonymous]
public class ShipCardsController : ControllerBase
{
    private readonly IShipService _shipService;

    public ShipCardsController(IShipService shipService)
    {
        _shipService = shipService;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var result = await _shipService.GetPublicCardAsync(slug);
        return Ok(result);
    }

    [HttpGet("{slug}/verify")]
    public async Task<IActionResult> Verify(string slug)
    {
        var result = await _shipService.VerifyPublicCardAsync(slug);
        return Ok(result);
    }
}
