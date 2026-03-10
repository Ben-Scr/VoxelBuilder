using BenScr.CubeDash;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BenScr.MinecraftClone
{
    public class GameController : MonoBehaviour
    {
        private static HashSet<FreezeReason> freezeReasons = new HashSet<FreezeReason>();

        public static bool IsFrozen => freezeReasons.Contains(FreezeReason.BGScreen);
        public static bool IsPlayerFrozen => freezeReasons.Contains(FreezeReason.BGScreen) | freezeReasons.Contains(FreezeReason.ManualCamera);

        [SerializeField] private TextMeshProUGUI fpsTxt;
        [SerializeField] private TextMeshProUGUI playerPosTxt;
        [SerializeField] private int targetFPS = -1;
        [SerializeField] private PlayerController player;
        [SerializeField] private GameObject loadingTerrainScreen;
        [SerializeField] private GameObject playerUI;

        [SerializeField] private GameObject pauseGameScreens;

        public static Action<FreezeReason> OnFreeze;
        public static Action<FreezeReason> OnUnFreeze;

        private void Awake()
        {
            Application.targetFrameRate = targetFPS < 0 ? 60 : targetFPS;
            freezeReasons.Clear();
            //LoadSceneManager.Check();
        }
        private void Update()
        {
            fpsTxt.text = "FPS: " + (1f / Time.unscaledDeltaTime).ToString("0");
            Vector3 playerPos = player.transform.position;
            playerPosTxt.text = $"X: {playerPos.x.ToString("0")} Y: {playerPos.y.ToString("0")} Z: {playerPos.z.ToString("0")}";

            if (CanvasScreenManager.activeScreen?.activeInHierarchy ?? false)
                Freeze(FreezeReason.BGScreen);
            else
                Unfreeze(FreezeReason.BGScreen);

            if (IsFrozen) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                ReloadScene();
            }
        }

        async void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void Freeze(FreezeReason reason)
        {
            if (!freezeReasons.Contains(reason))
            {
                freezeReasons.Add(reason);
                OnFreeze?.Invoke(reason);
            }
        }

        public static void Unfreeze(FreezeReason reason)
        {
            if (freezeReasons.Contains(reason))
            {
                freezeReasons.Remove(reason);
                OnUnFreeze?.Invoke(reason);
            }
        }

        private void OnEnable()
        {
            TerrainGenerator.OnLoadedTerrain += OnLoadedTerrain; 
        }
        private void OnDisable()
        {
            TerrainGenerator.OnLoadedTerrain -= OnLoadedTerrain;
        }

        private void OnLoadedTerrain()
        {
            Camera.main.gameObject.SetActive(false);
            player.gameObject.SetActive(true);
            playerUI.gameObject.SetActive(true);
            loadingTerrainScreen.gameObject.SetActive(false);
            RenderSettings.fog = true;
        }
    }

    public enum FreezeReason
    {
        Pause,
        Dialogue,
        Cutscene,
        BGScreen,
        Inventory,
        ManualCamera
    }
}