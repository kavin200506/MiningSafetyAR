using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MiningSafetyAR.Security
{
    /// <summary>
    /// Cryptographically secure password hashing and verification service using PBKDF2-HMAC-SHA256
    /// with per-user salt and constant-time hash comparison (OWASP standard compliant).
    /// </summary>
    public static class PasswordSecurityService
    {
        // 100,000 iterations of PBKDF2-SHA256 (equivalent to Bcrypt cost 12 / OWASP recommended standard)
        private const int IterationCount = 100000;
        private const int SaltSize = 16;       // 128-bit salt
        private const int HashSize = 32;       // 256-bit key

        /// <summary>
        /// Generates a cryptographically random salt.
        /// </summary>
        public static byte[] GenerateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        /// <summary>
        /// Hashes a password with a given salt using PBKDF2-HMAC-SHA256 (100,000 iterations).
        /// </summary>
        public static byte[] HashPassword(string password, byte[] salt)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (salt == null || salt.Length < SaltSize) throw new ArgumentException("Invalid salt length", nameof(salt));

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, IterationCount, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(HashSize);
        }

        /// <summary>
        /// Hashes a password and returns base64 encoded hash and salt strings.
        /// </summary>
        public static (string hashBase64, string saltBase64) HashAndSalt(string password)
        {
            byte[] salt = GenerateSalt();
            byte[] hash = HashPassword(password, salt);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        /// <summary>
        /// Verifies an input password against a stored base64 salt and hash using constant-time comparison.
        /// </summary>
        public static bool VerifyPassword(string inputPassword, string storedHashBase64, string storedSaltBase64)
        {
            if (string.IsNullOrEmpty(inputPassword) || string.IsNullOrEmpty(storedHashBase64) || string.IsNullOrEmpty(storedSaltBase64))
                return false;

            try
            {
                byte[] storedHash = Convert.FromBase64String(storedHashBase64);
                byte[] storedSalt = Convert.FromBase64String(storedSaltBase64);

                byte[] computedHash = HashPassword(inputPassword, storedSalt);

                return ConstantTimeEquals(storedHash, computedHash);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PasswordSecurity] Verification error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Constant-time byte array comparison to prevent timing attacks.
        /// </summary>
        public static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        /// <summary>
        /// Constant-time string comparison for legacy plain-text checks.
        /// </summary>
        public static bool ConstantTimeStringEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] bytesA = Encoding.UTF8.GetBytes(a);
            byte[] bytesB = Encoding.UTF8.GetBytes(b);
            return ConstantTimeEquals(bytesA, bytesB);
        }

        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        /// <summary>
        /// Checks if an account is currently locked out.
        /// </summary>
        public static bool IsLockedOut(string accountId, out int remainingSeconds)
        {
            remainingSeconds = 0;
            string lockoutKey = $"LockoutUntil_{accountId}";
            if (!PlayerPrefs.HasKey(lockoutKey)) return false;

            if (long.TryParse(PlayerPrefs.GetString(lockoutKey, "0"), out long lockoutUntil))
            {
                long currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentUnix < lockoutUntil)
                {
                    remainingSeconds = (int)(lockoutUntil - currentUnix);
                    return true;
                }
            }

            // Lockout period has elapsed — clear lock
            PlayerPrefs.DeleteKey(lockoutKey);
            PlayerPrefs.DeleteKey($"FailedAttempts_{accountId}");
            PlayerPrefs.Save();
            return false;
        }

        /// <summary>
        /// Records a failed login attempt, triggers lockout if >= 5 attempts,
        /// and calculates progressive exponential backoff delay (ms).
        /// </summary>
        public static int RecordFailedAttempt(string accountId, out bool isLockedNow)
        {
            isLockedNow = false;
            string attemptsKey = $"FailedAttempts_{accountId}";
            int attempts = PlayerPrefs.GetInt(attemptsKey, 0) + 1;
            PlayerPrefs.SetInt(attemptsKey, attempts);

            if (attempts >= MaxFailedAttempts)
            {
                long lockoutUntil = DateTimeOffset.UtcNow.AddMinutes(LockoutMinutes).ToUnixTimeSeconds();
                PlayerPrefs.SetString($"LockoutUntil_{accountId}", lockoutUntil.ToString());
                PlayerPrefs.Save();
                isLockedNow = true;
                return 0;
            }

            PlayerPrefs.Save();
            // Progressive exponential backoff delay: 500ms -> 1000ms -> 2000ms -> 4000ms
            return (int)(Math.Pow(2, attempts - 1) * 500);
        }

        /// <summary>
        /// Resets the failed attempt counter and lockout timer upon successful authentication.
        /// </summary>
        public static void ResetFailedAttempts(string accountId)
        {
            PlayerPrefs.DeleteKey($"FailedAttempts_{accountId}");
            PlayerPrefs.DeleteKey($"LockoutUntil_{accountId}");
            PlayerPrefs.Save();
        }
    }
}
