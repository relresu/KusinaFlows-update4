using Npgsql;
using Microsoft.Extensions.Configuration;

namespace KusinaFlows.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        // The constructor reads the appsettings.json file automatically
        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}