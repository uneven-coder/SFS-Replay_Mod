using SFS.UI;
using SFS.Input;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using SFS.World;
using static replay.RecordGame;  // direct access to recording state

namespace replay
{
    public static class WorldRecordMenuUI
    {
        public static void ShowConfirmUI(string title, string message, System.Action onConfirm, System.Action onCancel = null)
        {   // Display a confirmation dialog with custom title and message
            var confirmElements = new MenuElement[]
            {
                TextBuilder.CreateText(() => title),
                ElementGenerator.VerticalSpace(12),
                TextBuilder.CreateText(() => message),
                ElementGenerator.VerticalSpace(20),
                ElementGenerator.VerticalSpace(8),
                ButtonBuilder.CreateButton(null, () => "Confirm", () => { ScreenManager.main.CloseCurrent(); onConfirm?.Invoke(); }, CloseMode.None),
                ElementGenerator.VerticalSpace(8),
                ButtonBuilder.CreateButton(null, () => "Cancel", () => { ScreenManager.main.CloseCurrent(); onCancel?.Invoke(); }, CloseMode.None)
            };

            var confirmScreen = MenuGenerator.CreateMenu(CancelButton.Close, SFS.Input.CloseMode.Current, null, null, confirmElements);
            ScreenManager.main.OpenScreen(confirmScreen);
        }

        public static void ShowRecordingStoppedConfirm(System.Action onConfirm, System.Action onCancel = null) =>
            ShowConfirmUI("Stop Recording", "Are you sure you want to stop the current recording?", 
                         () => GameUiPatch.ShowRecordingEndMenu(onConfirm), onCancel);

        public static void ShowRecordingStartConfirm(System.Action onConfirm, System.Action onCancel = null)
        {   // Ask user to confirm starting a new recording
            ShowConfirmUI(
                "Start Recording",
                "Are you sure you want to start a new recording?",
                onConfirm,
                onCancel
            );
        }

        // Unified Harmony patch for all GameManager navigation methods
        [HarmonyPatch]
        public static class HarmonyPatches
        {   // Single interceptor dynamically targeting multiple GameManager methods
            
            // static MethodBase[] TargetMethods() =>
            //     new MethodBase[] { typeof(GameManager).GetMethod("RevertToLaunch"), typeof(GameManager).GetMethod("RevertToBuild"), 
            //                      typeof(GameManager).GetMethod("ExitToBuild"), typeof(GameManager).GetMethod("ExitToHub"), 
            //                      typeof(GameManager).GetMethod("ExitToMainMenu") };

            private static readonly System.Collections.Generic.Dictionary<string, System.Func<GameManager, object[], System.Action>> methodMap =
                new System.Collections.Generic.Dictionary<string, System.Func<GameManager, object[], System.Action>>
                {   // Map method names to their invocation logic
                    ["RevertToLaunch"] = (inst, args) => () => inst.RevertToLaunch(args?.Length > 0 && (bool)args[0]),
                    ["RevertToBuild"] = (inst, args) => () => inst.RevertToBuild(args?.Length > 0 && (bool)args[0]),
                    ["ExitToBuild"] = (inst, args) => () => inst.ExitToBuild(),
                    ["ExitToHub"] = (inst, args) => () => inst.ExitToHub(),
                    ["ExitToMainMenu"] = (inst, args) => () => inst.ExitToMainMenu()
                };

            [HarmonyPrefix]
            public static bool UnifiedGameManagerPrefix(GameManager __instance, MethodBase __originalMethod, object[] __args) =>
                !methodMap.TryGetValue(__originalMethod.Name, out var factory) || 
                !RecordingPatchHelpers.CheckRecordingAndConfirm(factory(__instance, __args));
        }

        // Scene change safety: open save/discard menu if scene changes
        [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "Internal_SceneLoaded")]
        public static class SceneChangeRecordingPatch
        {
            [HarmonyPostfix]
            public static void OnSceneLoaded()
            {
                if (CurrentRecordingState.IsRecording)
                {
                    Debug.Log("Scene changed while recording. Opening end menu to handle save/discard.");
                    GameUiPatch.ShowRecordingEndMenu(null);
                }
            }
        }

        // Helper nested class for recording state and stop logic
        private static class RecordingPatchHelpers
        {   // Centralize recording checks and confirmation UI
            public static bool CheckRecordingAndConfirm(System.Action onProceed)
            {   // Show confirmation if recording; block original and run after save/discard
                if (!IsRecording()) return false;

                WorldRecordMenuUI.ShowConfirmUI(
                    "Recording in progress",
                    "You are currently recording. Stop now to save or discard before proceeding?",
                    () => { GameUiPatch.ShowRecordingEndMenu(onProceed); },
                    null
                );

                return true;
            }

            public static bool IsRecording() =>
                CurrentRecordingState.IsRecording;  // Query current recording state
        }
    }
}
