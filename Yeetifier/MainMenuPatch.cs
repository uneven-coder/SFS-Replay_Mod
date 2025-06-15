using System;
using HarmonyLib;
using SFS.UI;
using UnityEngine;
using SFS.Translations;


namespace replay
{
    public class MainMenuPatch
    {
    }

    [HarmonyPatch(typeof(HomeManager), "Start")]
    public static class HomeManagerButtonPatch
    {
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
    }
}