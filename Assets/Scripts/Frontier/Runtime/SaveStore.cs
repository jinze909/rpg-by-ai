using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Starfall
{
    [Serializable] public sealed class SaveEnvelope { public int format = 1; public string payload, checksum; }
    public static class SaveStore
    {
        public static string PathName { get { return PathFor(-1); } }
        public static string PathFor(int slot)
        { if (slot < -1 || slot > 2) throw new ArgumentOutOfRangeException("slot"); return Path.Combine(Application.persistentDataPath, slot < 0 ? "starfall-v1.json" : "starfall-slot-" + (slot + 1) + ".json"); }
        private static string Checksum(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
        private static SaveData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) throw new InvalidDataException("Invalid save JSON.");
            SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            if (envelope != null && envelope.payload != null)
            { if (envelope.format != 1 || envelope.checksum != Checksum(envelope.payload)) throw new InvalidDataException("Save checksum mismatch."); json = envelope.payload; }
            SaveData value = JsonUtility.FromJson<SaveData>(json);
            if (value == null || (value.version != 1 && value.version != 2) || value.inventory == null || value.inventory.Length != 8) throw new InvalidDataException("Invalid save schema.");
            return value;
        }
        public static SaveData Read(out string warning) { return Read(-1, out warning); }
        public static SaveData Read(int slot, out string warning)
        {
            warning = null; string main = PathFor(slot);
            foreach (string path in new[] { main, main + ".bak", main + ".tmp" })
            {
                if (!File.Exists(path)) continue;
                try { SaveData value = Decode(File.ReadAllText(path)); if (path != main) warning = "Recovered " + (path.EndsWith(".bak") ? "backup" : "interrupted save") + "."; return value; }
                catch (Exception ex) { warning = "Cannot read save: " + ex.Message; }
            }
            return null;
        }
        public static bool Exists(int slot) { string path = PathFor(slot); return File.Exists(path) || File.Exists(path + ".bak") || File.Exists(path + ".tmp"); }
        public static bool Write(SaveData value) { return Write(value, -1); }
        public static bool Write(SaveData value, int slot)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath); string path = PathFor(slot), temp = path + ".tmp";
                value.savedAtUtc = DateTime.UtcNow.ToString("o"); string payload = JsonUtility.ToJson(value);
                string json = JsonUtility.ToJson(new SaveEnvelope { payload = payload, checksum = Checksum(payload) }, true);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { byte[] bytes = Encoding.UTF8.GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                Decode(File.ReadAllText(temp));
                if (!File.Exists(path)) File.Move(temp, path);
                else
                {
                    bool validOld = true; try { Decode(File.ReadAllText(path)); } catch { validOld = false; }
                    string backup = validOld ? path + ".bak" : null;
                    try { File.Replace(temp, path, backup); }
                    catch (PlatformNotSupportedException)
                    { if (backup != null) File.Copy(path, backup, true); File.Delete(path); File.Move(temp, path); }
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("Starfall save failed: " + ex.Message); return false; }
        }
    }
}
