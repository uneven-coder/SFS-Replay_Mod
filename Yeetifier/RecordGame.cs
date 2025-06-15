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


namespace replay
{
    // Simple JSON serializer for basic data structures
    public static class SimpleJsonSerializer
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            if (obj is string str) return $"\"{str}\"";
            if (obj is bool b) return b.ToString().ToLower();
            if (obj is int || obj is float || obj is double) return obj.ToString();

            if (obj.GetType().IsArray || obj is System.Collections.IEnumerable)
            {
                var items = new List<string>();
                foreach (var item in (System.Collections.IEnumerable)obj)
                {
                    items.Add(Serialize(item));
                }
                return $"[{string.Join(",", items)}]";
            }

            // Handle objects with properties
            var properties = new List<string>();
            foreach (var prop in obj.GetType().GetProperties())
            {
                if (prop.CanRead)
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        properties.Add($"\"{prop.Name}\":{Serialize(value)}");
                    }
                    catch { }
                }
            }
            return $"{{{string.Join(",", properties)}}}";
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

    public class RocketState
    {   // in game it is better to use the direct Part class but to save it we can use a class
        public double TimeStamp { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public static RocketState FromRocket(Rocket rocket)
        {
            var state = new RocketState
            {
                TimeStamp = Time.timeAsDouble
            };

            state.CaptureAllProperties(rocket);
            return state;
        }

        private void CaptureAllProperties(Rocket rocket)
        {
            try
            {
                // Capture basic rocket properties using reflection
                var rocketType = typeof(Rocket);
                var fields = rocketType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var properties = rocketType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // Capture all public fields
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(rocket);
                        if (IsSerializableType(value))
                        {
                            Properties[$"field_{field.Name}"] = value;
                        }
                        else if (value != null)
                        {
                            // For complex objects, capture their state recursively
                            CaptureObjectState(value, $"field_{field.Name}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to capture field {field.Name}: {ex.Message}");
                    }
                }

                // Capture all public properties
                foreach (var prop in properties)
                {
                    try
                    {
                        if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                        {
                            var value = prop.GetValue(rocket);
                            if (IsSerializableType(value))
                            {
                                Properties[$"prop_{prop.Name}"] = value;
                            }
                            else if (value != null)
                            {
                                // For complex objects, capture their state recursively
                                CaptureObjectState(value, $"prop_{prop.Name}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to capture property {prop.Name}: {ex.Message}");
                    }
                }

                // Capture all modules dynamically
                CaptureModulesState(rocket);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error capturing rocket properties: {ex.Message}");
            }
        }

        private void CaptureObjectState(object obj, string prefix, int depth = 0)
        {
            if (obj == null || depth > 3) return; // Prevent infinite recursion

            try
            {
                var objType = obj.GetType();

                // Handle special Unity types
                if (obj is Vector2 v2)
                {
                    Properties[$"{prefix}_x"] = v2.x;
                    Properties[$"{prefix}_y"] = v2.y;
                    return;
                }
                if (obj is Vector3 v3)
                {
                    Properties[$"{prefix}_x"] = v3.x;
                    Properties[$"{prefix}_y"] = v3.y;
                    Properties[$"{prefix}_z"] = v3.z;
                    return;
                }
                if (obj is Quaternion q)
                {
                    Properties[$"{prefix}_x"] = q.x;
                    Properties[$"{prefix}_y"] = q.y;
                    Properties[$"{prefix}_z"] = q.z;
                    Properties[$"{prefix}_w"] = q.w;
                    return;
                }

                // Handle objects with Value property (like Float_Local, Bool_Local, etc.)
                var valueProperty = objType.GetProperty("Value");
                if (valueProperty != null && valueProperty.CanRead)
                {
                    try
                    {
                        var value = valueProperty.GetValue(obj);
                        if (IsSerializableType(value))
                        {
                            Properties[$"{prefix}_Value"] = value;
                        }
                    }
                    catch { }
                }

                // Capture other important properties
                var importantProps = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 &&
                               !p.Name.Equals("Value") && IsImportantProperty(p.Name));

                foreach (var prop in importantProps)
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        if (IsSerializableType(value))
                        {
                            Properties[$"{prefix}_{prop.Name}"] = value;
                        }
                        else if (value != null && depth < 2)
                        {
                            CaptureObjectState(value, $"{prefix}_{prop.Name}", depth + 1);
                        }
                    }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to capture object state for {prefix}: {ex.Message}");
            }
        }

        private void CaptureModulesState(Rocket rocket)
        {
            try
            {
                // Get all module types and capture their states
                var moduleTypes = GetAllModuleTypes();

                foreach (var moduleType in moduleTypes)
                {
                    try
                    {
                        var getModulesMethod = typeof(PartHolder).GetMethod("GetModules")?.MakeGenericMethod(moduleType);
                        if (getModulesMethod != null)
                        {
                            var modules = getModulesMethod.Invoke(rocket.partHolder, null) as System.Array;
                            if (modules != null)
                            {
                                for (int i = 0; i < modules.Length; i++)
                                {
                                    var module = modules.GetValue(i);
                                    if (module != null)
                                    {
                                        CaptureObjectState(module, $"module_{moduleType.Name}_{i}");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Capture parts state
                if (rocket.partHolder?.parts != null)
                {
                    for (int i = 0; i < rocket.partHolder.parts.Count; i++)
                    {
                        var part = rocket.partHolder.parts[i];
                        if (part != null)
                        {
                            CaptureObjectState(part, $"part_{i}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to capture modules state: {ex.Message}");
            }
        }

        private System.Type[] GetAllModuleTypes()
        {
            try
            {
                // Use reflection to find all module types from the assembly
                var assembly = typeof(Rocket).Assembly;
                return assembly.GetTypes()
                    .Where(type => type.Namespace != null &&
                           type.Namespace.Contains("SFS.Parts.Modules") &&
                           !type.IsAbstract &&
                           type.IsClass)
                    .ToArray();
            }
            catch
            {
                // Fallback to known module types
                return new System.Type[]
                {
                    typeof(SFS.Parts.Modules.EngineModule),
                    typeof(SFS.Parts.Modules.RcsModule),
                    typeof(SFS.Parts.Modules.ResourceModule),
                    typeof(SFS.Parts.Modules.ControlModule),
                    typeof(SFS.Parts.Modules.TorqueModule),
                    typeof(SFS.Parts.Modules.BoosterModule),
                    typeof(SFS.Parts.Modules.DockingPortModule),
                    typeof(SFS.Parts.Modules.FlowModule)
                };
            }
        }

        private bool IsSerializableType(object value)
        {
            if (value == null) return true;

            var type = value.GetType();
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(System.DateTime) ||
                   type.IsEnum;
        }

        private bool IsImportantProperty(string propertyName)
        {
            var importantProps = new[] {
                "position", "velocity", "rotation", "mass", "enabled", "throttle",
                "thrust", "on", "amount", "percent", "control", "torque", "name",
                "x", "y", "z", "w"
            };

            return importantProps.Any(prop => propertyName.ToLower().Contains(prop.ToLower()));
        }

        // Method to apply state back to rocket (for replay functionality)
        public void ApplyToRocket(Rocket rocket)
        {
            try
            {
                // Apply properties back using reflection
                var rocketType = typeof(Rocket);

                foreach (var kvp in Properties)
                {
                    try
                    {
                        ApplyProperty(rocket, kvp.Key, kvp.Value);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to apply property {kvp.Key}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error applying state to rocket: {ex.Message}");
            }
        }

        private void ApplyProperty(object target, string propertyPath, object value)
        {
            try
            {
                var parts = propertyPath.Split('_');
                if (parts.Length < 2) return;

                var targetType = target.GetType();

                if (parts[0] == "field")
                {
                    var field = targetType.GetField(parts[1]);
                    if (field != null && !field.IsInitOnly && !field.IsLiteral)
                    {
                        field.SetValue(target, value);
                    }
                }
                else if (parts[0] == "prop")
                {
                    var property = targetType.GetProperty(parts[1]);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(target, value);
                    }
                }
                // Additional logic for nested properties and modules can be added here
            }
            catch { }
        }
    }

    public class RocketBlueprint
    {
        public string RocketId { get; set; }
        public string BlueprintName { get; set; }
        public string BlueprintData { get; set; }
        public Dictionary<string, object> BaseProperties { get; set; } = new Dictionary<string, object>();
        public double CreatedAt { get; set; }
    }

    public class RocketChange
    {
        public string RocketId { get; set; }
        public double TimeStamp { get; set; }
        public Dictionary<string, object> ChangedProperties { get; set; } = new Dictionary<string, object>();
    }

    public class RecordingSession
    {
        public string SessionId { get; set; }
        public string SolarSystemName { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public Dictionary<string, List<string>> PlanetRockets { get; set; } = new Dictionary<string, List<string>>();
    }
    public static class RecordGame
    {
        private static Dictionary<string, RocketBlueprint> _rocketBlueprints = new Dictionary<string, RocketBlueprint>();
        private static List<RocketChange> _rocketChanges = new List<RocketChange>();
        private static Dictionary<string, Dictionary<string, object>> _lastKnownStates = new Dictionary<string, Dictionary<string, object>>();
        private static RecordingSession _currentSession;
        private static bool _isRecording = false;
        private static string _recordingBasePath;
        private static bool _wasPaused = false;
        private static double _originalTimeScale = 1.0;
        public static double _lastAutoSave = 0;
        public static readonly double AUTO_SAVE_INTERVAL = 10.0; // Save every 10 seconds

        public static void StartRecording()
        {
            // Pause the world to safely capture initial state
            PauseWorld();

            _isRecording = true;
            _rocketBlueprints.Clear();
            _rocketChanges.Clear();
            _lastKnownStates.Clear();

            // Create new recording session
            _currentSession = new RecordingSession
            {
                SessionId = System.Guid.NewGuid().ToString(),
                StartTime = Time.timeAsDouble,
                SolarSystemName = GetCurrentSolarSystemName()
            };

            // Setup recording directory structure
            string baseRecordingsPath = GetRecordingsFolderPath();
            _recordingBasePath = Path.Combine(baseRecordingsPath, $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}");
            
            Debug.Log($"Creating recording folders at: {_recordingBasePath}");
            Directory.CreateDirectory(_recordingBasePath);
            Directory.CreateDirectory(Path.Combine(_recordingBasePath, "Blueprints"));
            Directory.CreateDirectory(Path.Combine(_recordingBasePath, "Changes"));

            Debug.Log($"Recording started: {_currentSession.SessionId}");
            Debug.Log($"Base recordings path: {baseRecordingsPath}");
            Debug.Log($"Recording path: {_recordingBasePath}");

            // Immediately try to record any existing rockets while paused
            try
            {
                var rockets = UnityEngine.Object.FindObjectsOfType<Rocket>();
                Debug.Log($"Found {rockets.Length} rockets at recording start");
                foreach (var rocket in rockets)
                {
                    if (rocket != null && rocket.gameObject.activeInHierarchy)
                    {
                        RecordRocket(rocket);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to record initial rockets: {ex.Message}");
            }

            // Resume world after initial capture
            ResumeWorld();
        }

        public static void StopRecording()
        {
            if (!_isRecording) return;

            // Pause world to safely save all data
            PauseWorld();

            _isRecording = false;
            _currentSession.EndTime = Time.timeAsDouble;

            Debug.Log($"Stopping recording. Blueprints: {_rocketBlueprints.Count}, Changes: {_rocketChanges.Count}");

            try
            {
                // Force save all remaining data
                SaveRecordingSession();
                SaveAllBlueprints();
                SaveAllChanges();

                Debug.Log($"Recording stopped: {_currentSession.SessionId}");
                Debug.Log($"Files saved to: {_recordingBasePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving recording data: {ex.Message}");
            }
            finally
            {
                // Always resume world even if saving failed
                ResumeWorld();
            }
        }

        private static void PauseWorld()
        {
            try
            {
                // Store current time scale
                _originalTimeScale = Time.timeScale;
                
                // Pause Unity time scale
                Time.timeScale = 0f;
                
                Debug.Log("World paused for recording operation");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to pause world: {ex.Message}");
            }
        }

        private static void ResumeWorld()
        {
            try
            {
                // Restore Unity time scale
                Time.timeScale = (float)_originalTimeScale;
                
                Debug.Log("World resumed after recording operation");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to resume world: {ex.Message}");
            }
        }

        public static void RecordRocket(Rocket rocket)
        {
            if (!_isRecording || rocket == null) return;

            try
            {
                // Pause briefly to capture consistent state
                var originalTimeScale = Time.timeScale;
                Time.timeScale = 0f;

                string rocketId = GetRocketId(rocket);
                string planetName = GetRocketPlanetName(rocket);
                var currentState = RocketState.FromRocket(rocket);

                Debug.Log($"Recording rocket: {rocketId} on planet: {planetName} with {currentState.Properties.Count} properties");

                // Check if this is a new rocket (create blueprint)
                if (!_rocketBlueprints.ContainsKey(rocketId))
                {
                    CreateRocketBlueprint(rocket, rocketId, currentState);
                    
                    // Add rocket to planet tracking
                    if (!_currentSession.PlanetRockets.ContainsKey(planetName))
                    {
                        _currentSession.PlanetRockets[planetName] = new List<string>();
                    }
                    _currentSession.PlanetRockets[planetName].Add(rocketId);
                }

                // Record changes from last known state
                RecordRocketChanges(rocketId, currentState);

                // Restore time scale
                Time.timeScale = originalTimeScale;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recording rocket: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void CreateRocketBlueprint(Rocket rocket, string rocketId, RocketState currentState)
        {
            try
            {
                var blueprint = new RocketBlueprint
                {
                    RocketId = rocketId,
                    BlueprintName = rocket.rocketName ?? $"Rocket_{rocketId}",
                    BlueprintData = SerializeRocketBlueprint(rocket),
                    BaseProperties = new Dictionary<string, object>(currentState.Properties),
                    CreatedAt = Time.timeAsDouble
                };

                _rocketBlueprints[rocketId] = blueprint;
                _lastKnownStates[rocketId] = new Dictionary<string, object>(currentState.Properties);

                // Save blueprint immediately
                SaveBlueprintToFile(blueprint);

                Debug.Log($"Created blueprint for rocket: {blueprint.BlueprintName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error creating blueprint: {ex.Message}");
            }
        }

        private static void RecordRocketChanges(string rocketId, RocketState currentState)
        {
            try
            {
                if (!_lastKnownStates.ContainsKey(rocketId)) return;

                var lastState = _lastKnownStates[rocketId];
                var changes = new Dictionary<string, object>();

                // Compare current state with last known state
                foreach (var kvp in currentState.Properties)
                {
                    if (!lastState.ContainsKey(kvp.Key) || !AreEqual(lastState[kvp.Key], kvp.Value))
                    {
                        changes[kvp.Key] = kvp.Value;
                    }
                }

                // Only record if there are actual changes
                if (changes.Count > 0)
                {
                    var rocketChange = new RocketChange
                    {
                        RocketId = rocketId,
                        TimeStamp = currentState.TimeStamp,
                        ChangedProperties = changes
                    };

                    _rocketChanges.Add(rocketChange);

                    // Update last known state
                    foreach (var kvp in changes)
                    {
                        _lastKnownStates[rocketId][kvp.Key] = kvp.Value;
                    }

                    // Save changes periodically (every 100 changes)
                    if (_rocketChanges.Count % 100 == 0)
                    {
                        SaveChangesToFile();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recording changes: {ex.Message}");
            }
        }

        private static string SerializeRocketBlueprint(Rocket rocket)
        {
            try
            {
                // Create a simplified blueprint data structure
                var blueprintData = new
                {
                    RocketName = rocket.rocketName,
                    PartCount = rocket.partHolder.parts.Count,
                    Parts = rocket.partHolder.parts.Select(part => new
                    {
                        Name = part.name,
                        Position = part.transform.localPosition,
                        Rotation = part.transform.localRotation,
                        Modules = GetPartModules(part)
                    }).ToArray()
                };

                return SimpleJsonSerializer.Serialize(blueprintData);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error serializing blueprint: {ex.Message}");
                return "{}";
            }
        }

        private static object[] GetPartModules(Part part)
        {
            try
            {
                var modules = new List<object>();
                var moduleTypes = new[]
                {
                    typeof(SFS.Parts.Modules.EngineModule),
                    typeof(SFS.Parts.Modules.RcsModule),
                    typeof(SFS.Parts.Modules.ResourceModule),
                    typeof(SFS.Parts.Modules.ControlModule)
                };

                foreach (var moduleType in moduleTypes)
                {
                    var moduleComponents = part.GetComponents(moduleType);
                    foreach (var module in moduleComponents)
                    {
                        modules.Add(new { Type = moduleType.Name, Module = module });
                    }
                }

                return modules.ToArray();
            }
            catch
            {
                return new object[0];
            }
        }

        private static void SaveBlueprintToFile(RocketBlueprint blueprint)
        {
            try
            {
                string filePath = Path.Combine(_recordingBasePath, "Blueprints", $"{blueprint.RocketId}.json");
                string json = SimpleJsonSerializer.Serialize(blueprint);
                File.WriteAllText(filePath, json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving blueprint: {ex.Message}");
            }
        }

        private static void SaveChangesToFile()
        {
            try
            {
                string filePath = Path.Combine(_recordingBasePath, "Changes", $"changes_{System.DateTime.Now:HHmmss}.json");
                string json = SimpleJsonSerializer.Serialize(_rocketChanges);
                File.WriteAllText(filePath, json);
                _rocketChanges.Clear(); // Clear after saving
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving changes: {ex.Message}");
            }
        }

        private static void SaveRecordingSession()
        {
            try
            {
                string filePath = Path.Combine(_recordingBasePath, "session.json");
                string json = SimpleJsonSerializer.Serialize(_currentSession);
                File.WriteAllText(filePath, json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving session: {ex.Message}");
            }
        }

        private static void SaveAllBlueprints()
        {
            foreach (var blueprint in _rocketBlueprints.Values)
            {
                SaveBlueprintToFile(blueprint);
            }
        }

        private static void SaveAllChanges()
        {
            if (_rocketChanges.Count > 0)
            {
                SaveChangesToFile();
            }
        }

        private static string GetRocketId(Rocket rocket)
        {
            // Create a unique ID based on rocket structure and name
            var parts = rocket.partHolder.parts;
            var partSignature = string.Join(",", parts.Select(p => p.name).OrderBy(x => x));
            var hash = partSignature.GetHashCode();
            return $"{rocket.rocketName ?? "Unknown"}_{hash:X8}";
        }

        private static bool AreEqual(object obj1, object obj2)
        {
            if (obj1 == null && obj2 == null) return true;
            if (obj1 == null || obj2 == null) return false;
            return obj1.Equals(obj2);
        }

        private static string GetCurrentSolarSystemName()
        {
            try
            {
                return "SolarSystem"; // Placeholder - actual implementation depends on game API
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetCurrentPlanetName()
        {
            try
            {
                return PlayerController.main?.player?.Value?.location?.Value?.planet?.DisplayName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetRocketPlanetName(Rocket rocket)
        {
            try
            {
                return rocket?.location?.Value?.planet?.DisplayName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetRecordingsFolderPath()
        {
            try
            {
                return Settings.RecordingsFolderPath;
            }
            catch
            {
                return Path.GetTempPath();
            }
        }

        // Public methods for external control
        public static bool IsRecording => _isRecording;
        public static RecordingSession CurrentSession => _currentSession;
        public static int BlueprintCount => _rocketBlueprints.Count;
        public static int ChangeCount => _rocketChanges.Count;
        public static void AutoSave()
        {
            if (!_isRecording) return;

            try
            {
                Debug.Log("Performing auto-save...");
                
                // Save current session state
                SaveRecordingSession();
                
                // Save any pending changes
                if (_rocketChanges.Count > 0)
                {
                    SaveChangesToFile();
                }
                
                Debug.Log($"Auto-save completed. Blueprints: {_rocketBlueprints.Count}, Changes saved.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Auto-save failed: {ex.Message}");
            }
        }
    }
}

// Harmony patches to automatically record rockets during gameplay
namespace replay
{
    [HarmonyPatch(typeof(UnityEngine.MonoBehaviour), "Update")]
    public static class UniversalRecordingPatch
    {
        private static int _frameCount = 0;

        [HarmonyPostfix]
        public static void Postfix(MonoBehaviour __instance)
        {
            if (!RecordGame.IsRecording) return;
            
            // Only process on GameManager instances to avoid performance issues
            if (!(__instance is SFS.World.GameManager)) return;

            _frameCount++;
            
            // Only run every 10 frames to avoid performance issues
            if (_frameCount % 10 != 0) return;

            try
            {
                // Find and record all rockets
                var rockets = UnityEngine.Object.FindObjectsOfType<Rocket>();
                foreach (var rocket in rockets)
                {
                    if (rocket != null && rocket.gameObject.activeInHierarchy)
                    {
                        RecordGame.RecordRocket(rocket);
                    }
                }

                // Auto-save periodically
                if (_frameCount % 600 == 0) // Every 60 seconds at 10fps
                {
                    double currentTime = Time.timeAsDouble;
                    if (currentTime - RecordGame._lastAutoSave > RecordGame.AUTO_SAVE_INTERVAL)
                    {
                        RecordGame.AutoSave();
                        RecordGame._lastAutoSave = currentTime;
                    }
                    
                    Debug.Log($"Recording frame {_frameCount} - Blueprints: {RecordGame.BlueprintCount}, Changes: {RecordGame.ChangeCount}");
                }
            }
            catch (System.Exception ex)
            {
                if (_frameCount % 600 == 0) // Log errors less frequently
                {
                    Debug.LogError($"Error in recording patch: {ex.Message}");
                }
            }
        }
    }
}
