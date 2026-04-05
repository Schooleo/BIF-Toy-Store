using BIF.ToyStore.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Windows.Security.Credentials;

namespace BIF.ToyStore.WinUI.Services
{
    public class CredentialVaultService : ICredentialVaultService
    {
        public (string Username, string Password)? GetCredentials(string resourceName)
        {
            try
            {
                var vault = new PasswordVault();
                var credential = vault.RetrieveAll()
                    .FirstOrDefault(c => c.Resource == resourceName);

                if (credential is null)
                {
                    return null;
                }

                credential.RetrievePassword();
                return (credential.UserName, credential.Password);
            }
            catch
            {
                return null;
            }
        }

        public void SaveCredentials(string resourceName, string username, string password)
        {
            var vault = new PasswordVault();
            ClearCredentials(resourceName);
            vault.Add(new PasswordCredential(resourceName, username, password));
        }

        public void ClearCredentials(string resourceName)
        {
            var vault = new PasswordVault();
            IReadOnlyList<PasswordCredential> credentials;
            try
            {
                credentials = vault.RetrieveAll();
            }
            catch
            {
                return;
            }

            foreach (var credential in credentials.Where(c => c.Resource == resourceName))
            {
                vault.Remove(credential);
            }
        }
    }
}
