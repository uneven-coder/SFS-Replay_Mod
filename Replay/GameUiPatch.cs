using System;
using System.Collections.Generic;
using HarmonyLib;
using SFS.UI;
using SFS.UI.ModGUI;
using UnityEngine;
using SFS.Translations;
using SFS.Input;
using System.Diagnostics;
using static replay.GameUiPatch;
using static replay.RecordGame;

namespace replay
{
    public static class GameUiPatch
    {
        public static void ShowRecordingEndMenu(Action onExitAction = null)
        {
            // Capture recording state before stopping to preserve data
            var recordingData = new RecordingState
            {
                SessionId = CurrentRecordingState.SessionId,
                RecordingName = CurrentRecordingState.RecordingName,
                StartTime = CurrentRecordingState.StartTime,
                EndTime = DateTime.Now,
                SolarSystem = CurrentRecordingState.SolarSystem,
                IsRecording = false
            };

            // Stop recording but preserve the data for display
            StopRecording();
            
            TimeSpan duration = recordingData.EndTime - recordingData.StartTime;

            List<MenuElement> endMenuElements = new List<MenuElement>();

            // Title
            endMenuElements.Add(TextBuilder.CreateText(() => "Recording Complete"));
            endMenuElements.Add(ElementGenerator.VerticalSpace(12));            {   // Recording info display
                // endMenuElements.Add(TextBuilder.CreateText(() => $"Name: {recordingData.RecordingName}"));
                endMenuElements.Add(ButtonBuilder.CreateButton(null, () => recordingData.RecordingName, () =>
                {
                    string selectedName = recordingData.RecordingName;
                    Menu.textInput.Open(Loc.main.Cancel, Loc.main.Rename, delegate (string[] input)
                    {
                        recordingData.RecordingName = input[0];
                        Debug.Log($"Recording renamed to: {input[0]}");                        // Refresh the menu to show the updated name
                        ScreenManager.main.CloseCurrent();
                        ShowRecordingEndMenu(onExitAction);

                    }, CloseMode.Current, new TextInputElement[]
                    {
                        TextInputMenu.Element(string.Empty, selectedName)
                    });
                }, CloseMode.None));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Duration: {duration:hh\\:mm\\:ss}"));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Started: {recordingData.StartTime:HH:mm:ss}"));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Ended: {recordingData.EndTime:HH:mm:ss}"));
                endMenuElements.Add(ElementGenerator.VerticalSpace(10));
            }


            // Skip adding TextInput directly since it's not a MenuElement
            endMenuElements.Add(ElementGenerator.VerticalSpace(30));            // Save button
            endMenuElements.Add(ButtonBuilder.CreateButton(null, () => "Save Recording", () =>
            {
                // Update the current recording state with the potentially renamed data
                UpdateRecordingState(recordingData);
                Debug.Log($"Recording saved: {recordingData.RecordingName}");
                Debug.Log($"Duration: {duration:hh\\:mm\\:ss}");
                SaveRecording();
                UpdateRecordingState(new RecordingState());

                // Execute the exit action after saving
                onExitAction?.Invoke();
            }, CloseMode.Current));
            {   // Other options                
                endMenuElements.Add(ButtonBuilder.CreateButton(null, () => "Discard Recording", () =>
                    {
                        Debug.Log("Recording discarded");
                        // Reset recording state without saving
                        UpdateRecordingState(new RecordingState());

                        // Execute the exit action after discarding
                        onExitAction?.Invoke();
                    }, CloseMode.Current));
            }

            MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, endMenuElements.ToArray());
        }

        public static void ToggleRecording()
        {
            if (!CurrentRecordingState.IsRecording)
            {   // start recording
                CurrentRecordingState.IsRecording = true;
                CurrentRecordingState.RecordingName = "Recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                CurrentRecordingState.StartTime = DateTime.Now;
                CurrentRecordingState.EndTime = DateTime.MinValue;
                // CurrentRecordingState.RecordedEvents = new List<string>();
                Debug.Log($"Started recording: {CurrentRecordingState.RecordingName}");

                try
                {
                    if (ScreenManager.main != null)
                        ScreenManager.main.CloseCurrent();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing menu: {ex.Message}");
                }
                
                RecordGame.StartRecording();

            }            else
            {
                // Show recording end menu with proper data preservation
                ShowRecordingEndMenu();
            }
        }

        public static string GetRecordingButtonText() =>
            CurrentRecordingState.IsRecording ? "Stop Recording" : "Start Recording";


    }


    [HarmonyPatch(typeof(MenuGenerator), "OpenMenu")]
    public static class MenuGeneratorPatch
    {   // Edit the MenuGenerator becuse its easier them modigfying the GameManager directly
        // check that the function is being called from GameManager

        public static bool IsCalledFromGameManager()
        {   // includes a little extra dynamic check to see if the function is being called from GameManager
            // This checks the stack trace to see if the OpenMenu method is being called from GameManager
            var stackTrace = new StackTrace();
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var method = stackTrace.GetFrame(i).GetMethod();
                var name = method?.Name ?? "";
                var declaringType = method?.DeclaringType?.FullName ?? "";

                if ((declaringType == "SFS.World.GameManager" && name == "OpenMenu")
                    || name.Contains("DMD<SFS.World.GameManager::OpenMenu>"))
                    return true;
            }
            return false;
        }


        [HarmonyPrefix]
        public static void Prefix(ref MenuElement[] elements)
        {
            try
            {   // we use this to check if the function is being called from GameManager and if it exists
                bool isFromGameManager = IsCalledFromGameManager();
                if (isFromGameManager && elements != null)
                {
                    Debug.Log($"Adding recording button");
                    var recordButton = ButtonBuilder.CreateButton(null, () => GameUiPatch.GetRecordingButtonText(), () => {
                        Debug.Log("Recording button clicked");
                        ToggleRecording();
                    }, CloseMode.None);

                    // Add the button to the elements array
                    List<MenuElement> elementsList = new List<MenuElement>(elements)
                        {   recordButton    };
                    elements = elementsList.ToArray();

                    Debug.Log($"Recording button added. Total elements now: {elements.Length}");
                }
                else if (isFromGameManager)
                    Debug.Log("GameManager detected but elements array is null or empty");
                else
                    Debug.Log("Not called from GameManager - skipping button addition");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in MenuGeneratorPatch: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}