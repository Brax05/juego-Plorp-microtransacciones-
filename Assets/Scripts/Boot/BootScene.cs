using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Inicio scene: title screen (JUGAR / AJUSTES) and the loading screen. JUGAR loads the game scene
/// (Build Settings index 1) in the background and the loading bar follows that real progress.
/// The game scene then skips its own title screen (see <see cref="CameFromBoot"/>).
/// Create the scene with Tools ▸ Plorp ▸ Crear escena de inicio.
/// </summary>
public sealed class BootScene : MonoBehaviour
{
    /// <summary>True once the game scene was opened from Inicio (so it doesn't show the title again).</summary>
    public static bool CameFromBoot { get; private set; }

    /// <summary>True while the Inicio scene is on screen (the farm must not build on top of it).</summary>
    public static bool IsShowing { get; private set; }

    // Statics survive between Play sessions when domain reload is off: start clean every time.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CameFromBoot = false;
        IsShowing = false;
    }

    void OnDestroy() => IsShowing = false;

    RectTransform portraitRect;

    // The canvas scaler already fits the portrait to the screen; nothing else may rescale it.
    void LateUpdate()
    {
        if (portraitRect != null && portraitRect.localScale != Vector3.one) portraitRect.localScale = Vector3.one;
    }

    const float Width = 570f, Height = 1018f;
    const int GameSceneIndex = 1;

    void Awake()
    {
        IsShowing = true;

        // If the farm canvas is also loaded (both scenes open in the Hierarchy), hide it: it belongs to Granja.
        foreach (var other in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (other.isRootCanvas && other.gameObject.scene != gameObject.scene) other.gameObject.SetActive(false);

        var state = new FarmGameState(); // music/sound settings in AJUSTES

        var canvasGo = new GameObject("Inicio Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        // Shrink = the whole 570x1018 portrait always fits inside the screen (same as the farm's FitScreen);
        // the dark backdrop fills the leftover bars. "Match .5" cropped the title on wide Game views.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

        // Dark backdrop for screens with another aspect ratio, then the portrait area centred.
        var back = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(canvasGo.transform, false);
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        back.GetComponent<Image>().color = PixelUIFactory.Hex("#14112B");

        var portrait = new GameObject("Inicio portrait", typeof(RectTransform)).GetComponent<RectTransform>();
        portrait.SetParent(canvasGo.transform, false);
        portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(.5f, .5f);
        portrait.sizeDelta = new Vector2(Width, Height);
        portraitRect = portrait;

        StartScreens.CreateForBoot(portrait, state, () =>
        {
            CameFromBoot = true;
            try
            {
                var op = SceneManager.LoadSceneAsync("Granja");
                if (op != null) return op;
            }
            catch { }

            if (SceneManager.sceneCountInBuildSettings > GameSceneIndex)
                return SceneManager.LoadSceneAsync(GameSceneIndex);

            Debug.LogError("Agrega la escena del juego a Build Settings (Tools ▸ Plorp ▸ Crear escena de inicio).");
            return null;
        });

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
