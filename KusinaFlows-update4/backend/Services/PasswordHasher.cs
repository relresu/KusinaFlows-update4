using System;
using System.Security.Cryptography;

namespace KusinaFlows.Services
{
    // PBKDF2-based password hashing (no extra NuGet dependency needed — Rfc2898DeriveBytes
    // ships with .NET). Stored format: "{iterations}.{saltBase64}.{hashBase64}".
    // Plain-text legacy passwords (rows created before this was introduced) don't match
    // that format, so Verify() falls back to a direct comparison for them — the caller
    // is then expected to re-hash and persist the password on that successful login.
    public static class PasswordHasher
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool IsHashed(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.Split('.').Length == 3 && int.TryParse(stored.Split('.')[0], out _);

        // Returns true if the supplied plain-text password matches what's stored,
        // whether stored is a PBKDF2 hash or (for legacy rows) plain text.
        public static bool Verify(string suppliedPassword, string storedPassword)
        {
            if (!IsHashed(storedPassword))
            {
                return suppliedPassword == storedPassword; // legacy plaintext row
            }

            var parts = storedPassword.Split('.');
            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(suppliedPassword, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
