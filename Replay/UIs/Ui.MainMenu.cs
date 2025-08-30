using HarmonyLib;
using ModLoader;
using UnityEngine;
using System.IO;
using SFS.IO;
using System.Collections.Generic;
using SFS.UI;
using SFS.Input;
using System;
using SFS.Parsers.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using SFS.World;
using SFS.Translations;

namespace replay
{
    public static class MainMenuUI
    {
        public static void HomeManagerAlert()
        {   // Display first-time user alert with mod warnings and information
            if (Settings.CurrentSettings.HasSeenInfoMenu)
                return;

            // Create alert menu with messages
            string[] alertMessages = {
                "<color=yellow>Welcome to the Replay Mod!</color>",
                "<color=red>Some Warnings</color>",
                "This mod is in <color=orange>beta</color> and may cause issues.",
                "- Do not delete solar systems",
                "       they are required to replay recordings.",
                "- large file sizes",
                "- Save loss is unlikely",
                "Press Escape to close this message."
            };

            var alertElements = new MenuElement[alertMessages.Length + 2];
            alertElements[0] = TextBuilder.CreateText(() => "Replay Mod Alert");
            alertElements[1] = ElementGenerator.VerticalSpace(12);

            for (int i = 0; i < alertMessages.Length; i++)
                alertElements[i + 2] = TextBuilder.CreateText(() => alertMessages[i]);

            var menuScreen = MenuGenerator.CreateMenu(CancelButton.Close, SFS.Input.CloseMode.Current, null, null, alertElements);
            ScreenManager.main.OpenScreen(menuScreen);

            // Mark alert as seen and save
            Settings.CurrentSettings.HasSeenInfoMenu = true;
            Settings.SaveSettings();

            // Configure warning text alignment and sizing
            var textAdapters = UnityEngine.Object.FindObjectsOfType<TextAdapter>();
            var warningTransforms = new List<RectTransform>();
            Vector2 largestSize = Vector2.zero;

            for (int i = 1; i <= Math.Min(5, textAdapters.Length - 1); i++)
            {   // Process warning text elements for alignment and sizing
                var adapter = textAdapters[i];
                var children = adapter.GetComponentsInChildren<RectTransform>();
                warningTransforms.AddRange(children);

                // Configure text alignment
                var textComponent = adapter.GetComponentInChildren<UnityEngine.UI.Text>();
                if (textComponent != null)
                    textComponent.alignment = TextAnchor.MiddleLeft;

                // Configure TextMeshPro alignment using reflection
                var tmpComponent = adapter.GetComponent("TextMeshProUGUI");
                if (tmpComponent != null)
                {   // Apply left alignment through reflection
                    var alignmentProperty = tmpComponent.GetType().GetProperty("alignment");
                    if (alignmentProperty?.PropertyType != null)
                    {   // Parse and set MidlineLeft alignment
                        var midlineLeftValue = System.Enum.Parse(alignmentProperty.PropertyType, "MidlineLeft");
                        alignmentProperty.SetValue(tmpComponent, midlineLeftValue);
                    }
                }

                // Track largest size for uniform resizing
                foreach (var rt in children)
                {   // Find maximum dimensions
                    largestSize.x = largestSize.x > rt.sizeDelta.x ? largestSize.x : rt.sizeDelta.x;
                    largestSize.y = largestSize.y > rt.sizeDelta.y ? largestSize.y : rt.sizeDelta.y;
                }
            }

            // Apply uniform sizing to all warning elements
            if (warningTransforms.Count > 0)
            {   // Resize all warning transforms to consistent size
                var adjustedSize = largestSize - new Vector2(40, 0);
                foreach (var rt in warningTransforms)
                    rt.sizeDelta = adjustedSize;

                Debug.Log($"Resized {warningTransforms.Count} warning RectTransforms to: {largestSize}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(HomeManager __instance)
        {
            try
            {
                Debug.Log("Adding Recording Manager button to HomeManager");
                InsertRecordingManagerButton();
                Debug.Log("Recording Manager button added successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error adding Recording Manager button: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }


        [HarmonyPatch(typeof(HomeManager), "Start")]
        private static void InsertRecordingManagerButton()
        {
            Transform buttons = GameObject.Find("Buttons").transform;
            GameObject modLoaderButton = GameObject.Find("Mod Loader Button");
            GameObject recordingButton = UnityEngine.Object.Instantiate(modLoaderButton, buttons, true);
            recordingButton.GetComponent<RectTransform>().SetSiblingIndex(modLoaderButton.GetComponent<RectTransform>().GetSiblingIndex() + 1);
            var buttonPC = recordingButton.GetComponent<ButtonPC>();
            var textAdapter = recordingButton.GetComponentInChildren<TextAdapter>();
            textAdapter.Text = "Recording Manager";
            UnityEngine.Object.Destroy(recordingButton.GetComponent<TranslationSelector>());
            recordingButton.name = "Recording Manager Button";

            buttonPC.holdEvent = new HoldUnityEvent();
            buttonPC.clickEvent = new ClickUnityEvent();
            buttonPC.clickEvent.AddListener(delegate
            {
                Debug.Log("Recording Manager button clicked");
                // Open recording manager functionality
            });
        }


    }
}