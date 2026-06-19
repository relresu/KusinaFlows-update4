using Microsoft.AspNetCore.Mvc;
using Npgsql;
using KusinaFlows.Services;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly DatabaseService _dbService;

        // Update the constructor name to match your class
        public TestController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        [HttpGet("connect")]
        public IActionResult TestConnection()
        {
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT version();", conn))
                    {
                        var version = cmd.ExecuteScalar()?.ToString();
                        return Ok(new {
                            status = "Success",
                            message = "Successfully connected to Neon PostgreSQL!",
                            databaseVersion = version
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    status = "Error",
                    message = "Connection failed!",
                    errorDetails = ex.Message
                });
            }
        }
    }
}
