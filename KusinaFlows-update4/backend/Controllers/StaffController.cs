using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KusinaFlows.Services;
using KusinaFlows.Models;
using KusinaFlows.Repositories;
using System;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Routes to: api/staff
    [Authorize] // Staff Management is restricted to Manager/Owner — enforced per-action below
    public class StaffController : ControllerBase
    {
        private readonly IStaffRepository _repo;

        public StaffController(IStaffRepository repo)
        {
            _repo = repo;
        }

        // Position carried in the caller's JWT (see middleware/JwtTokenService.cs)
        private string CallerPosition => User.FindFirst("position")?.Value ?? "Staff";
        private bool CallerIsManagerOrOwner => CallerPosition == "Manager" || CallerPosition == "Owner";

        // ============================================================================
        // GET: api/staff (Fetch all users)
        // Intentionally open to any authenticated role, not just Manager/Owner —
        // Stock-In/Out/Edit forms and the Stock History filter all depend on this
        // list (e.g. to populate the "Approved By" dropdown), and those are used
        // by Staff-level accounts too. The Staff Management *page* itself is what
        // nav.js hides from Staff; the write actions below remain locked down.
        // ============================================================================
        [HttpGet]
        public async Task<IActionResult> GetAllStaff()
        {
            try
            {
                return Ok(await _repo.GetAllAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling staff records.", error = ex.Message });
            }
        }

        // ============================================================================
        // POST: api/staff (Create new record)
        // ============================================================================
        [HttpPost]
        public async Task<IActionResult> CreateStaff([FromBody] StaffDto payload)
        {
            if (!CallerIsManagerOrOwner)
                return Forbid();

            if (payload == null) return BadRequest(new { message = "Invalid data format payload." });

            // ENCAPSULATION: name-format rules live on StaffDto itself (see
            // Models/StaffDto.cs) — the controller just asks "is this valid?"
            if (!payload.IsValid(out string nameError))
                return BadRequest(new { message = nameError });

            // Managers can only create Staff-level accounts (mirrors the frontend's
            // role dropdown restriction, enforced here so it can't be bypassed by
            // calling the API directly).
            if (CallerPosition == "Manager" && payload.Position != "Staff")
                return Forbid();

            try
            {
                if (await _repo.UsernameTakenAsync(payload.Username ?? ""))
                    return BadRequest("Username is already taken by another staff member.");

                payload.SC_ID = await _repo.CreateAsync(payload, PasswordHasher.Hash(payload.Password ?? "password123"));
                return Ok(payload);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database registration operation failed.", error = ex.Message });
            }
        }

        // ============================================================================
        // PUT: api/staff/{id} (Update details / change status flags / reset password)
        // ============================================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStaff(int id, [FromBody] StaffDto payload)
        {
            if (!CallerIsManagerOrOwner)
                return Forbid();

            if (payload == null) return BadRequest(new { message = "Invalid data transaction format." });

            if (!payload.IsValid(out string nameError))
                return BadRequest(new { message = nameError });

            try
            {
                // Managers cannot touch Manager/Owner accounts — neither the existing
                // record nor what the payload is trying to turn it into. Owners are
                // unrestricted. This mirrors the frontend's Edit-button guard, now
                // enforced server-side so it can't be bypassed via direct API calls.
                if (CallerPosition == "Manager")
                {
                    string? currentPosition = await _repo.GetPositionAsync(id);
                    bool targetIsPrivileged = currentPosition == "Manager" || currentPosition == "Owner";
                    bool payloadEscalates = payload.Position == "Manager" || payload.Position == "Owner";

                    if (targetIsPrivileged || payloadEscalates)
                        return Forbid();
                }

                bool hasNewPassword = !string.IsNullOrWhiteSpace(payload.Password);
                string? hashedPassword = hasNewPassword ? PasswordHasher.Hash(payload.Password!) : null;

                bool updated = await _repo.UpdateAsync(id, payload, hashedPassword);
                if (!updated) return NotFound("Staff profile configuration context row not found.");

                return Ok(payload);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database record update operations failed.", error = ex.Message });
            }
        }

        // ============================================================================
        // POST: api/staff/update-login-time (Save modern audit session stamp)
        // ============================================================================
        [HttpPost("update-login-time")]
        public async Task<IActionResult> UpdateStaffLoginTime([FromBody] StaffDto staff)
        {
            if (staff == null || string.IsNullOrEmpty(staff.Username))
                return BadRequest(new { message = "Invalid staff profile authentication telemetry layout." });

            try
            {
                DateTime utcNow = DateTime.UtcNow;
                TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, phTimeZone);
                string currentTimestamp = phTime.ToString("yyyy-MM-dd HH:mm:ss");

                bool updated = await _repo.UpdateLastLoginAsync(staff.Username, currentTimestamp);
                if (!updated) return NotFound(new { message = "Target staff login credentials record not tracked." });

                return Ok(new { message = "Login audit timestamp updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to synchronize session timestamp properties.", error = ex.Message });
            }
        }
    }
}
