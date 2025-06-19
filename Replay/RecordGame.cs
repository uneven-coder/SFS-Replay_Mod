using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using SFS.IO;
using SFS.World;
using SFS.WorldBase;
using System;
using SFS;
using Newtonsoft.Json;
using static replay.SaveManager;
using static replay.Main;

namespace replay
{
    // all stuff it should capture
    // the solar system
    //      Get the solar system 
    // it could be better to copy the solar system file but this is a lot of storage and the user may not want to do this
    // store all rockets into diffrent blueprint files
    // store all rockets no matter what time they were made so that we can save changes to reduce storage size, if they existed store them

    // store the planet
    //      have all rockets of that planet
    // maybe also show any docked rockets as seperate rockets in the heiarchy


    // recording just means save a file of the rockket in its basic state
    // then the states of every part in the rocket at a time


    public static class RecordGame
    {
        public static RecordingState CurrentRecordingState { get; private set; } = new RecordingState();
        internal static FolderPath _CurrentRecordingFolder;

        private static float _changeTrackingInterval = 0.3f;
        private static float _lastChangeTrackTime = 0f;

        internal static Dictionary<string, string> _lastRocketHashes = new Dictionary<string, string>();



        public static void StartRecording()
        {
            try
            {
                FreezeOrResumeTime(true);

                {
                    if (Base.worldBase?.settings?.solarSystem == null)
                        Util.ReturnLog("Cannot start recording: worldBase, settings, or solarSystem is null");

                    if (Base.planetLoader?.planets == null)
                        Util.ReturnLog("Cannot start recording: planetLoader or planets is null");

                    if (GameManager.main?.rockets == null)
                        Util.ReturnLog("Cannot start recording: GameManager.main or rockets is null");
                }

                // Initialize planet rocket mapping
                rocketRegistry.PlanetRocketMapping.Clear();
                foreach (Planet planet in Base.planetLoader.planets.Values)
                {
                    if (planet != null)
                        rocketRegistry.PlanetRocketMapping[planet] = new List<Rocket>();
                }


                CurrentRecordingState = new RecordingState
                {
                    IsRecording = true,
                    RecordingName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}",
                    SessionId = Guid.NewGuid().ToString(),
                    StartTime = DateTime.Now,
                    SolarSystem = Base.worldBase.settings.solarSystem
                };

                {   // create a temp path using the base recordings folder so it dosent cause issues like replacing the base recording folder
                    // then make the sub folders for the recording
                    _CurrentRecordingFolder = new FolderPath(Path.Combine(Settings.RecordingsFolderPath, CurrentRecordingState.RecordingName));
                    Debug.Log($"Creating recording folders at: {_CurrentRecordingFolder}");
                    Directory.CreateDirectory(_CurrentRecordingFolder);
                    Directory.CreateDirectory(Path.Combine(_CurrentRecordingFolder, "Blueprints"));
                    Directory.CreateDirectory(Path.Combine(_CurrentRecordingFolder, "Changes"));
                }

                // Record existing rockets
                Rocket[] rockets = GameManager.main.rockets.ToArray();
                foreach (Rocket rocket in rockets)
                {
                    if (rocket?.location?.planet?.Value != null)
                        rocketRegistry.AddRocketToPlanet(rocket.location.planet.Value, rocket);
                    // Debug.Log($"Added rocket '{rocket.rocketName ?? "Unnamed"}' to planet '{planet.DisplayName ?? planet.codeName ?? "Unknown"}'");
                    else
                        Debug.LogWarning($"Skipped rocket with null location or planet: {rocket?.rocketName ?? "Unnamed"}");
                }

                // this is the end of the prepare env for recording
                // Create quick save to capture initial state
                CreateQuickSave();


                // Initialize change tracking
                _lastChangeTrackTime = Time.time;
                _lastRocketHashes.Clear();

                // Create update handler to continusly track
                EnsureUpdateHandlerExists();

                Util.Log("Recording started successfully");

                // this will print a file tree structure of the rockets and planets
                // todo: can be removed but helps with debugging also want to use it for ui later
                Debug.Log(rocketRegistry.ReturnAsPrint());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to start recording: {ex.Message}\nStackTrace: {ex.StackTrace}");
                CurrentRecordingState.IsRecording = false;
            }
            finally
            { FreezeOrResumeTime(false); }
        }

        public static void FreezeOrResumeTime(bool FreezeTime)
        {   // realtime physics should be true so stuff like engines arnt turned off
            // this is used to freeze the world so that we can save all data without it changing
            var worldTime = UnityEngine.Object.FindObjectOfType<WorldTime>();
            if (worldTime != null)
                if (FreezeTime)
                    worldTime.SetState(0, true, false);
                else
                    worldTime.SetState(1, true, true);
            else
                Debug.LogError("WorldTime instance not found.");
        }

        public static void StopRecording()
        {
            if (!CurrentRecordingState.IsRecording) return;

            try
            {
                FreezeOrResumeTime(true);   // Pause world to safely save all data

                // Set end time before saving
                CurrentRecordingState.EndTime = DateTime.Now;

                // Save the recording data
                SaveRecording();

                Debug.Log($"Recording stopped: {CurrentRecordingState.SessionId}");
                Debug.Log($"Files saved to: {_CurrentRecordingFolder}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving recording data: {ex.Message}");
            }
            finally
            {
                // Always resume world and clear state even if saving failed
                FreezeOrResumeTime(false);

                {   // Clean up update handler
                    GameObject updateHandler = GameObject.Find("RecordingUpdateHandler");
                    if (updateHandler != null)
                        UnityEngine.Object.Destroy(updateHandler);
                }


                {   // Clear recording state
                    CurrentRecordingState = new RecordingState();
                    rocketRegistry.PlanetRocketMapping.Clear();
                    _CurrentRecordingFolder = null;
                    _lastRocketHashes.Clear();
                }

                Debug.Log("Recording Cleaned up");
            }
        }

        public static void SaveRecording()
        {
            if (!CurrentRecordingState.IsRecording)
                Util.ReturnLog("Cannot save recording: No active recording session");


            try
            {
                // Ensure recording folder exists - use the absolute path
                _CurrentRecordingFolder = new FolderPath(Path.Combine(Settings.RecordingsFolderPath, CurrentRecordingState.RecordingName));
                if (!Directory.Exists(_CurrentRecordingFolder))
                {
                    Directory.CreateDirectory(_CurrentRecordingFolder);
                    Directory.CreateDirectory(Path.Combine(_CurrentRecordingFolder, "Blueprints"));
                    Directory.CreateDirectory(Path.Combine(_CurrentRecordingFolder, "Changes"));
                }

                // Save recording metadata
                var recordingInfo = new RecordingInfo
                {
                    SessionId = CurrentRecordingState.SessionId,
                    RecordingName = CurrentRecordingState.RecordingName,
                    StartTime = CurrentRecordingState.StartTime,
                    EndTime = CurrentRecordingState.EndTime != DateTime.MinValue ? CurrentRecordingState.EndTime : DateTime.Now,
                    SolarSystemName = CurrentRecordingState.SolarSystem?.name ?? "Unknown",
                    PlanetCount = rocketRegistry.PlanetRocketMapping.Count,
                    TotalRockets = rocketRegistry.PlanetRocketMapping.Values.Sum(rockets => rockets.Count)
                };

                string metadataPath = Path.Combine(_CurrentRecordingFolder, "recording_info.json");
                File.WriteAllText(metadataPath, JsonConvert.SerializeObject(recordingInfo, Formatting.Indented));

                Debug.Log($"Recording saved successfully to: {_CurrentRecordingFolder}");
                Debug.Log($"Metadata saved: {metadataPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save recording: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        // Ensures the update handler is always present while recording
        private static void EnsureUpdateHandlerExists()
        {
            GameObject updateHandler = GameObject.Find("RecordingUpdateHandler");
            if (updateHandler == null)
            {
                updateHandler = new GameObject("RecordingUpdateHandler");
                updateHandler.AddComponent<RecordingUpdateHandler>();
                UnityEngine.Object.DontDestroyOnLoad(updateHandler);
            }
        }

        // Update method that should be called from the main game loop or mod update
        public static void Update()
        {
            // Ensure update handler is present while recording
            // todo: change to call once on reording start
            if (CurrentRecordingState.IsRecording)
                EnsureUpdateHandlerExists();

            // Check if we should track changes
            if (CurrentRecordingState.IsRecording && Time.time - _lastChangeTrackTime >= _changeTrackingInterval)
            {
                _lastChangeTrackTime = Time.time;

                // Get current rockets and track changes
                // =====================================
                // =====================================
                // todo: Change this to get rockets and any new or removed rockets can be added with their active state
                // =====================================
                // =====================================

                Rocket[] currentRockets = GameManager.main?.rockets?.ToArray();
                if (currentRockets != null)
                    SaveManager.SetChange(currentRockets);

            }
        }

        public static void UpdateRecordingState(RecordingState newState) =>
            CurrentRecordingState = newState;
    }

    // Unity MonoBehaviour to handle update calls
    public class RecordingUpdateHandler : MonoBehaviour
    {
        private void Update() =>
            RecordGame.Update();

        private void OnDestroy()
        {
            // Clean up when component is destroyed
            if (RecordGame.CurrentRecordingState.IsRecording)
            {
                RecordGame.StopRecording();
                Debug.Log("RecordingUpdateHandler destroyed, recording stopped!!!!!!!!!!");
            }
        }
    }
}



// new namespace to help specify the recording state and rocket registry
// holds a higherarchy of the planets and rockets
// this is used to help organize the rockets and planets in a tree structure
// usefull for the ui
namespace replay
{
    public class RecordingState
    {
        public bool IsRecording { get; set; }
        public string SessionId { get; set; }
        public string RecordingName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        // public List<string> RecordedEvents { get; set; }
        public SolarSystemReference SolarSystem { get; set; }

        public RecordingState()
        {
            IsRecording = false;
            RecordingName = string.Empty;
            SessionId = string.Empty;
            StartTime = DateTime.MinValue;
            EndTime = DateTime.MinValue;
            SolarSystem = null;
        }
    }

    public static class rocketRegistry
    {
        public static Dictionary<Planet, List<Rocket>> PlanetRocketMapping = new Dictionary<Planet, List<Rocket>>(); public static void AddRocketToPlanet(Planet planet, Rocket rocket)
        {
            if (planet == null || rocket == null)
                Util.ReturnLog($"Cannot add rocket to planet: planet={planet != null}, rocket={rocket != null}");

            if (!PlanetRocketMapping.ContainsKey(planet))
                PlanetRocketMapping[planet] = new List<Rocket>();

            if (!PlanetRocketMapping[planet].Contains(rocket))
                PlanetRocketMapping[planet].Add(rocket);
        }














        

        public static string ReturnAsPrint(bool hideEmptyPlanets = false, bool hideEmptyOrbit = true)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Solar System Hierarchy:");

            // Get all planets and organize them by hierarchy (parent planets and their moons)
            var allPlanets = PlanetRocketMapping.Keys.ToList();
            var rootPlanets = allPlanets.Where(p => p.parentBody == null).OrderBy(p => p.codeName).ToList();

            for (int i = 0; i < rootPlanets.Count; i++)
            {
                var planet = rootPlanets[i];
                bool isLastRootPlanet = (i == rootPlanets.Count - 1);

                AppendPlanetHierarchy(sb, planet, "", isLastRootPlanet, hideEmptyPlanets, hideEmptyOrbit);
            }

            return sb.ToString();
        }

        private static void AppendPlanetHierarchy(System.Text.StringBuilder sb, Planet planet, string baseIndent, bool isLastSibling, bool hideEmptyPlanets, bool hideEmptyOrbit)
        {
            var rockets = PlanetRocketMapping.ContainsKey(planet) ? PlanetRocketMapping[planet] : new List<Rocket>();
            var moons = PlanetRocketMapping.Keys.Where(p => p.parentBody == planet).OrderBy(p => p.codeName).ToList();

            // Skip empty planets if hideEmptyPlanets is true
            if (hideEmptyPlanets && rockets.Count == 0 && moons.Count == 0)
                return; string planetPrefix = isLastSibling ? "└── " : "├── ";
            string planetType = planet.parentBody == null ? "* " : "o ";
            sb.AppendLine($"{baseIndent}{planetPrefix}{planetType}{planet.codeName}");

            string childIndent = baseIndent + (isLastSibling ? "    " : "│   ");

            // Count total children (moons + rocket sections)
            int totalChildren = moons.Count;
            bool hasRocketSections = rockets.Count > 0 || !hideEmptyOrbit;
            if (hasRocketSections) totalChildren += 2; // "Not in Orbit" and "In Orbit" sections

            int currentChildIndex = 0;

            // Add moons first
            for (int i = 0; i < moons.Count; i++)
            {
                bool isLastChild = (currentChildIndex == totalChildren - 1);
                AppendPlanetHierarchy(sb, moons[i], childIndent, isLastChild, hideEmptyPlanets, hideEmptyOrbit);
                currentChildIndex++;
            }

            // Add rocket sections
            if (hasRocketSections)
            {
                // Separate rockets into orbit and not in orbit
                var rocketsInOrbit = new List<Rocket>();
                var rocketsNotInOrbit = new List<Rocket>();

                foreach (var rocket in rockets)
                {
                    bool inOrbit = false;
                    if (rocket.physics != null)
                    {
                        var physicsType = rocket.physics.GetType();
                        var inOrbitMethod = physicsType.GetMethod("InOrbit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (inOrbitMethod != null)
                            inOrbit = (bool)inOrbitMethod.Invoke(rocket.physics, null);
                    }
                    (inOrbit ? rocketsInOrbit : rocketsNotInOrbit).Add(rocket);
                }

                bool showNotInOrbit = rocketsNotInOrbit.Count > 0 || !hideEmptyOrbit;
                bool showInOrbit = rocketsInOrbit.Count > 0 || !hideEmptyOrbit; if (showNotInOrbit)
                {
                    bool isLastChild = (currentChildIndex == totalChildren - 1);
                    AppendRocketSection(sb, "^ Not in Orbit", rocketsNotInOrbit, childIndent, isLastChild);
                    currentChildIndex++;
                }

                if (showInOrbit)
                {
                    bool isLastChild = (currentChildIndex == totalChildren - 1);
                    AppendRocketSection(sb, "~ In Orbit", rocketsInOrbit, childIndent, isLastChild);
                    currentChildIndex++;
                }
            }
        }

        private static void AppendRocketSection(System.Text.StringBuilder sb, string title, List<Rocket> rockets, string baseIndent, bool isLastSection)
        {
            string sectionPrefix = isLastSection ? "└── " : "├── ";
            string rocketIndent = baseIndent + (isLastSection ? "    " : "│   ");

            sb.AppendLine($"{baseIndent}{sectionPrefix}{title}");

            for (int i = 0; i < rockets.Count; i++)
            {
                var rocket = rockets[i];
                bool isLastRocket = (i == rockets.Count - 1);
                string rocketPrefix = isLastRocket ? "└── " : "├── ";
                string rocketName = !string.IsNullOrEmpty(rocket.rocketName) ? rocket.rocketName : $"Rocket_{rocket.GetHashCode()}";
                sb.AppendLine($"{rocketIndent}{rocketPrefix}{rocketName}");
            }
        }
    }
    public class RecordingInfo
    {
        public string SessionId { get; set; }
        public string RecordingName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string SolarSystemName { get; set; }
        public int PlanetCount { get; set; }
        public int TotalRockets { get; set; }
    }






}
