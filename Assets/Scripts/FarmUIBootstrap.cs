using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Composes the modular farm UI and handles screen changes.</summary>
public sealed class FarmUIBootstrap : MonoBehaviour
{
    RectTransform content;
    FarmScreen farm;
    FarmMenuScreen menu;
    Text messageLabel;
    Coroutine messageRoutine;
    int lastWidth;
    int lastHeight;
    Vector3 authoredScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<FarmUIBootstrap>() != null) return;
        new GameObject("PlorpFarm UI").AddComponent<FarmUIBootstrap>();
    }

    void Awake()
    {
        var state = new FarmGameState();

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("PixelFarmCanvas must be present in the scene. The UI is authored in the canvas.");
            enabled = false;
            return;
        }

        var portraitTransform = FindChildRecursive(canvas.transform, "Portrait screen") as RectTransform;
        if (portraitTransform == null)
        {
            Debug.LogError("Portrait screen is missing from PixelFarmCanvas.", canvas);
            enabled = false;
            return;
        }
        content = portraitTransform;
        authoredScale = content.localScale;
        FitScreen();

        farm = new FarmScreen(content, state, Navigate, ShowMessage);
        menu = new FarmMenuScreen(content, state, Navigate, ShowMessage);

        var feedbackTransform = FindChildRecursive(content, "Feedback");
        messageLabel = feedbackTransform != null ? feedbackTransform.GetComponent<Text>() : null;
        if (messageLabel != null)
        {
            messageLabel.gameObject.SetActive(false);
            // Pepito lives at the bottom of the farm, so feedback goes above the fields.
            var feedbackRect = messageLabel.rectTransform;
            feedbackRect.anchoredPosition = new Vector2(feedbackRect.anchoredPosition.x, -250f);
        }

        // Plorp's order panel and Pepito sit above the farm but under the menus and feedback.
        int layer = feedbackTransform != null ? feedbackTransform.GetSiblingIndex() : content.childCount;
        var pepito = PepitoController.Create(content, layer, state, ShowMessage);
        PlorpGoalPanel.Create(content, layer, state, ShowMessage, pepito);
        FarmArtLayout.LockedZoneTapped = () =>
        {
            ShowMessage("Zona bloqueada: más adelante...");
            pepito.Say("Miau... Plorp dice que detrás de esas nubes hay más tierra. Se abrirá más adelante. Él tampoco sabe cuándo.", 7f);
        };

        Navigate(0);

        // Title screen -> loading -> farm. Pepito starts visiting once the farm is visible.
        StartScreens.Create(content, state, pepito.Begin);

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
        float scale = Mathf.Min(Screen.width / Mathf.Max(1f, content.rect.width),
            Screen.height / Mathf.Max(1f, content.rect.height));
        content.localScale = authoredScale * scale;
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
        if (messageLabel == null) return;
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
