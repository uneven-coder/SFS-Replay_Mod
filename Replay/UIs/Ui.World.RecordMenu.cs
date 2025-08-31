using SFS.UI;
using SFS.Input;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using SFS.World;
using static replay.RecordGame;
using static SFS.UI.ElementGenerator;
using static SFS.UI.ButtonBuilder;
using static SFS.UI.TextBuilder;
using System.Runtime.CompilerServices;
using ModLoader.Helpers;

namespace replay
{
public class WorldRecordMenuUI : SFSUiBase
{
    public WorldRecordMenuUI()
    {   // Initialize UI and subscribe to scene changes
        OnSceneChanged += HandleSceneChange;
    }

    private void HandleSceneChange(Scene scene)
    {   // Handle recording state when scene changes
        if (IsRecording())
        {
            Debug.Log("Scene changed while recording. Opening end menu to handle save/discard.");
            Debug.Log("New Scene: " + scene.name);
            GameUiPatch.ShowRecordingEndMenu(null);
        }
    }

        public static void ShowConfirmMenu(string title, string message, System.Action onConfirm, System.Action onCancel = null,
                                string confirmText = "Confirm", string cancelText = "Cancel")
    {   // Display a confirmation dialog with custom title, message, and button text
        ScreenManager.main.OpenScreen(MenuGenerator.CreateMenu(CancelButton.Close, CloseMode.Current, null, null, new MenuElement[]
        {
                CreateText(() => title),
                VerticalSpace(12),
                CreateText(() => message),
                VerticalSpace(20),
                CreateButton(null, () => confirmText, () => { ScreenManager.main.CloseCurrent(); onConfirm?.Invoke(); }, CloseMode.None),
                // VerticalSpace(4),
                CreateButton(null, () => cancelText, () => { ScreenManager.main.CloseCurrent(); onCancel?.Invoke(); }, CloseMode.None)
        }));
    }

        public static void ShowRecordingStoppedConfirm(System.Action onConfirm, System.Action onCancel = null) =>
            ShowConfirmMenu("Stop Recording", "Are you sure you want to stop the current recording?", 
                         () => GameUiPatch.ShowRecordingEndMenu(onConfirm), onCancel);

        

        [HarmonyPatch]
        public class Patches
        {   // Single interceptor dynamically targeting multiple GameManager methods

            private static readonly System.Collections.Generic.Dictionary<string, System.Func<GameManager, object[], System.Action>> methodMap =
                new System.Collections.Generic.Dictionary<string, System.Func<GameManager, object[], System.Action>>
                {   // Map method names to their invocation logic
                    ["RevertToLaunch"] = (inst, args) => () => inst.RevertToLaunch(args?.Length > 0 && (bool)args[0]),
                    ["RevertToBuild"] = (inst, args) => () => inst.RevertToBuild(args?.Length > 0 && (bool)args[0]),
                    ["ExitToBuild"] = (inst, args) => () => inst.ExitToBuild(),
                    ["ExitToHub"] = (inst, args) => () => inst.ExitToHub(),
                    ["ExitToMainMenu"] = (inst, args) => () => inst.ExitToMainMenu()
                };

            [HarmonyTargetMethods]
            static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {   // Target all GameManager methods we want to intercept
                var gameManagerType = typeof(SFS.World.GameManager);
                foreach (var methodName in methodMap.Keys)
                {
                    var method = gameManagerType.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                        yield return method;
                }
            }

            [HarmonyPrefix]
            public static bool UnifiedGameManagerPrefix(GameManager __instance, MethodBase __originalMethod, object[] __args)
            {   // Intercept GameManager methods and show confirmation if recording
                if (methodMap.TryGetValue(__originalMethod.Name, out var factory))
                    return !CheckRecordingAndConfirm(factory(__instance, __args));
                
                return true;  // Allow method to proceed if not in our map
            }
        }




        public static bool CheckRecordingAndConfirm(System.Action onProceed)
        {   // Show confirmation if recording; block original and run after save/discard
            if (!IsRecording()) 
                return false;  // Not recording, allow original method to proceed

            ShowConfirmMenu(
                "Recording in progress",
                "You are currently recording. Stop now to save or discard before proceeding?",
                () => { GameUiPatch.ShowRecordingEndMenu(onProceed); },
                null,
                "End Recording and Exit",
                "Continue Recording"
            );

            return true;  // Block original method execution
        }

            public static bool IsRecording() =>
                CurrentRecordingState.IsRecording;  // Query current recording state
        
    }
}
