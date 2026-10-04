using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ColonyGame.Profiles
{
    [Serializable]
    public sealed class Profile
    {
        public int saveFormatVersion = 1;
        public string id;
        public string name;
        public string createdUtc;
        public string lastPlayedUtc;
    }

    // File storage is independent of scenes and UI. The root is supplied by the application.
    public sealed class ProfileStore
    {
        public string Root { get; }
        public ProfileStore(string root) { Root = root; }

        public List<Profile> Load(out int skipped)
        {
            skipped = 0;
            var profiles = new List<Profile>();
            if (!Directory.Exists(Root)) return profiles;
            foreach (var directory in Directory.GetDirectories(Root))
            {
                var path = Path.Combine(directory, "profile.json");
                if (!File.Exists(path)) continue;
                try
                {
                    var profile = JsonUtility.FromJson<Profile>(File.ReadAllText(path));
                    if (profile == null || profile.saveFormatVersion != 1 ||
                        !Guid.TryParseExact(profile.id, "N", out _) ||
                        Path.GetFileName(directory) != profile.id || string.IsNullOrWhiteSpace(profile.name) ||
                        !DateTimeOffset.TryParse(profile.createdUtc, out _) ||
                        (!string.IsNullOrEmpty(profile.lastPlayedUtc) && !DateTimeOffset.TryParse(profile.lastPlayedUtc, out _)))
                        throw new InvalidDataException("Invalid or unsupported profile metadata.");
                    profiles.Add(profile);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                { skipped++; }
            }
            return profiles.OrderByDescending(p => string.IsNullOrEmpty(p.lastPlayedUtc) ? p.createdUtc : p.lastPlayedUtc).ToList();
        }

        public Profile Create(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80 || name.Any(char.IsControl))
                throw new ArgumentException("Enter a profile name of 1–80 characters without control characters.");
            var profile = new Profile { id = Guid.NewGuid().ToString("N"), name = name,
                createdUtc = DateTimeOffset.UtcNow.ToString("O"), lastPlayedUtc = "" };
            Write(profile);
            return profile;
        }

        public void MarkPlayed(Profile profile)
        {
            var previous = profile.lastPlayedUtc;
            profile.lastPlayedUtc = DateTimeOffset.UtcNow.ToString("O");
            try { Write(profile); }
            catch { profile.lastPlayedUtc = previous; throw; }
        }

        public void Delete(Profile profile)
        {
            var directory = DirectoryFor(profile);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private string DirectoryFor(Profile profile)
        {
            if (profile == null || !Guid.TryParseExact(profile.id, "N", out _))
                throw new ArgumentException("Invalid profile ID.");
            return Path.Combine(Root, profile.id);
        }

        private void Write(Profile profile)
        {
            var directory = DirectoryFor(profile);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "profile.json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(profile, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
