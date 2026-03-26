using System.Security.Cryptography;
using System.Text;

namespace BIF.ToyStore.Infrastructure.Services
{
    public static class PasswordCipher
    {
        private const string Prefix = "aes:v1:";
        private const int NonceSize = 12;
        private const int TagSize = 16;

        public static string Encrypt(string plainText)
        {
            if (plainText is null)
            {
                throw new ArgumentNullException(nameof(plainText));
            }

            var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            var ciphertext = new byte[plaintextBytes.Length];
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];

            using var aes = new AesGcm(GetKey(), TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            var payload = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

            return Prefix + Convert.ToBase64String(payload);
        }

        public static bool TryDecrypt(string encryptedValue, out string plainText)
        {
            plainText = string.Empty;
            if (string.IsNullOrWhiteSpace(encryptedValue) || !encryptedValue.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var encodedPayload = encryptedValue[Prefix.Length..];
            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(encodedPayload);
            }
            catch (FormatException)
            {
                return false;
            }

            if (payload.Length < NonceSize + TagSize)
            {
                return false;
            }

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[payload.Length - NonceSize - TagSize];

            Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(payload, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(payload, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            var plaintextBytes = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(GetKey(), TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
            }
            catch (CryptographicException)
            {
                return false;
            }

            plainText = Encoding.UTF8.GetString(plaintextBytes);
            return true;
        }

        public static bool IsBcryptHash(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.StartsWith("$2a$", StringComparison.Ordinal)
                    || value.StartsWith("$2b$", StringComparison.Ordinal)
                    || value.StartsWith("$2x$", StringComparison.Ordinal)
                    || value.StartsWith("$2y$", StringComparison.Ordinal));
        }

        private static byte[] GetKey()
        {
            // Development fallback key. In production set TOYSTORE_AES_KEY.
            string secret = Environment.GetEnvironmentVariable("TOYSTORE_AES_KEY")
                ?? "BIF.ToyStore.DevOnly.Aes.Secret.v1";

            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }
    }
}
