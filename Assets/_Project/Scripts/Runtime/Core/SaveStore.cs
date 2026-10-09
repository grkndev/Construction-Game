using System;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Reads and writes <see cref="SaveData"/> as JSON. The default location is under persistentDataPath
    /// (on WebGL that is IndexedDB-backed). Writes go to a temp file first so a crash mid-write cannot corrupt the save.
    /// </summary>
    public sealed class SaveStore
    {
        readonly string path;

        public SaveStore(string path) => this.path = path;

        public static SaveStore AtDefaultLocation() =>
            new SaveStore(Path.Combine(Application.persistentDataPath, "save.json"));

        public bool Exists => File.Exists(path);

        /// <summary>The saved game, or null when there is none or it cannot be read (corrupt / unknown version).</summary>
        public SaveData Load()
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data != null && data.version == SaveData.CurrentVersion ? data : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
                return null;
            }
        }

        public bool Save(SaveData data)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] Could not write {path}: {e.Message}");
                return false;
            }
        }

        public void Delete()
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] Could not delete {path}: {e.Message}");
            }
        }
    }
}
