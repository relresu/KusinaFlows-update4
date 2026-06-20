using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using KusinaFlows.Models;
using KusinaFlows.Services;

namespace KusinaFlows.Repositories
{
    // Concrete Postgres implementation backing both StaffController and
    // AuthController — both work against the same STOCK CONTROLLER table, so
    // one repository covers both rather than duplicating the connection/SQL
    // boilerplate across two classes.
    public class StaffRepository : IStaffRepository
    {
        private readonly DatabaseService _dbService;

        public StaffRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task<List<StaffDto>> GetAllAsync()
        {
            var staffList = new List<StaffDto>();

            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            string sql = @"
                SELECT ""SC_ID"", ""FirstName"", ""MI"", ""LastName"", ""Position"",
                       ""Username"", ""ContactInfo"", ""ProfilePicture"", ""DateHired"",
                       ""LastLogin"", ""Active""
                FROM public.""STOCK CONTROLLER""
                ORDER BY ""SC_ID"" ASC;";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
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

            return staffList;
        }

        public async Task<StaffDto?> FindByUsernameAsync(string username)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                SELECT ""SC_ID"", ""Username"", ""Password"", ""FirstName"", ""LastName"", ""Position"", ""Active""
                FROM public.""STOCK CONTROLLER""
                WHERE LOWER(""Username"") = LOWER(@Username);", conn);
            cmd.Parameters.AddWithValue("@Username", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new StaffDto
            {
                SC_ID = reader.GetInt32(0),
                Username = reader.GetString(1),
                Password = reader.GetString(2),
                FirstName = reader.GetString(3),
                LastName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Position = reader.IsDBNull(5) ? "Staff" : reader.GetString(5),
                Active = reader.IsDBNull(6) ? true : reader.GetBoolean(6)
            };
        }

        public async Task<bool> UsernameTakenAsync(string username)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT COUNT(1) FROM public.""STOCK CONTROLLER"" WHERE LOWER(""Username"") = LOWER(@Username);", conn);
            cmd.Parameters.AddWithValue("@Username", username);
            long count = (await cmd.ExecuteScalarAsync() as long?) ?? 0;
            return count > 0;
        }

        public async Task<string?> GetPositionAsync(int scId)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT ""Position"" FROM public.""STOCK CONTROLLER"" WHERE ""SC_ID""=@Id;", conn);
            cmd.Parameters.AddWithValue("@Id", scId);
            return await cmd.ExecuteScalarAsync() as string;
        }

        public async Task<int> CreateAsync(StaffDto payload, string hashedPassword)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            // MI, ContactInfo, and ProfilePicture are NOT NULL columns in
            // STOCK CONTROLLER even though they're optional in the UI — default to
            // empty string instead of DBNull so an omitted value never 500s here.
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO public.""STOCK CONTROLLER""
                (""FirstName"", ""MI"", ""LastName"", ""Position"", ""Username"", ""Password"", ""ContactInfo"", ""ProfilePicture"", ""DateHired"", ""LastLogin"", ""Active"")
                VALUES (@FirstName, @MI, @LastName, @Position, @Username, @Password, @ContactInfo, @ProfilePicture, @DateHired, @LastLogin, @Active)
                RETURNING ""SC_ID"";", conn);

            cmd.Parameters.AddWithValue("@FirstName", payload.FirstName ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@MI", payload.MI ?? "");
            cmd.Parameters.AddWithValue("@LastName", payload.LastName ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@Position", payload.Position ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@Username", payload.Username ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", hashedPassword);
            cmd.Parameters.AddWithValue("@ContactInfo", payload.ContactInfo ?? "");
            cmd.Parameters.AddWithValue("@ProfilePicture", payload.ProfilePicture ?? "");
            cmd.Parameters.AddWithValue("@DateHired", payload.DateHired ?? System.DateTime.Now.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@LastLogin", payload.LastLogin ?? "-");
            cmd.Parameters.AddWithValue("@Active", payload.Active);

            return (await cmd.ExecuteScalarAsync() as int?) ?? 0;
        }

        public async Task<bool> UpdateAsync(int scId, StaffDto payload, string? hashedPassword)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            bool hasNewPassword = hashedPassword != null;
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

            using var cmd = new NpgsqlCommand(updateSql, conn);
            cmd.Parameters.AddWithValue("@FirstName", payload.FirstName ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@MI", payload.MI ?? "");
            cmd.Parameters.AddWithValue("@LastName", payload.LastName ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@Position", payload.Position ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@Username", payload.Username ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ContactInfo", payload.ContactInfo ?? "");
            cmd.Parameters.AddWithValue("@ProfilePicture", payload.ProfilePicture ?? "");
            cmd.Parameters.AddWithValue("@Active", payload.Active);
            cmd.Parameters.AddWithValue("@SC_ID", scId);

            if (hasNewPassword)
            {
                cmd.Parameters.AddWithValue("@Password", hashedPassword!);
            }

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task UpdatePasswordAsync(int scId, string hashedPassword)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"UPDATE public.""STOCK CONTROLLER"" SET ""Password""=@P WHERE ""SC_ID""=@Id;", conn);
            cmd.Parameters.AddWithValue("@P", hashedPassword);
            cmd.Parameters.AddWithValue("@Id", scId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> UpdateLastLoginAsync(string username, string timestamp)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                UPDATE public.""STOCK CONTROLLER""
                SET ""LastLogin"" = @LastLogin
                WHERE LOWER(""Username"") = LOWER(@Username);", conn);
            cmd.Parameters.AddWithValue("@LastLogin", timestamp);
            cmd.Parameters.AddWithValue("@Username", username);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}
