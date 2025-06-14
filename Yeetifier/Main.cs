using System;
using HarmonyLib;
using ModLoader;
using ModLoader.Helpers;
using SFS.UI;
using SFS.Translations;
using SFS.World;
using SFS.Utilities;
using UnityEngine;
using System.IO;
using SFS.IO;
using SFS.Input;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Diagnostics;
using static replay.GameUiPatch;
using UnityEngine.UI;


namespace replay
{
    public class Main : Mod
    {
        public override string ModNameID => "replayMod";
        public override string DisplayName => "Replay Mod";
        public override string Author => "Cratior";
        public override string MinimumGameVersionNecessary => "1.5.10";
        public override string ModVersion => "v1.0.0";
        public override string Description => "Records SFS Gameplay and them shows the recordings in game with camera controls.";

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            Settings.EnsureRecordingsFolderExists();
            Debug.Log("Replay Mod patches applied in Early_Load");
        }

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
                bool isFromGameManager = IsCalledFromGameManager();                if (isFromGameManager && elements != null)
                {
                    Debug.Log($"Adding recording button");
                    var recordButton = ButtonBuilder.CreateButton(null, () => GameUiPatch.GetRecordingButtonText(), () =>
                    {
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

    public static class Settings
    {
        public static string RecordingsFolderPath = SavingFolder.Extend("/Recordings");

        public static void EnsureRecordingsFolderExists()
        {
            if (!Directory.Exists(RecordingsFolderPath))
            {
                Directory.CreateDirectory(RecordingsFolderPath);
                Debug.Log($"Created recordings folder at: {RecordingsFolderPath}");
            }
        }

        private static FolderPath SavingFolder
        { get { return FileLocations.BaseFolder.Extend((Application.isMobilePlatform || Application.isEditor) ? "Saving" : "/../Saving"); }}
    }
}