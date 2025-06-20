﻿using HarmonyLib;
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
            HomeManagerAlert();
            Settings.LoadSettings();
        }

        public static void HomeManagerAlert()
        {
            // Check if user has already seen the info menu
            if (Settings.CurrentSettings.HasSeenInfoMenu)
                return;

            List<string> alertMessages = new List<string>
            {
                "<color=yellow>Welcome to the Replay Mod!</color>",
                "<color=red>Some Warnings</color>",
                "This mod is in <color=orange>beta</color> and may cause issues.",
                "- Do not delete solar systems",
                "       they are required to replay recordings.",
                "- large file sizes",
                "- Save loss is unlikely",
                "Press Escape to close this message."
            };

            List<MenuElement> alertElements = new List<MenuElement>
            {
                TextBuilder.CreateText(() => "Replay Mod Alert"),
                ElementGenerator.VerticalSpace(12)
            };

            foreach (var message in alertMessages)
            {
                alertElements.Add(TextBuilder.CreateText(() => message));
            }

            Func<Screen_Base> menuScreen = MenuGenerator.CreateMenu(CancelButton.Close, SFS.Input.CloseMode.Current, null, null, alertElements.ToArray());
            ScreenManager.main.OpenScreen(menuScreen);

            // Mark that user has seen the info menu and save settings
            Settings.CurrentSettings.HasSeenInfoMenu = true;
            Settings.SaveSettings();

            // Apply left alignment and resize only warning messages (indices 2-5 in alertMessages)
            TextAdapter[] textAdapters = UnityEngine.Object.FindObjectsOfType<TextAdapter>();
            List<RectTransform> warningRectTransforms = new List<RectTransform>();
            int warningStartIndex = 1;
            int warningEndIndex = 5;

            for (int i = 0; i < textAdapters.Length && i < alertElements.Count; i++)
            {
                // Check if this corresponds to a warning message
                if (i >= warningStartIndex && i <= warningEndIndex)
                {
                    var textAdapter = textAdapters[i];

                    // Collect RectTransform for resizing
                    RectTransform[] children = textAdapter.GetComponentsInChildren<RectTransform>();
                    warningRectTransforms.AddRange(children);

                    // Set text alignment to left through underlying Text component
                    var textComponent = textAdapter.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (textComponent != null)
                    {
                        textComponent.alignment = TextAnchor.MiddleLeft;
                    }

                    // Also handle TextMeshPro components using reflection
                    var tmpComponent = textAdapter.GetComponent("TextMeshProUGUI");
                    if (tmpComponent != null)
                    {
                        var alignmentProperty = tmpComponent.GetType().GetProperty("alignment");
                        if (alignmentProperty != null)
                        {
                            var alignmentType = alignmentProperty.PropertyType;
                            var midlineLeftValue = System.Enum.Parse(alignmentType, "MidlineLeft");
                            alignmentProperty.SetValue(tmpComponent, midlineLeftValue);
                        }
                    }
                }
            }

            // Resize warning message elements to fit content
            if (warningRectTransforms.Count > 0)
            {
                Vector2 largestSize = Vector2.zero;

                // Find the largest size among warning element rect transforms
                foreach (var rt in warningRectTransforms)
                {
                    if (rt.sizeDelta.x > largestSize.x)
                        largestSize.x = rt.sizeDelta.x;
                    if (rt.sizeDelta.y > largestSize.y)
                        largestSize.y = rt.sizeDelta.y;
                }

                // Apply the largest size to warning element rect transforms
                foreach (var rt in warningRectTransforms)
                {
                    rt.sizeDelta = largestSize - new Vector2(40, 0); // Keep height as is, only adjust width
                }

                Debug.Log($"Resized {warningRectTransforms.Count} warning message RectTransform components to size: {largestSize}");
            }
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