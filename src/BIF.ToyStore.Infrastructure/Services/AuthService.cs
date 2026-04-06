using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class AuthService(IAuthRepository authRepository) : IAuthService
    {
        private readonly IAuthRepository _authRepository = authRepository;

        public async Task<User?> LoginAsync(string username, string password)
        {
            var user = await _authRepository.FindByUsernameAsync(username);
            if (user is null)
            {
                return null;
            }

            if (PasswordCipher.TryDecrypt(user.PasswordHash, out var decryptedPassword))
            {
                return string.Equals(password, decryptedPassword, StringComparison.Ordinal) ? user : null;
            }

            if (PasswordCipher.IsBcryptHash(user.PasswordHash))
            {
                bool isValidLegacyHash;
                try
                {
                    isValidLegacyHash = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    isValidLegacyHash = false;
                }

                if (!isValidLegacyHash)
                {
                    return null;
                }

                user.PasswordHash = PasswordCipher.Encrypt(password);
                await _authRepository.SaveChangesAsync();
                return user;
            }

            // Backward compatibility for records that still store plain text.
            if (!string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
            {
                return null;
            }

            user.PasswordHash = PasswordCipher.Encrypt(password);
            await _authRepository.SaveChangesAsync();
            return user;
        }

        public Task LogoutAsync()
        {
            // Clear saved local settings/tokens
            return Task.CompletedTask;
        }
    }
}