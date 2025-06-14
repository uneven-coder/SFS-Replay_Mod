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
    public class RecordingState
    {
        public bool IsRecording { get; set; }
        public string RecordingName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> RecordedEvents { get; set; }

        public RecordingState()
        {
            IsRecording = false;
            RecordingName = string.Empty;
            StartTime = DateTime.MinValue;
            EndTime = DateTime.MinValue;
        }
    }
    public static class GameUiPatch
    {
        public static RecordingState CurrentRecordingState { get; private set; } = new RecordingState();
        private static readonly int windowID = Builder.GetRandomID();



        private static void ShowRecordingEndMenu()
        {
            CurrentRecordingState.IsRecording = false;
            CurrentRecordingState.EndTime = DateTime.Now;

            TimeSpan duration = CurrentRecordingState.EndTime - CurrentRecordingState.StartTime;

            List<MenuElement> endMenuElements = new List<MenuElement>();

            // Title
            endMenuElements.Add(TextBuilder.CreateText(() => "Recording Complete"));
            endMenuElements.Add(ElementGenerator.VerticalSpace(12));


            {   // Recording info display
                // endMenuElements.Add(TextBuilder.CreateText(() => $"Name: {CurrentRecordingState.RecordingName}"));
                endMenuElements.Add(ButtonBuilder.CreateButton(null, () => CurrentRecordingState.RecordingName, () =>
                {
                    string selectedName = CurrentRecordingState.RecordingName;
                    Menu.textInput.Open(Loc.main.Cancel, Loc.main.Rename, delegate(string[] input)
                    {
                        CurrentRecordingState.RecordingName = input[0];
                        Debug.Log($"Recording renamed to: {input[0]}");
                        
                        // Refresh the menu to show the updated name
                        ScreenManager.main.CloseCurrent();
                        ShowRecordingEndMenu();
                        
                    }, CloseMode.Current, new TextInputElement[]
                    {
                        TextInputMenu.Element(string.Empty, selectedName)
                    });
                }, CloseMode.None));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Duration: {duration:hh\\:mm\\:ss}"));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Started: {CurrentRecordingState.StartTime:HH:mm:ss}"));
                endMenuElements.Add(TextBuilder.CreateText(() => $"Ended: {CurrentRecordingState.EndTime:HH:mm:ss}"));
                endMenuElements.Add(ElementGenerator.VerticalSpace(10));
            }

        
            // Skip adding TextInput directly since it's not a MenuElement
            endMenuElements.Add(ElementGenerator.VerticalSpace(30));            // Save button
            endMenuElements.Add(ButtonBuilder.CreateButton(null, () => "Save Recording", () =>
            {
                CurrentRecordingState.RecordingName = CurrentRecordingState.RecordingName;
                Debug.Log($"Recording saved: {CurrentRecordingState.RecordingName}");
                Debug.Log($"Duration: {duration:hh\\:mm\\:ss}");
                // TODO: Implement actual save functionality
                CurrentRecordingState = new RecordingState(); // Reset after saving
            }, CloseMode.Current));

            {   // Other options
                endMenuElements.Add(ButtonBuilder.CreateButton(null, () => "Discard Recording", () =>
                {
                    Debug.Log("Recording discarded");
                    // Reset recording state without saving
                    CurrentRecordingState = new RecordingState();
                }, CloseMode.Current));
            }

            MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, endMenuElements.ToArray());
        }

        public static void ToggleRecording()
        {
            if (!CurrentRecordingState.IsRecording)
            {
                CurrentRecordingState.IsRecording = true;
                CurrentRecordingState.RecordingName = "Recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                CurrentRecordingState.StartTime = DateTime.Now;
                CurrentRecordingState.EndTime = DateTime.MinValue;
                CurrentRecordingState.RecordedEvents = new List<string>();
                Debug.Log($"Started recording: {CurrentRecordingState.RecordingName}");
                try
                {
                    if (ScreenManager.main != null)
                    {
                        ScreenManager.main.CloseCurrent();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing menu: {ex.Message}");
                }
            }
            else
            {
                CurrentRecordingState.IsRecording = false;
                CurrentRecordingState.EndTime = DateTime.Now;
                ShowRecordingEndMenu();
            }
        }

        public static string GetRecordingButtonText() =>
            CurrentRecordingState.IsRecording ? "Stop Recording" : "Start Recording";


    }
}