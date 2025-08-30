using HarmonyLib;
using ModLoader;
using UnityEngine;
using System.IO;
using SFS.IO;
using System.Collections.Generic;
using SFS.UI;
using SFS.Input;
using System;
using SFS.Parsers.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using SFS.World;

namespace replay
{
    public class Main : Mod
    {
        public static Main Instance { get; private set; }

        public override string ModNameID => "replayMod";
        public override string DisplayName => "Replay Mod";
        public override string Author => "Cratior";
        public override string MinimumGameVersionNecessary => "1.5.10";
        public override string ModVersion => "v1.0.0";
        public override string Description => "Records SFS Gameplay and them shows the recordings in game with camera controls.";

        public override void Early_Load()
        {
            Instance = this;
            new Harmony(ModNameID).PatchAll();
            Settings.EnsureRecordingsFolderExists();
            Settings.LoadSettings();
            Debug.Log("Replay Mod patches applied in Early_Load");
        }

        public override void Load()
        {
            MainMenuUI.HomeManagerAlert();
            Settings.LoadSettings();
        }
    }


    public static class Settings
    {
        private static FilePath SettingsFilePath => new FolderPath(Main.Instance.ModFolder).ExtendToFile("replaymod-settings.json");
        public static FolderPath RecordingsFolderPath = SavingFolder.Extend("/Recordings");

        public static ReplaySettings CurrentSettings { get; private set; } = new ReplaySettings();

        public static void EnsureRecordingsFolderExists()
        {
            if (!Directory.Exists(RecordingsFolderPath))
            {
                Directory.CreateDirectory(RecordingsFolderPath);
                Debug.Log($"Created recordings folder at: {RecordingsFolderPath}");
            }
        }

        public static void LoadSettings()
        {
            if (JsonWrapper.TryLoadJson<ReplaySettings>(SettingsFilePath, out ReplaySettings loadedSettings))
            {
                CurrentSettings = loadedSettings;
            }
            else
            {
                CurrentSettings = new ReplaySettings();
                SaveSettings();
            }
        }
        public static void SaveSettings()
        {
            JsonWrapper.SaveAsJson(SettingsFilePath, CurrentSettings, true);
        }

        private static FolderPath SavingFolder
        {
            get { return FileLocations.BaseFolder.Extend((Application.isMobilePlatform || Application.isEditor) ? "Saving" : "/../Saving"); }
        }
    }

    public class ReplaySettings
    {
        [JsonProperty("hasSeenInfoMenu")]
        public bool HasSeenInfoMenu { get; set; } = false;
    }

    // Custom contract resolver to exclude derived properties
    internal class IgnoreDerivedPropertiesContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            // Exclude derived vector properties that can be calculated from x and y
            if (property.PropertyName == "ToVector2" ||
                property.PropertyName == "ToVector3" ||
                property.PropertyName == "normalized" ||
                property.PropertyName == "magnitude" ||
                property.PropertyName == "sqrMagnitude" ||
                property.PropertyName == "AngleRadians" ||
                property.PropertyName == "AngleDegrees")
            {
                property.ShouldSerialize = instance => false;
            }

            return property;
        }
    }

    internal static class Util
    {
        public static void ReturnLog(string message)
        {
            Debug.Log($"[replay] {message}");
            return;
        }

        public static void Log(string message)
        {
            Debug.Log($"[Replay] {message}");
            MsgDrawer.main.Log(message);
        }

        internal static bool IsRocketInOrbit(Rocket rocket)
        {   // Check if the rocket's physics component has an InOrbit method and invoke it
            // Had to use reflection because the meathoud i found was private, there may be other ways but this works
            if (rocket?.physics == null) return false;
            var physicsType = rocket.physics.GetType();
            var inOrbitMethod = physicsType.GetMethod("InOrbit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (inOrbitMethod != null)
                return (bool)inOrbitMethod.Invoke(rocket.physics, null);
            return false;
        }

        internal static string SanitizeFileName(string fileName)
        {   // Replace invalid characters in the file name with underscores
            if (string.IsNullOrEmpty(fileName)) return "UnnamedRocket";
            foreach (char c in Path.GetInvalidFileNameChars())
            { fileName = fileName.Replace(c, '_'); }
            return fileName;
        }

        internal static string GenerateRocketHash(Rocket rocket)
        {
            if (rocket == null) return "null_rocket";

            // Create a hash based on rocket properties that define its unique state
            var hashData = new System.Text.StringBuilder();


            {   // add the properties of the rocket to the hash
                hashData.Append(rocket.rocketName ?? "unnamed");
                hashData.Append(rocket.location?.planet?.Value?.codeName ?? "unknown_planet");

                if (rocket.location?.position.Value != null)
                {
                    var pos = rocket.location.position;
                    hashData.Append(pos.Value.magnitude.ToString("F6"));
                    hashData.Append(pos.Value.AngleRadians.ToString("F6"));
                }

                if (rocket.location?.velocity?.Value != null)
                {
                    var vel = rocket.location.velocity;
                    hashData.Append(vel.Value.magnitude.ToString("F6"));
                    hashData.Append(vel.Value.AngleRadians.ToString("F6"));
                }

                if (rocket.partHolder?.parts != null)
                {
                    hashData.Append(rocket.partHolder.parts.Count.ToString());
                    foreach (var part in rocket.partHolder.parts)
                    {
                        if (part != null)
                        {
                            hashData.Append(part.name);
                            hashData.Append(part.Position.x.ToString("F3"));
                            hashData.Append(part.Position.y.ToString("F3"));
                        }
                    }
                }
            }

            // Generate hash from the combined data
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashData.ToString()));
                return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").Substring(0, 16);
            }
        }

    }



}