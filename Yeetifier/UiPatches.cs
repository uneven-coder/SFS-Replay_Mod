using System;
using System.Collections.Generic;
using HarmonyLib;
using SFS.Builds;
using SFS.UI;
using SFS.UI.ModGUI;
using SFS.World;
using SFS.WorldBase;
using static SFS.Base;
using UnityEngine;
using SFS.Translations;
using UITools;
using SFS.Input;


namespace replay
{
    public static class UiPatches
    {
        public static bool isRecording = false;
        private static Window replayWindow;
        private static SFS.UI.ModGUI.Button recordingButton;
        private static readonly int windowID = Builder.GetRandomID();

        public static void Initialize()
        {
            Debug.Log("Replay Mod UI Patches Initialized");
            // CreateReplayWindow();
            // CreateWorldWindow();
        }

        public static void Cleanup()
        {   // remove the ui fully
            Debug.Log("Replay Mod UI Patches Cleaned up");
        }



        private static string GetRecordingButtonText()
        {
            return isRecording ? "Stop Recording" : "Start Recording";
        }

        public static void ToggleRecording()
        {
            if (isRecording)
                StopRecording();
            else
                StartRecording();



            {
                if (recordingButton != null)
                    recordingButton.Text = isRecording ? "Stop Recording" : "Start Recording";
            }


            void StartRecording()
            {
                isRecording = true;
                Debug.Log("Started recording replay");
                // Recording implementation will go here
            }

            void StopRecording()
            {
                isRecording = false;
                Debug.Log("Stopped recording replay");
                // Stop implementation will go here
            }
        }


        private static void CreateReplayWindow()
        {
            if (replayWindow != null)
            {
                return;
            }

            var holder = Builder.CreateHolder(Builder.SceneToAttach.BaseScene, "Replay Mod Window");
            replayWindow = Builder.CreateWindow(holder.transform, windowID, 450, 220, 0, -500, true, true, 0.95f, "Replay Mod");
            replayWindow.CreateLayoutGroup(SFS.UI.ModGUI.Type.Vertical, TextAnchor.UpperCenter, 10f, null, true);

            // Create recording button
            recordingButton = Builder.CreateButton(replayWindow, 330, 60, 0, 0, ToggleRecording, GetRecordingButtonText());

            // Create status label
            Builder.CreateLabel(replayWindow, 370, 100, 0, 0, "Click to start/stop recording");
        }



        private static void DestroyReplayWindow()
        {
            if (replayWindow != null)
            {
                UnityEngine.Object.Destroy(replayWindow.gameObject.transform.parent.gameObject);
                replayWindow = null;
                recordingButton = null;
            }
        }

        public static void ShowReplayWindow()
        {
            if (replayWindow == null)
            {
                CreateReplayWindow();
            }
            replayWindow.gameObject.SetActive(true);
            Debug.Log("Replay window shown");
        }

        public static void HideReplayWindow()
        {
            if (replayWindow != null)
            {
                replayWindow.gameObject.SetActive(false);
                Debug.Log("Replay window hidden");
            }
        }
    }

    // [HarmonyPatch(typeof(GameManager), "OpenMenu")]
    // class GameMenuPatch
    // {
    //     private static void Postfix()
    //     {
    //         UiPatches.ShowReplayWindow();
    //         Debug.Log("Replay window shown when game menu opened");
    //     }
    // }



    // [HarmonyPatch(typeof(GameManager), "OpenMenu")]
    // public static class GameMenuPatch
    // {
    //     private static void Postfix()
    //     {
    //         UiPatches.ShowReplayWindow();
    //         Debug.Log("Replay window created when menu opened");
    //     }
    // }
}