using HarmonyLib;
using SFS.World;
using SFS.UI;
using System;
using UnityEngine;
using static replay.RecordGame;
using static replay.GameUiPatch;
using SFS.Input;

namespace replay
{
    [HarmonyPatch(typeof(GameManager))]
    public static class GameManagerRecordingPatch
    {        // Helper to show confirmation if recording is active
        private static bool CheckRecordingAndConfirm(Action onSaveAndExit, Action onCancel = null)
        {
            if (!CurrentRecordingState.IsRecording)
                return false;

            // Show confirmation menu
            Func<string> warningText = () => "Recording in progress. Save before exiting?";
            Func<string> saveButtonText = () => "End Recording and Exit";
            // Review recording then exit
            // End Recording and Exit
            // Finish & Exit
            Func<string> cancelButtonText = () => "Cancel";
            Action onConfirm = () => {
                // Show recording end menu with the exit action passed through
                GameUiPatch.ShowRecordingEndMenu(onSaveAndExit);
            };
            Action onCancelAction = () => {
                onCancel?.Invoke();
            };
            MenuGenerator.OpenConfirmation(
                CloseMode.Current,
                warningText,
                saveButtonText,
                onConfirm,
                cancelButtonText,
                onCancelAction
            );
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("RevertToLaunch")]
        public static bool RevertToLaunch_Prefix(GameManager __instance, bool skipConfirmation)
        {
            if (CheckRecordingAndConfirm(() => __instance.RevertToLaunch(skipConfirmation)))
                return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("RevertToBuild")]
        public static bool RevertToBuild_Prefix(GameManager __instance, bool skipConfirmation)
        {
            if (CheckRecordingAndConfirm(() => __instance.RevertToBuild(skipConfirmation)))
                return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ExitToBuild")]
        public static bool ExitToBuild_Prefix(GameManager __instance)
        {
            if (CheckRecordingAndConfirm(() => __instance.ExitToBuild()))
                return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ExitToHub")]
        public static bool ExitToHub_Prefix(GameManager __instance)
        {
            if (CheckRecordingAndConfirm(() => __instance.ExitToHub()))
                return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ExitToMainMenu")]
        public static bool ExitToMainMenu_Prefix(GameManager __instance)
        {
            if (CheckRecordingAndConfirm(() => __instance.ExitToMainMenu()))
                return false;
            return true;
        }
    }

    // Scene change safety: stop recording if scene changes
    [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "Internal_SceneLoaded")]
    public static class SceneChangeRecordingPatch
    {
        [HarmonyPostfix]
        public static void OnSceneLoaded()
        {
            if (CurrentRecordingState.IsRecording)
            {
                Debug.Log("Scene changed while recording. Stopping recording to prevent data loss.");
                StopRecording();
            }
        }
    }
}