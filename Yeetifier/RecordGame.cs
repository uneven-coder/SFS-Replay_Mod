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
            // RecordedEvents = new List<string>();
            SolarSystem = null;
            // Don't reset SolarSystemName - it should only be updated when a new world loads
        }
    }

    public static class heiarchy
    {
        public static Dictionary<Planet, List<Rocket>> PlanetsAndRockets = new Dictionary<Planet, List<Rocket>>();

        public static void AddRocketToPlanet(Planet planet, Rocket rocket)
        {
            if (planet == null || rocket == null) return;

            if (!PlanetsAndRockets.ContainsKey(planet))
            {
                PlanetsAndRockets[planet] = new List<Rocket>();
            }

            if (!PlanetsAndRockets[planet].Contains(rocket))
            {
                PlanetsAndRockets[planet].Add(rocket);
            }
        }

    }

    // all stuff it should capture
    // the solar system
    //      Get the name of the solar system 
    // it could be better to copy the solar system file but this is a lot of storage and the user may not want to do this
    // store all rockets into diffrent blueprint files
    // store all rockets no matter what time they were made so that we can save changes to reduce storage size, if they existed store them

    // store the planet name
    //      have all rockets of that planet
    // maybe also show any docked rockets as seperate rockets in the heiarchy


    // recording just means save a file of the rockket in its basic state
    // then the states of every part in the rocket at a time


    public static class RecordGame
    {

        public static RecordingState CurrentRecordingState { get; private set; } = new RecordingState();
        private static FolderPath _CurrentRecordingFolder;

        public static void StartRecording()
        {
            Debug.Log(Base.worldBase.settings.solarSystem.name);
            heiarchy.PlanetsAndRockets.Clear();
            foreach (Planet planet in Base.planetLoader.planets.Values)
            {
                heiarchy.PlanetsAndRockets[planet] = new List<Rocket>();
            }

            // Log the hierarchy
            string hierarchyLog = "Hierarchy:\n";
            foreach (var kvp in heiarchy.PlanetsAndRockets)
            {
                string planetName = kvp.Key?.codeName ?? "Unknown Planet";
                int rocketCount = kvp.Value.Count;
                hierarchyLog += $"Planet: {planetName} -> Rockets: {rocketCount}\n";
            }


            // Immediately try to record any existing rockets while paused
            try
            {
                // Pause the world to safely capture initial state
                FreezeOrResumeTime(true);

                CurrentRecordingState.SolarSystem = Base.worldBase.settings.solarSystem;                // Debug.Log($"Creating recording folders at: {_recordingBasePath}");
                // Directory.CreateDirectory(_recordingBasePath);
                // Directory.CreateDirectory(Path.Combine(_recordingBasePath, "Blueprints"));
                // Directory.CreateDirectory(Path.Combine(_recordingBasePath, "Changes"));
                
                Rocket[] rockets = GameManager.main.rockets.ToArray();
                Debug.Log($"Found {rockets.Length} rockets at recording start");
                foreach (Rocket rocket in rockets)
                {
                    if (rocket != null)
                    {
                        // RecordRocket(rocket);
                        heiarchy.AddRocketToPlanet(rocket.location.planet, rocket);
                    }
                }

                // Update hierarchy log after adding rockets
                hierarchyLog = "Updated Hierarchy:\n";
                foreach (var kvp in heiarchy.PlanetsAndRockets)
                {
                    string planetName = kvp.Key?.codeName ?? "Unknown Planet";
                    int rocketCount = kvp.Value.Count;
                    hierarchyLog += $"Planet: {planetName} -> Rockets: {rocketCount}\n";
                }
                Debug.Log(hierarchyLog);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to record initial rockets: {ex.Message}");
            }
            finally
            {
                FreezeOrResumeTime(false);
            }
        }

        private static void FreezeOrResumeTime(bool FreezeTime)
        {
            var worldTime = UnityEngine.Object.FindObjectOfType<WorldTime>();
            if (worldTime != null)
                if (FreezeTime)
                    worldTime.SetState(0, false, false);
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
                // Pause world to safely save all data
                FreezeOrResumeTime(true);

                // Debug.Log($"Stopping recording. Blueprints: {_rocketBlueprints.Count}, Changes: {_rocketChanges.Count}");
                // // // Force save all remaining data
                // // SaveRecordingSession();
                // // SaveAllBlueprints();
                // // SaveAllChanges();

                // Debug.Log($"Recording stopped: {_currentSession.SessionId}");
                // Debug.Log($"Files saved to: {_recordingBasePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving recording data: {ex.Message}");
            }
            finally
            {
                // Always resume world even if saving failed
                FreezeOrResumeTime(false);
            }
        }

        public static void SaveRecording()
        {
            _CurrentRecordingFolder = Settings.RecordingsFolderPath.Extend(CurrentRecordingState.RecordingName);
            throw new System.NotImplementedException("SaveRecording is not implemented yet. Use StopRecording to save the current session.");
        }

        public static void UpdateRecordingState(RecordingState newState)
        {
            CurrentRecordingState = newState;
            // Solar system name is preserved since it's static and only changes on world load
        }

    }
}