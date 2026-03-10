using BenScr.MinecraftClone;
using System;
using UnityEngine;

public class CanvasScreenManager : MonoBehaviour
{
    public static GameObject activeScreen;
    public static CanvasScreenManager instance;

    public static Action<GameObject> OnOpenScreen;
    public static Action<GameObject> OnCloseScreen;

    private void Awake()
    {
        activeScreen = null;
        instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseActiveScreen();
        }
    }

    public bool OpenScreen(GameObject screen)
    {
        if (activeScreen != null) return false;

        CloseActiveScreen();
        activeScreen = screen;
        screen.SetActive(true);
        InventoryManager.instance.selectedBarSlotImage.gameObject.SetActive(false);
        //WorldUIManager.instance.bgAnimator.SetBool("Active", true);
        OnOpenScreen?.Invoke(activeScreen);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public void CloseActiveScreen()
    {
        if (activeScreen != null)
        {
            activeScreen.SetActive(false);
            InventoryManager.instance.selectedBarSlotImage.gameObject.SetActive(true);
            //WorldUIManager.instance.bgAnimator.SetBool("Active", false);
            OnCloseScreen?.Invoke(activeScreen);
            activeScreen = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}