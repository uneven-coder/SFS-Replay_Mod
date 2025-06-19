using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SFS.IO;
using SFS.World;
using System;
using Newtonsoft.Json;
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
                Util.ReturnLog("Cannot create quick save: No active recording session");

            try
            {
                // Pause world to safely capture state
                // this could be repllcaed or even removed 
                // but i dont belive it will cause issues

                // todo: although i do belive time is already frozen-
                // when this is called from record game
                // so this may not be needed
                FreezeOrResumeTime(true);

                // create a temp path using the base recordings folder so it dosent cause issues like replacing the base recording folder
                _CurrentRecordingFolder = new FolderPath(Path.Combine(Settings.RecordingsFolderPath, CurrentRecordingState.RecordingName));

                // Ensure blueprints folder exists
                string blueprintsFolder = Path.Combine(_CurrentRecordingFolder, "Blueprints");
                if (!Directory.Exists(blueprintsFolder))
                    Directory.CreateDirectory(blueprintsFolder);

                // Get current world state data
                var worldState = new QuickSaveData
                {
                    Timestamp = DateTime.Now,
                    WorldTime = WorldTime.main.worldTime,
                    // i dont think we need player adress, at most we need camera position
                    // PlayerAddress = PlayerController.main.player.Value?.location?.planet?.Value?.codeName ?? "Unknown" 
                    // TimewarpIndex = WorldTime.main.timewarpIndex, // also dont think we need this
                };

                // Save all rockets as individual blueprint files with unique hash-based naming
                Rocket[] rockets = GameManager.main.rockets.ToArray();
                var rocketSaves = new List<RocketSaveData>();
                var savedRocketHashes = new HashSet<string>();

                foreach (Rocket rocket in rockets)
                {
                    if (rocket == null) continue;

                    // Create unique identifier for this rocket based on actual rocket data
                    // why a hash? idk it could be random or a incremental id
                    // but i dont think it matters much i may chnage to a id, the function can be swaped out later
                    // but it can still be used to check if the rocket is already saved or diffrent enough to be saved later in the update changes files
                    string rocketHash = GenerateRocketHash(rocket);
                    if (savedRocketHashes.Contains(rocketHash))
                        // Skip if we've already saved this exact rocket state
                        Util.ReturnLog($"Skipping duplicate rocket with hash: {rocketHash}");
                    savedRocketHashes.Add(rocketHash);



                    var rocketSave = new RocketSave(rocket);
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
                    // may have to change this as to get the rockets we have to get the hash of the starting conditions of the rocket to make sure its the same rocket
                    // so maybe saving a diffrent name would be better so it dosent duplicate and we know what to refrence later
                    string rocketName = !string.IsNullOrEmpty(rocketSave.rocketName) ? rocketSave.rocketName : "UnnamedRocket";
                    string rocketFileName = $"{SanitizeFileName(rocketName)}_{rocketHash}.json";
                    string rocketFilePath = Path.Combine(blueprintsFolder, rocketFileName);
                    
                    // Configure JSON settings for rocket save files
                    // this is to make sure we dont save the same rocket twice and to make sure we
                    // save the rocket in a way that is easy to read and understand
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
                    // RocketCount = rocketSaves.Count,
                    WorldState = worldState,
                    RocketFiles = rocketSaves.Select(r => {
                        string rocketName = !string.IsNullOrEmpty(r.RocketSave.rocketName) ? r.RocketSave.rocketName : "UnnamedRocket";
                        return $"{SanitizeFileName(rocketName)}_{r.RocketId}.json";
                    }).ToList()
                };


                string quickSaveFilePath = Path.Combine(_CurrentRecordingFolder, $"quicksave_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                var replayJsonSettings = new JsonSerializerSettings {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };


                File.WriteAllText(quickSaveFilePath, JsonConvert.SerializeObject(quickSave, replayJsonSettings));
                Debug.Log($"Quick save created successfully with {rocketSaves.Count} rockets");
                Debug.Log($"Rocket blueprints saved to: {blueprintsFolder}");
                Debug.Log($"Quick save file: {quickSaveFilePath}");
            }
            catch (System.Exception ex)
                { Debug.LogError($"Failed to create quick save: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
            finally // Ensure we always resume time after saving even if an error occurs
                { FreezeOrResumeTime(false); }
        }


        private static int i = 0; // just a counter so the game dosent truncate the log message
        internal static void SetChange(Rocket[] rockets)
        {   // takes in rockets and and will create a delta for change so unchanged rockets are skiped
            // create a changes file for the rocket and store the changes, 
            // Some stuff to not check for is:
            // - Rocket name changes

            // Factors to concider:
            // rockets merging
            // rockets splitting
            // rockets being deleted/destroyed
            // rockets being created 
            // if its created add to blueprints folder
            // includes all basics like position, velocity, rotation, etc.

            if (!CurrentRecordingState.IsRecording)
            {
                Debug.LogWarning("Cannot set changes: No active recording session");
                return;
            }
            // check if the rocket exists in the blueprints folder

            Debug.Log("Setting changes for rockets... " + i++);
        }
    }

    public class QuickSaveData
    {   // dont need many of these as i wont use them and they are not needed for the quick save
        public DateTime Timestamp { get; set; }
        public double WorldTime { get; set; }
        // public int TimewarpIndex { get; set; }
        // public string MapMode { get; set; }
        public double ViewPositionX { get; set; }
        public double ViewPositionY { get; set; }
        // public double ViewDistance { get; set; }
        // public string PlayerAddress { get; set; }
        // public float CameraDistance { get; set; }
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
        // public int RocketCount { get; set; }
        public List<string> RocketFiles { get; set; }
    }
}