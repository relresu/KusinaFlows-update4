using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KusinaFlows.Services;
using KusinaFlows.Repositories;
using System;
using System.Threading.Tasks;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IStaffRepository _repo;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(IStaffRepository repo, JwtTokenService jwtTokenService)
        {
            _repo = repo;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "All login fields are required." });
            }

            var staff = await _repo.FindByUsernameAsync(request.Username);

            if (staff == null)
            {
                return NotFound(new { message = "Unknown Username" });
            }

            // Evaluate Password Match Integrity (hashed passwords, with a transparent
            // upgrade path for any legacy plain-text rows still in the database)
            if (!PasswordHasher.Verify(request.Password, staff.Password!))
            {
                return Unauthorized(new { message = "Wrong Password" });
            }

            if (!PasswordHasher.IsHashed(staff.Password))
            {
                await UpgradeLegacyPasswordAsync(staff.SC_ID, request.Password);
            }

            // 🔒 SECURITY INTERCEPT LAYER: Block access if administrative status is false
            if (!staff.Active)
            {
                return StatusCode(403, new { message = "Can't Logged in" });
            }

            string token = _jwtTokenService.GenerateToken(staff.SC_ID, staff.Username!, staff.Position ?? "Staff");

            // Authenticated successfully! Compile the payload profile object
            return Ok(new {
                status = "Success",
                message = $"Maligayang pagbabalik, {staff.FirstName}!",
                token = token,
                user = new {
                    userId   = staff.SC_ID, // kept for backward compatibility
                    SC_ID    = staff.SC_ID, // explicit FK field used by STOCK HISTORY
                    username = staff.Username,
                    firstName = staff.FirstName,
                    lastName  = staff.LastName,
                    position  = staff.Position,
                    active    = staff.Active
                }
            });
        }

        // ============================================================================
        // POST api/auth/logout
        // JWTs are stateless — there's no server-side session to invalidate, so
        // logging out is just the client discarding its token. This endpoint
        // exists purely so the frontend has a symmetrical call to make.
        // ============================================================================
        [HttpPost("logout")]
        public IActionResult Logout() => Ok(new { message = "Logged out." });

        private async Task UpgradeLegacyPasswordAsync(int scId, string plainTextPassword)
        {
            try
            {
                await _repo.UpdatePasswordAsync(scId, PasswordHasher.Hash(plainTextPassword));
            }
            catch
            {
                // Non-fatal — login already succeeded; the row simply stays plain-text
                // and will be retried on the next successful login.
            }
        }

        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}
