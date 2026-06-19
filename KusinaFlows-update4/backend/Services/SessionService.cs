using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KusinaFlows.Services
{
    public class SessionInfo
    {
        public int SC_ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    // Minimal in-memory bearer-token store backing the auth middleware.
    // Good enough for a single-instance deployment; tokens are lost on app
    // restart (users simply have to log in again — no data is affected).
    public class SessionService
    {
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

        public string CreateSession(int scId, string username, string position)
        {
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            _sessions[token] = new SessionInfo
            {
                SC_ID = scId,
                Username = username,
                Position = position,
                CreatedAtUtc = DateTime.UtcNow
            };

            return token;
        }

        public bool TryGetSession(string token, out SessionInfo? session) =>
            _sessions.TryGetValue(token, out session);

        public void InvalidateSession(string token) => _sessions.TryRemove(token, out _);
    }
}
