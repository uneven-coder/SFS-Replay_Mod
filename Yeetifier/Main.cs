using System;
using HarmonyLib;
using ModLoader;
using ModLoader.Helpers;
using SFS.UI;
using SFS.World;
using SFS.Utilities;
using UnityEngine;
using System.IO;
using SFS.IO;
using SFS.Input;
using System.Collections.Generic;
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



    [HarmonyPatch(typeof(GameManager), "Start")]
    public class GameManagerReplayListenerPatch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called via Harmony reflection")]
        private static void Postfix()
        {
            GameManager.AddOnKeyDown(KeybindingsPC.keys.Close_Menu, GameManager.main.OpenMenu);
        }
    }

    [HarmonyPatch(typeof(MenuGenerator), "OpenMenu")]
    public static class MenuGenerator_OpenMenu_Listener
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called via Harmony reflection")]
        static void Postfix(ref MenuElement[] elements)
        {
            Debug.Log("Menu opened, adding Replay button dynamically.");

            // Get size syncer (fallback in case needed)
            new SizeSyncerBuilder(out var sizeSync).HorizontalMode(SizeMode.MaxChildSize);

            // Create the "Start Recording" button
            MenuElement recordButton = ButtonBuilder.CreateIconButton(
                sizeSync,
                ResourcesLoader.main.buttonIcons.cheats,
                () => "Start Recording",
                new Action(OpenReplayUI),
                CloseMode.None
            );

            if (recordButton == null)
            {
                Debug.LogError("Failed to create Start Recording button.");
                return;
            }

            // Insert the new button at the beginning of the array
            List<MenuElement> newList = new List<MenuElement>(elements);
            newList.Insert(0, recordButton);
            elements = newList.ToArray();

                        GameObject menuHolder = MenuGenerator.OpenMenu();
            if (menuHolder != null)
            {
                RectTransform rectTransform = menuHolder.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(800, 600); // Example size adjustment
                    Debug.Log("Menu size adjusted to 800x600.");
                }
            }
        }

        public static void OpenReplayUI()
        {
            Debug.Log("Opening Replay UI");
            List<MenuElement> replayUIElements = new List<MenuElement>();
            new SizeSyncerBuilder(out var sizeSync).HorizontalMode(SizeMode.MaxChildSize);

            MenuElement replayUIHeader = ButtonBuilder.CreateButton(
                sizeSync,
                () => "Replay UI Header",
                null,
                CloseMode.None
            );

            if (replayUIHeader == null)
            {
                Debug.LogError("Failed to create Replay UI Header.");
                return;
            }

            replayUIElements.Add(replayUIHeader);
            MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, replayUIElements.ToArray());
        }
    }


}