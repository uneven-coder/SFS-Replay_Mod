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


namespace replay
{

    // in early load,
    // edit the escape menu
    // add settings
    // also manage world management like on world load, world unload
    // also create a new scene for the replay

    // also get when the game is paused or not
    // create a new ui menu not builder menu to save the replays
    // change the builder ui to a button in the main menu



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

        public override void Load()
        {
            UiPatches.Initialize();
        }
    }    [HarmonyPatch(typeof(MenuGenerator), "OpenMenu")]
    public static class MenuGeneratorOpenMenuPatch
    {
        [HarmonyPrefix]
        public static void PrefixOpenMenu(ref MenuElement[] elements)
        {
            // Create a new list with existing elements plus our recording button
            List<MenuElement> elementsList = new List<MenuElement>(elements);
            
            // Create the recording button
            var recordButton = ButtonBuilder.CreateButton(null, () => "Start Recording", () =>
            {
                Debug.Log("Start Recording button clicked");
                // Add your recording start logic here
            }, CloseMode.None);

            // Add the recording button to the elements list
            elementsList.Add(recordButton);
            
            // Update the elements array to include our button
            elements = elementsList.ToArray();
            
            Debug.Log($"Recording button added to menu elements array. Total elements: {elements.Length}");
        }
    }

    public static class Settings
    {
        // public static string extend = Path.Combine(Application.persistentDataPath, "SFS_Saves");
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
        {
            get
            {
                return FileLocations.BaseFolder.Extend((Application.isMobilePlatform || Application.isEditor) ? "Saving" : "/../Saving");
            }
        }
    }


}