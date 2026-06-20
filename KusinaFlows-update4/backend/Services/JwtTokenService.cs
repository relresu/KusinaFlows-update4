using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KusinaFlows.Services
{
    // Issues signed JWTs for authenticated sessions. Stateless by design — there's
    // no server-side session store, so tokens remain valid until they expire
    // (configurable via Jwt:ExpiryHours). "Logout" is purely a client-side
    // action (discard the token); that's the standard, expected tradeoff for
    // JWT auth and is appropriate for this app's scale.
    public class JwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(int scId, string username, string position)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, scId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim("position", position)
            };

            double expiryHours = double.TryParse(_config["Jwt:ExpiryHours"], out var hrs) ? hrs : 12;

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
