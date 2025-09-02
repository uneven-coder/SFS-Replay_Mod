using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using ModLoader.Helpers;

namespace replay
{
    public abstract class SFSUiBase
    {   // UI base providing scene change notifications
        private static readonly List<SFSUiBase> registeredUIs = new List<SFSUiBase>();
        private static bool isInitialized = false;

        public event Action<Scene> OnSceneChanged;

        protected SFSUiBase()
        {   // Register instance and initialize scene handler once
            registeredUIs.Add(this);
            
            if (!isInitialized)
            {   // Hook scene changes once for all instances
                SceneHelper.OnSceneLoaded += NotifyAllInstances;
                isInitialized = true;
            }
        }

        private static void NotifyAllInstances(Scene scene)
        {   // Efficiently notify all registered UI instances
            for (int i = registeredUIs.Count - 1; i >= 0; i--)
                registeredUIs[i].OnSceneChanged?.Invoke(scene);
        }

        protected virtual void Dispose()
        {   // Clean up instance
            registeredUIs.Remove(this);
            OnSceneChanged = null;
        }
    }
}