using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Composes the modular farm UI and handles screen changes.</summary>
public sealed class FarmUIBootstrap : MonoBehaviour
{
    const float Width = 570f;
    const float Height = 1018f;
    RectTransform content;
    FarmScreen farm;
    FarmMenuScreen menu;
    Text messageLabel;
    Coroutine messageRoutine;
    int lastWidth;
    int lastHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<FarmUIBootstrap>() != null) return;
        new GameObject("GlorpFarm UI").AddComponent<FarmUIBootstrap>();
    }

    void Awake()
    {
        var state = new FarmGameState();
        var ui = new PixelUIFactory();

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

        GameObject canvasObject;
        if (canvas == null)
        {
            canvasObject = new GameObject("Pixel Farm Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }
        else
        {
            canvasObject = canvas.gameObject;
        }

        var portraitTransform = FindChildRecursive(canvasObject.transform, "Portrait screen") as RectTransform;
        if (portraitTransform == null)
        {
            content = ui.Rect(canvasObject.transform, "Portrait screen", 0, 0, Width, Height);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
        }
        else
        {
            content = portraitTransform;
        }
        FitScreen();

        farm = new FarmScreen(content, state, ui, Navigate, ShowMessage);
        menu = new FarmMenuScreen(content, state, ui, Navigate, ShowMessage);

        var feedbackTransform = FindChildRecursive(content, "Feedback");
        if (feedbackTransform != null)
        {
            messageLabel = feedbackTransform.GetComponent<Text>();
        }
        else
        {
            messageLabel = ui.Label(content, "Feedback", "", 60, 488, 450, 48, 24,
                PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        }
        if (messageLabel != null) messageLabel.gameObject.SetActive(false);

        Navigate(0);

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
    }

    public static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight) FitScreen();
    }

    void FitScreen()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
        content.localScale = Vector3.one * scale;
    }

    void Navigate(int tab)
    {
        farm.SelectTab(tab);
        menu.Show(tab);
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        if (messageLabel != null) messageLabel.gameObject.SetActive(false);
    }

    void ShowMessage(string text)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageForMoment(text));
    }

    IEnumerator MessageForMoment(string text)
    {
        messageLabel.text = text;
        messageLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.6f);
        messageLabel.gameObject.SetActive(false);
        messageRoutine = null;
    }
}
