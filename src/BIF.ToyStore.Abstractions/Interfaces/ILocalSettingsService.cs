namespace BIF.ToyStore.Core.Interfaces
{
    public interface ILocalSettingsService
    {
        void SetString(string key, string value);
        string GetString(string key, string defaultValue = "");
        void SetInt(string key, int value);
        int GetInt(string key, int defaultValue);
        void SetBool(string key, bool value);
        bool GetBool(string key, bool defaultValue);
    }
}
