using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using ModLoader.Helpers;
using UnityEngine;

namespace replay
{
    public abstract class SFSUiBase
    {   // Abstract base for all UI classes providing unified event handling and scene management
        private static readonly List<SFSUiBase> registeredUIs = new List<SFSUiBase>();
        private static bool isSceneHandlerInitialized = false;

        public event Action<Scene> OnSceneChanged;
        public event Action OnUIStateChanged;

        protected SFSUiBase()
        {   // Register this UI instance and ensure scene handler is initialized
            registeredUIs.Add(this);
            InitializeSceneHandler();
        }

        private static void InitializeSceneHandler()
        {   // Set up scene change handler once for all UI instances
            if (isSceneHandlerInitialized) return;

            SceneHelper.OnSceneLoaded += (scene) =>
            {   // Notify all registered UI instances of scene changes
                foreach (var ui in registeredUIs)
                    ui.OnSceneChanged?.Invoke(scene);
            };

            isSceneHandlerInitialized = true;
        }

        protected void NotifyStateChanged()
        {   // Trigger state change event for this UI instance
            OnUIStateChanged?.Invoke();
        }

        protected virtual void OnSceneLoad(Scene scene)
        {   // Override in derived classes to handle scene-specific logic
            // Default implementation does nothing
        }

        public static void UnregisterUI(SFSUiBase ui)
        {   // Remove UI from registration list when no longer needed
            registeredUIs.Remove(ui);
        }

        protected virtual void Dispose()
        {   // Clean up UI instance and remove from registration
            UnregisterUI(this);
            OnSceneChanged = null;
            OnUIStateChanged = null;
        }
    }
}