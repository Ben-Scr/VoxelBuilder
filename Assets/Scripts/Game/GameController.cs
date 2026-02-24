using TMPro;
using UnityEngine;

namespace BenScr.MinecraftClone
{
    public class GameController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI fpsTxt;
        [SerializeField] private TextMeshProUGUI playerPosTxt;
        [SerializeField] private int targetFPS = -1;
        [SerializeField] private PlayerController player;
        [SerializeField] private GameObject loadingTerrainScreen;
        [SerializeField] private GameObject playerUI;

        private void Awake()
        {
            Application.targetFrameRate = targetFPS < 0 ? QualitySettings.vSyncCount : targetFPS;
        }
        private void Update()
        {
            fpsTxt.text = "FPS: " + (1f / Time.unscaledDeltaTime).ToString("0");
            Vector3 playerPos = player.transform.position;
            playerPosTxt.text = $"X: {playerPos.x.ToString("0")} Y: {playerPos.y.ToString("0")} Z: {playerPos.z.ToString("0")}";
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
}