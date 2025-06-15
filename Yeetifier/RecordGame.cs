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


namespace replay
{
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

    public class RecordGame
    {
    }
}