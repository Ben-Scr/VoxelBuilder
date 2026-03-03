using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BenScr.CubeDash
{
    public enum SceneType
    {
        Persistent = 0,
        Menu = 1,
        Game = 2,
        Editor = 3,
        Debug = 4
    }

    public class LoadSceneManager : MonoBehaviour
    {
        public const string PERSISTENT_SCENE = "Persistent";
        public const string MENU_SCENE = "Menu";
        public const string GAME_SCENE = "Game";
        public const string EDITOR_SCENE = "Editor";

        public static Action OnLoadScene;
        public static Action<SceneType> BeforeUnloadScene;
        public static SceneType activeScene = SceneType.Persistent;
        public static SceneType lastActiveScene = SceneType.Persistent;

        private void Awake()
        {
            OnInit();
        }

        private void OnInit()
        {
            if (!IsSceneLoaded(MENU_SCENE))
            {
                LoadSceneAsyncAdditive(MENU_SCENE);
            }
        }


        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (scene.buildIndex == 0) return;
        }
        public static async Task UnLoadAndLoadScene(SceneType unload, SceneType load)
        {
            BeforeUnloadScene?.Invoke(unload);
            UnloadSceneAsync(unload.ToString());
            await LoadSceneAsyncAdditive(load.ToString());
        }

        public static void UnloadSceneAsync(string name)
        {
            SceneManager.UnloadSceneAsync(name);
        }

        public static async Task LoadSceneAsyncAdditive(string name)
        {
            await SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
        }

        public static bool Check()
        {
            if (!IsSceneLoaded(PERSISTENT_SCENE))
            {
                SceneManager.LoadScene(PERSISTENT_SCENE);
                return false;
            }

            return true;
        }

        internal static bool IsSceneLoaded(string name)
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
