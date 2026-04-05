namespace BIF.ToyStore.Core.Interfaces
{
    public interface ICredentialVaultService
    {
        (string Username, string Password)? GetCredentials(string resourceName);
        void SaveCredentials(string resourceName, string username, string password);
        void ClearCredentials(string resourceName);
    }
}
