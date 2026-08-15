using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AuraNova.Application.Auth.DTOs;
using AuraNova.Application.Auth.Interfaces;

namespace AuraNova.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("login")]
        [EnableRateLimiting("login_policy")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _auth.LoginAsync(request);
            if (result == null) return Unauthorized(new { message = "Credenciales inválidas." });

            return Ok(new
            {
                accessToken = result.AccessToken,
                tokenType = result.TokenType,
                expiresAt = result.ExpiresAt,
                user = result.User
            });
        }
    }
}