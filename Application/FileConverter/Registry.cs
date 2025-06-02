// <copyright file="Registry.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml.Serialization;

    public class Registry : IDisposable
    {
        private static Registry instance;

        private Dictionary<string, string> registryEntries = new Dictionary<string, string>();

        ~Registry()
        {
            this.Dispose();
            Registry.instance = null;
        }

        [XmlElement("Entry")]
        public Entry[] SerializableEntries
        {
            get
            {
                Entry[] entries = new Entry[this.registryEntries.Count];
                int index = 0;
                foreach (KeyValuePair<string, string> kvp in this.registryEntries)
                {
                    if (kvp.Value == null)
                    {
                        continue;
                    }

                    entries[index] = new Entry(kvp.Key, kvp.Value);
                    index++;
                }

                return entries;
            }

            set
            {
                if (value == null)
                {
                    return;
                }

                this.registryEntries.Clear();
                for (int index = 0; index < value.Length; index++)
                {
                    Entry entry = value[index];
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    if (this.registryEntries.ContainsKey(entry.Key))
                    {
                        Diagnostics.Debug.Log($"Ignore registry entry {entry.Key}.");
                        continue;
                    }

                    this.registryEntries.Add(entry.Key, entry.Value);
                }
            }
        }

        private static Registry Instance
        {
            get
            {
                if (Registry.instance == null)
                {
                    Registry.Load();
                }

                return Registry.instance;
            }
        }

        private const string AppName = "FileConverter"; // Application name for settings directory

        private static string GetUserRegistryFilePath
        {
            get
            {
                // Use Environment.SpecialFolder.ApplicationData for cross-platform application settings.
                // This typically maps to:
                // Windows: C:\Users\<User>\AppData\Roaming
                // macOS: /Users/<User>/.config (or sometimes ~/Library/Application Support)
                // Linux: /home/<User>/.config
                string appDataBasePaath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appSpecificSettingsDir = Path.Combine(appDataBasePaath, AppName);
                return Path.Combine(appSpecificSettingsDir, "Registry.xml");
            }
        }

        public static T GetValue<T>(string key, T defaultValue = default(T))
        {
            Registry registry = Registry.Instance;

            if (!registry.registryEntries.TryGetValue(key, out string stringValue))
            {
                return defaultValue;
            }

            try
            {
                object value = System.Convert.ChangeType(stringValue, typeof(T));
                return (T)value;
            }
            catch (Exception exception)
            {
                Diagnostics.Debug.LogError($"Can't convert registry value: {exception.Message}.");
            }

            return defaultValue;
        }

        public static void SetValue<T>(string key, T value)
        {
            Registry registry = Registry.Instance;

            if (!registry.registryEntries.ContainsKey(key))
            {
                registry.registryEntries.Add(key, null);
            }

            try
            {
                string stringValue = (string)System.Convert.ChangeType(value, typeof(string));
                registry.registryEntries[key] = stringValue;
            }
            catch (Exception exception)
            {
                Diagnostics.Debug.LogError($"Can't convert registry value: {exception.Message}.");
            }
        }

        public void Dispose()
        {
            // SAVE
            string registryFilePath = Registry.GetUserRegistryFilePath;

            try
            {
                // Ensure the directory exists before saving.
                string directoryPath = Path.GetDirectoryName(registryFilePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                XmlHelpers.SaveToFile("Registry", registryFilePath, this);
            }
            catch (Exception exception)
            {
                // Log detailed error, including path if possible (be careful with PII).
                Diagnostics.Debug.LogError($"Fail to save registry to '{registryFilePath}'. Exception: {exception.Message}");
            }
        }
        
        private static void Load()
        {
            string registryFilePath = Registry.GetUserRegistryFilePath;
            if (!File.Exists(registryFilePath))
            {
                Registry.instance = new Registry();
                return;
            }

            try
            {
                Registry.instance = null;
                XmlHelpers.LoadFromFile<Registry>("Registry", registryFilePath, out Registry.instance);
            }
            catch (Exception exception)
            {
                Registry.instance = new Registry();
                Diagnostics.Debug.LogError($"Fail to load registry. {exception.Message}");
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                    Diagnostics.Debug.LogError($"Inner exception: {exception.Message}");
                }
            }
        }

        [XmlRoot("Entry")]
        public struct Entry
        {
            public Entry(string key, string value)
            {
                this.Key = key;
                this.Value = value;
            }

            [XmlAttribute]
            public string Key
            {
                get;
                set;
            }

            [XmlText]
            public string Value
            {
                get;
                set;
            }
        }

        public static class Keys
        {
            public static readonly string LastUpdateCheckDate = "LastUpdateCheckDate";
            public static readonly string ImportInitialFolder = "ImportInitialFolder";
        }
    }
}
