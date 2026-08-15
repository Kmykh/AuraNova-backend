using Microsoft.AspNetCore.Mvc;
using AuraNova.Infrastructure.Persistence;
using System.Threading.Tasks;

namespace AuraNova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "ok",
            service = "AuraNova API"
        });
    }

    [HttpGet("database")]
    public async Task<IActionResult> GetDatabase()
    {
        var canConnect = await _db.Database.CanConnectAsync();
        if (canConnect)
            return Ok(new { status = "ok", database = "connected" });

        return StatusCode(503, new { status = "error", database = "disconnected" });
    }
}
