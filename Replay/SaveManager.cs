using HarmonyLib;
using ModLoader;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using SFS.IO;
using SFS.Parts;
using SFS.World;
using SFS.WorldBase;
using System;
using static replay.Main;
using SFS;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SFS.Navigation;
using SFS.UI;
using SFS.World.Maps;
using static replay.RecordGame;


namespace replay
{

    public class SaveManager
    {
        public static void CreateQuickSave()
        {   // calling it quick save as its simular to sfs quicksave
            // it will save all rockets as blueprints/the quick save format except better formated
            // its more custom but works the same and donsnt really save much of the world


            if (!CurrentRecordingState.IsRecording)
            {
                Debug.LogWarning("Cannot create quick save: No active recording session");
                return;
            }

            try
            {
                // Pause world to safely capture state
                FreezeOrResumeTime(true);

                // Always use absolute path for current recording folder
                _CurrentRecordingFolder = new FolderPath(Path.Combine(Settings.RecordingsFolderPath, CurrentRecordingState.RecordingName));

                // Ensure blueprints folder exists
                string blueprintsFolder = Path.Combine(_CurrentRecordingFolder, "Blueprints");
                if (!Directory.Exists(blueprintsFolder))
                {
                    Directory.CreateDirectory(blueprintsFolder);
                }// Get current world state data
                var worldState = new QuickSaveData
                {
                    Timestamp = DateTime.Now,
                    WorldTime = WorldTime.main.worldTime,
                    TimewarpIndex = WorldTime.main.timewarpIndex,
                    PlayerAddress = PlayerController.main.player.Value?.location?.planet?.Value?.codeName ?? "Unknown"
                };                // Save all rockets as individual blueprint files with unique hash-based naming
                Rocket[] rockets = GameManager.main.rockets.ToArray();
                var rocketSaves = new List<RocketSaveData>();
                var savedRocketHashes = new HashSet<string>();

                foreach (Rocket rocket in rockets)
                {
                    if (rocket == null) continue;

                    // Create unique identifier for this rocket based on actual rocket data
                    string rocketHash = GenerateRocketHash(rocket);

                    // Skip if we've already saved this exact rocket state
                    if (savedRocketHashes.Contains(rocketHash))
                    {
                        Debug.LogWarning($"Skipping duplicate rocket with hash: {rocketHash}");
                        continue;
                    }
                    savedRocketHashes.Add(rocketHash);

                    // Create rocket save data using SFS's RocketSave structure
                    var rocketSave = new RocketSave(rocket);

                    // Create our custom rocket save data with additional metadata
                    var rocketSaveData = new RocketSaveData
                    {
                        RocketSave = rocketSave,
                        RocketId = rocketHash,
                        PlanetName = rocket.location?.planet?.Value?.codeName ?? "Unknown",
                        IsInOrbit = IsRocketInOrbit(rocket),
                        SaveTimestamp = DateTime.Now
                    };

                    rocketSaves.Add(rocketSaveData);

                    // Save individual rocket blueprint file with hash-based name
                    string rocketName = !string.IsNullOrEmpty(rocketSave.rocketName) ? rocketSave.rocketName : "UnnamedRocket";
                    string rocketFileName = $"{SanitizeFileName(rocketName)}_{rocketHash}.json";
                    string rocketFilePath = Path.Combine(blueprintsFolder, rocketFileName);                    // Configure JSON settings to handle circular references and exclude derived properties
                    var jsonSettings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented,
                        DefaultValueHandling = DefaultValueHandling.Ignore,
                        ContractResolver = new IgnoreDerivedPropertiesContractResolver()
                    };

                    File.WriteAllText(rocketFilePath, JsonConvert.SerializeObject(rocketSaveData, jsonSettings));
                }

                // Save master quick save file with world state and rocket references
                var quickSave = new QuickSave
                {
                    WorldState = worldState,
                    RocketCount = rocketSaves.Count,
                    RocketFiles = rocketSaves.Select(r =>
                    {
                        string rocketName = !string.IsNullOrEmpty(r.RocketSave.rocketName) ? r.RocketSave.rocketName : "UnnamedRocket";
                        return $"{SanitizeFileName(rocketName)}_{r.RocketId}.json";
                    }).ToList()
                }; string quickSaveFilePath = Path.Combine(_CurrentRecordingFolder, $"quicksave_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                // Configure JSON settings for master save file
                var masterJsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };

                File.WriteAllText(quickSaveFilePath, JsonConvert.SerializeObject(quickSave, masterJsonSettings));

                Debug.Log($"Quick save created successfully with {rocketSaves.Count} rockets");
                Debug.Log($"Quick save file: {quickSaveFilePath}");
                Debug.Log($"Rocket blueprints saved to: {blueprintsFolder}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to create quick save: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            finally
            {
                FreezeOrResumeTime(false);
            }
        }
    }

    public class QuickSaveData
    {
        public DateTime Timestamp { get; set; }
        public double WorldTime { get; set; }
        public int TimewarpIndex { get; set; }
        public string MapMode { get; set; }
        public double ViewPositionX { get; set; }
        public double ViewPositionY { get; set; }
        public double ViewDistance { get; set; }
        public string PlayerAddress { get; set; }
        public float CameraDistance { get; set; }
    }

    public class RocketSaveData
    {
        public RocketSave RocketSave { get; set; }
        public string RocketId { get; set; }
        public string PlanetName { get; set; }
        public bool IsInOrbit { get; set; }
        public DateTime SaveTimestamp { get; set; }
    }

    public class QuickSave
    {
        public QuickSaveData WorldState { get; set; }
        public int RocketCount { get; set; }
        public List<string> RocketFiles { get; set; }
    }
}