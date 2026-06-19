using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using KusinaFlows.Services;
using KusinaFlows.Models;
using System;
using System.Collections.Generic;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Routes to: api/staff
    [Authorize] // Staff Management is restricted to Manager/Owner — enforced per-action below
    public class StaffController : ControllerBase
    {
        private readonly DatabaseService _dbService;

        public StaffController(DatabaseService dbService)
        {
            _dbService = dbService;
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
        public IActionResult GetAllStaff()
        {
            var staffList = new List<StaffDto>();

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT ""SC_ID"", ""FirstName"", ""MI"", ""LastName"", ""Position"",
                               ""Username"", ""ContactInfo"", ""ProfilePicture"", ""DateHired"",
                               ""LastLogin"", ""Active""
                        FROM public.""STOCK CONTROLLER""
                        ORDER BY ""SC_ID"" ASC;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            staffList.Add(new StaffDto
                            {
                                SC_ID = reader.GetInt32(0),
                                FirstName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                MI = reader.IsDBNull(2) ? null : reader.GetString(2),
                                LastName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Position = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Username = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ContactInfo = reader.IsDBNull(6) ? null : reader.GetString(6),
                                ProfilePicture = reader.IsDBNull(7) ? null : reader.GetString(7),
                                DateHired = reader.IsDBNull(8) ? null : reader.GetString(8),
                                LastLogin = reader.IsDBNull(9) ? null : reader.GetString(9),
                                Active = reader.IsDBNull(10) ? false : reader.GetBoolean(10)
                            });
                        }
                    }
                }
                return Ok(staffList);
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
        public IActionResult CreateStaff([FromBody] StaffDto payload)
        {
            if (!CallerIsManagerOrOwner)
                return Forbid();

            if (payload == null) return BadRequest(new { message = "Invalid data format payload." });

            // Managers can only create Staff-level accounts (mirrors the frontend's
            // role dropdown restriction, enforced here so it can't be bypassed by
            // calling the API directly).
            if (CallerPosition == "Manager" && payload.Position != "Staff")
                return Forbid();

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();

                    // Check unique username constraint first
                    string checkSql = @"SELECT COUNT(1) FROM public.""STOCK CONTROLLER"" WHERE LOWER(""Username"") = LOWER(@Username);";
                    using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", payload.Username ?? "");
                        long count = (checkCmd.ExecuteScalar() as long?) ?? 0;
                        if (count > 0) return BadRequest("Username is already taken by another staff member.");
                    }

                    // Insert query matching your schema sequence layout
                    string insertSql = @"
                        INSERT INTO public.""STOCK CONTROLLER""
                        (""FirstName"", ""MI"", ""LastName"", ""Position"", ""Username"", ""Password"", ""ContactInfo"", ""ProfilePicture"", ""DateHired"", ""LastLogin"", ""Active"")
                        VALUES (@FirstName, @MI, @LastName, @Position, @Username, @Password, @ContactInfo, @ProfilePicture, @DateHired, @LastLogin, @Active)
                        RETURNING ""SC_ID"";";

                    using (var cmd = new NpgsqlCommand(insertSql, conn))
                    {
                        // NOTE: MI, ContactInfo, and ProfilePicture are NOT NULL columns in
                        // STOCK CONTROLLER even though they're optional in the UI — default to
                        // empty string instead of DBNull so an omitted value never 500s here.
                        cmd.Parameters.AddWithValue("@FirstName", payload.FirstName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MI", payload.MI ?? "");
                        cmd.Parameters.AddWithValue("@LastName", payload.LastName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Position", payload.Position ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Username", payload.Username ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Password", PasswordHasher.Hash(payload.Password ?? "password123"));
                        cmd.Parameters.AddWithValue("@ContactInfo", payload.ContactInfo ?? "");
                        cmd.Parameters.AddWithValue("@ProfilePicture", payload.ProfilePicture ?? "");
                        cmd.Parameters.AddWithValue("@DateHired", payload.DateHired ?? DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@LastLogin", payload.LastLogin ?? "-");
                        cmd.Parameters.AddWithValue("@Active", payload.Active);

                        // Safely cast to a nullable int, falling back to 0 if the database returns null
                        payload.SC_ID = (cmd.ExecuteScalar() as int?) ?? 0;
                    }
                }
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
        public IActionResult UpdateStaff(int id, [FromBody] StaffDto payload)
        {
            if (!CallerIsManagerOrOwner)
                return Forbid();

            if (payload == null) return BadRequest(new { message = "Invalid data transaction format." });

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();

                    // Managers cannot touch Manager/Owner accounts — neither the existing
                    // record nor what the payload is trying to turn it into. Owners are
                    // unrestricted. This mirrors the frontend's Edit-button guard, now
                    // enforced server-side so it can't be bypassed via direct API calls.
                    if (CallerPosition == "Manager")
                    {
                        string? currentPosition = null;
                        using (var sel = new NpgsqlCommand(
                            @"SELECT ""Position"" FROM public.""STOCK CONTROLLER"" WHERE ""SC_ID""=@Id;", conn))
                        {
                            sel.Parameters.AddWithValue("@Id", id);
                            currentPosition = sel.ExecuteScalar() as string;
                        }

                        bool targetIsPrivileged = currentPosition == "Manager" || currentPosition == "Owner";
                        bool payloadEscalates = payload.Position == "Manager" || payload.Position == "Owner";

                        if (targetIsPrivileged || payloadEscalates)
                            return Forbid();
                    }

                    // Dynamically build string parameters depending on if password was updated
                    bool hasNewPassword = !string.IsNullOrWhiteSpace(payload.Password);
                    string updateSql = @"
                        UPDATE public.""STOCK CONTROLLER""
                        SET ""FirstName"" = @FirstName,
                            ""MI"" = @MI,
                            ""LastName"" = @LastName,
                            ""Position"" = @Position,
                            ""Username"" = @Username,
                            ""ContactInfo"" = @ContactInfo,
                            ""ProfilePicture"" = @ProfilePicture,
                            ""Active"" = @Active"
                            + (hasNewPassword ? @", ""Password"" = @Password " : " ") +
                        @"WHERE ""SC_ID"" = @SC_ID;";

                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FirstName", payload.FirstName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MI", payload.MI ?? "");
                        cmd.Parameters.AddWithValue("@LastName", payload.LastName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Position", payload.Position ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Username", payload.Username ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ContactInfo", payload.ContactInfo ?? "");
                        cmd.Parameters.AddWithValue("@ProfilePicture", payload.ProfilePicture ?? "");
                        cmd.Parameters.AddWithValue("@Active", payload.Active);
                        cmd.Parameters.AddWithValue("@SC_ID", id);

                        if (hasNewPassword)
                        {
                            cmd.Parameters.AddWithValue("@Password", PasswordHasher.Hash(payload.Password!));
                        }

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0) return NotFound("Staff profile configuration context row not found.");
                    }
                }
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
        public IActionResult UpdateStaffLoginTime([FromBody] StaffDto staff)
        {
            if (staff == null || string.IsNullOrEmpty(staff.Username))
                return BadRequest(new { message = "Invalid staff profile authentication telemetry layout." });

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();

                    // Generate synchronized Philippine Standard Time (GMT+8) coordinates
                    DateTime utcNow = DateTime.UtcNow;
                    TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, phTimeZone);
                    string currentTimestamp = phTime.ToString("yyyy-MM-dd HH:mm:ss");

                    string updateQuery = @"
                        UPDATE public.""STOCK CONTROLLER""
                        SET ""LastLogin"" = @LastLogin
                        WHERE LOWER(""Username"") = LOWER(@Username);";

                    using (var cmd = new NpgsqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@LastLogin", currentTimestamp);
                        cmd.Parameters.AddWithValue("@Username", staff.Username);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0) return NotFound(new { message = "Target staff login credentials record not tracked." });
                    }
                }

                return Ok(new { message = "Login audit timestamp updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to synchronize session timestamp properties.", error = ex.Message });
            }
        }
    }
}
