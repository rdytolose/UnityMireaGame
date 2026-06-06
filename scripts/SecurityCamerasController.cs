using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SecurityCamerasController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public RawImage cameraDisplay;
    public RawImage staticOverlay;
    public Button[] cameraButtons;

    [Header("Cameras")]
    public Camera[] securityCameras;
    public int renderWidth = 1280;
    public int renderHeight = 720;

    [Header("Player scripts (auto-found if not set)")]
    public MouseLook mouseLook;
    public PlayerMovement playerMovement;
    public PlayerInteraction playerInteraction;

    [Header("Static transition")]
    public float transitionDuration = 0.6f;
    public int noiseSize = 256;

    private RenderTexture[] renderTextures;
    private Texture2D noiseTexture;
    private byte[] noiseBuffer;
    private System.Random rng = new System.Random();

    private bool panelOpen = false;
    private bool transitioning = false;
    private int currentCameraIndex = 0;
    private static SecurityCamerasController activePanelController;

    void Awake()
    {
        if (mouseLook == null) mouseLook = Object.FindFirstObjectByType<MouseLook>();
        if (playerMovement == null) playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
        if (playerInteraction == null) playerInteraction = Object.FindFirstObjectByType<PlayerInteraction>();

        renderTextures = new RenderTexture[securityCameras.Length];
        for (int i = 0; i < securityCameras.Length; i++)
        {
            if (securityCameras[i] == null)
            {
                Debug.LogError($"[SecurityCameras] securityCameras[{i}] is null — не проставлен в Inspector");
                continue;
            }
            var rt = new RenderTexture(renderWidth, renderHeight, 16);
            rt.name = "SecurityCamRT_" + i;
            renderTextures[i] = rt;
            securityCameras[i].targetTexture = rt;
            securityCameras[i].enabled = false;
        }

        noiseTexture = new Texture2D(noiseSize, noiseSize, TextureFormat.R8, false);
        noiseTexture.filterMode = FilterMode.Point;
        noiseTexture.wrapMode = TextureWrapMode.Repeat;
        noiseBuffer = new byte[noiseSize * noiseSize];
        if (staticOverlay != null)
        {
            staticOverlay.texture = noiseTexture;
            var c = staticOverlay.color; c.a = 0f; staticOverlay.color = c;
            staticOverlay.gameObject.SetActive(false);
        }

        for (int i = 0; i < cameraButtons.Length; i++)
        {
            if (cameraButtons[i] == null)
            {
                Debug.LogError($"[SecurityCameras] cameraButtons[{i}] is null — не проставлен в Inspector");
                continue;
            }
            int index = i;
            cameraButtons[i].onClick.AddListener(() =>
            {
                SwitchToCamera(index);
            });
        }

        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (PauseMenuManager.IsPaused) return;

        if (panelOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (staticOverlay != null && staticOverlay.gameObject.activeSelf)
            UpdateNoiseTexture();
    }

    public void TogglePanel()
    {
        panelOpen = !panelOpen;
        if (panel != null) panel.SetActive(panelOpen);

        if (panelOpen)
        {
            activePanelController = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetPlayerScriptsEnabled(false);

            for (int i = 0; i < securityCameras.Length; i++)
                if (securityCameras[i] != null)
                    securityCameras[i].enabled = (i == currentCameraIndex);

            if (cameraDisplay != null && renderTextures[currentCameraIndex] != null)
                cameraDisplay.texture = renderTextures[currentCameraIndex];
        }
        else
        {
            if (activePanelController == this)
                activePanelController = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerScriptsEnabled(true);

            for (int i = 0; i < securityCameras.Length; i++)
                if (securityCameras[i] != null) securityCameras[i].enabled = false;
        }
    }

    public static bool TryCloseActivePanel()
    {
        if (activePanelController == null || !activePanelController.panelOpen)
            return false;

        activePanelController.TogglePanel();
        return true;
    }

    void SetPlayerScriptsEnabled(bool value)
    {
        if (mouseLook) mouseLook.enabled = value;
        if (playerMovement) playerMovement.enabled = value;
        if (playerInteraction) playerInteraction.enabled = value;
    }

    public void SwitchToCamera(int index)
    {
        if (index < 0 || index >= securityCameras.Length) return;
        if (transitioning) return;
        if (index == currentCameraIndex) return;
        StartCoroutine(SwitchRoutine(index));
    }

    IEnumerator SwitchRoutine(int index)
    {
        transitioning = true;
        staticOverlay.gameObject.SetActive(true);
        securityCameras[index].enabled = true;

        float half = Mathf.Max(0.05f, transitionDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Clamp01(t / half));
            yield return null;
        }
        SetOverlayAlpha(1f);

        int prev = currentCameraIndex;
        currentCameraIndex = index;
        cameraDisplay.texture = renderTextures[currentCameraIndex];

        yield return new WaitForSecondsRealtime(0.05f);

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            SetOverlayAlpha(1f - Mathf.Clamp01(t / half));
            yield return null;
        }
        SetOverlayAlpha(0f);
        staticOverlay.gameObject.SetActive(false);

        if (prev != index) securityCameras[prev].enabled = false;
        transitioning = false;
    }

    void SetOverlayAlpha(float a)
    {
        var c = staticOverlay.color; c.a = a; staticOverlay.color = c;
    }

    void UpdateNoiseTexture()
    {
        for (int i = 0; i < noiseBuffer.Length; i++)
            noiseBuffer[i] = (byte)rng.Next(0, 256);
        noiseTexture.LoadRawTextureData(noiseBuffer);
        noiseTexture.Apply(false);

        Rect uv = staticOverlay.uvRect;
        uv.x += Time.unscaledDeltaTime * 2f;
        uv.y -= Time.unscaledDeltaTime * 1.3f;
        staticOverlay.uvRect = uv;
    }
}
