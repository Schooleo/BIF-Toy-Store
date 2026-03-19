using BIF.ToyStore.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace BIF.ToyStore.WinUI.Services
{
    public class LocalSettingsService : ILocalSettingsService
    {
        private readonly object _syncRoot = new();
        private readonly string _fallbackFilePath;
        private readonly Dictionary<string, string> _fallbackValues;

        public LocalSettingsService()
        {
            var settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore");

            Directory.CreateDirectory(settingsDirectory);
            _fallbackFilePath = Path.Combine(settingsDirectory, "localsettings.json");
            _fallbackValues = LoadFallbackValues();
        }

        public void SetString(string key, string value)
        {
            if (TryGetWindowsSettings(out var settings))
            {
                settings.Values[key] = value;
                return;
            }

            lock (_syncRoot)
            {
                _fallbackValues[key] = value;
                SaveFallbackValues();
            }
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (TryGetWindowsSettings(out var settings))
            {
                return settings.Values[key] as string ?? defaultValue;
            }

            lock (_syncRoot)
            {
                return _fallbackValues.TryGetValue(key, out var value)
                    ? value
                    : defaultValue;
            }
        }

        public void SetInt(string key, int value)
        {
            if (TryGetWindowsSettings(out var settings))
            {
                settings.Values[key] = value;
                return;
            }

            lock (_syncRoot)
            {
                _fallbackValues[key] = value.ToString();
                SaveFallbackValues();
            }
        }

        public int GetInt(string key, int defaultValue)
        {
            if (TryGetWindowsSettings(out var settings))
            {
                var raw = settings.Values[key];
                if (raw is int value)
                {
                    return value;
                }

                if (raw is long longValue)
                {
                    return (int)longValue;
                }

                return defaultValue;
            }

            lock (_syncRoot)
            {
                if (_fallbackValues.TryGetValue(key, out var raw)
                    && int.TryParse(raw, out var parsed))
                {
                    return parsed;
                }

                return defaultValue;
            }
        }

        private static bool TryGetWindowsSettings(out ApplicationDataContainer settings)
        {
            try
            {
                settings = ApplicationData.Current.LocalSettings;
                return true;
            }
            catch (InvalidOperationException)
            {
                settings = null!;
                return false;
            }
        }

        private Dictionary<string, string> LoadFallbackValues()
        {
            try
            {
                if (!File.Exists(_fallbackFilePath))
                {
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }

                var json = File.ReadAllText(_fallbackFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private void SaveFallbackValues()
        {
            var json = JsonSerializer.Serialize(_fallbackValues, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_fallbackFilePath, json);
        }
    }
}
