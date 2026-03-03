using BenScr.CubeDash;
using UnityEngine;

public class MenuController : MonoBehaviour
{

    public void OnClickGenerateWorld()
    {
        LoadSceneManager.UnLoadAndLoadScene(SceneType.Menu, SceneType.Game);
    }
}
