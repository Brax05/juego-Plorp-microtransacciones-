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

    void Awake() => Compose(false);

    /// <summary>
    /// Editor only (Tools ▸ Plorp ▸ Vista previa): builds the same UI the game builds at runtime, but on a
    /// preview copy of the canvas, so the Scene/Game views show the current code. Skips store purchases,
    /// screen fitting, the title screen and the EventSystem.
    /// </summary>
    public void BuildPreview() => Compose(true);

    /// <summary>Editor preview: shows one bottom tab (0 Granja … 4 Ajustes) with its content filled in.</summary>
    public void PreviewTab(int tab)
    {
        if (content == null) return;
        var start = content.Find("Start screens");
        if (start != null) start.gameObject.SetActive(false);
        Navigate(tab);
        foreach (var page in content.GetComponentsInChildren<StoreScreen>(true)) page.PreviewRefresh();
        foreach (var page in content.GetComponentsInChildren<AchievementsScreen>(true)) page.PreviewRefresh();
        foreach (var page in content.GetComponentsInChildren<GardenScreen>(true)) page.PreviewRefresh();
        if (boostLabel != null) boostLabel.text = gameState.Boosts.Summary;
    }

    /// <summary>Editor preview: shows the title screen on top.</summary>
    public void PreviewStartScreen()
    {
        if (content == null) return;
        var start = content.Find("Start screens");
        if (start == null) StartScreens.Create(content, gameState, () => { });
        else
        {
            start.gameObject.SetActive(true);
            start.SetAsLastSibling();
        }
    }

    void Compose(bool preview)
    {
        var state = new FarmGameState();
        gameState = state;

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

#if UNITY_EDITOR
        if (canvas == null)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PixelFarmCanvas.prefab");
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                canvas = instance.GetComponent<Canvas>();
                Debug.LogWarning("PixelFarmCanvas no estaba en la escena activa. Se instanció automáticamente desde Assets/Prefabs/PixelFarmCanvas.prefab. Para editar y guardar cambios en el canvas, abre la escena 'Assets/Scenes/Granja.unity'.", instance);
            }
        }
#endif

        if (canvas == null)
        {
            Debug.LogError("PixelFarmCanvas must be present in the scene. The UI is authored in the canvas. Abre la escena 'Assets/Scenes/Granja.unity' en la ventana Project (Assets > Scenes > Granja) antes de dar Play.");
            enabled = false;
            return;
        }

        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        if (!canvas.enabled) canvas.enabled = true;

        var portraitTransform = FindChildRecursive(canvas.transform, "Portrait screen") as RectTransform;
        if (portraitTransform == null)
        {
            Debug.LogError("Portrait screen is missing from PixelFarmCanvas.", canvas);
            enabled = false;
            return;
        }
        content = portraitTransform;
        authoredScale = content.localScale;
        if (!preview) FitScreen();

        RenameGardenTab(content);
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
        var goalPanel = PlorpGoalPanel.Create(content, layer, state, ShowMessage, pepito);
        // Active boosts on the farm (above the farm, under the menus).
        var hudUi = new PixelUIFactory();
        boostLabel = hudUi.Label(content, "Boost timers", "", 300, 112, 262, 22, 14, PixelUIFactory.Gold, TextAnchor.MiddleRight);
        boostLabel.transform.SetSiblingIndex(layer);

        // Pepito speaks up when the garden plant needs something (dry, too much sun, bugs, bloom...).
        // Only on the farm tab: inside Jardinería the garden shows its own toast (Say is ignored while suppressed).
        farmPepito = pepito;
        state.Garden.Alert += text => { if (pepito != null && currentTab == 0) pepito.Say(text, 7f); };
        RewardFx.Create(content, FindChildRecursive(content, "Gold"));
        RewardFx.SetFoodTarget(goalPanel.transform.Find("Plorp face") ?? goalPanel.transform);
        FarmArtLayout.LockedZoneTapped = () =>
        {
            ShowMessage("Zona bloqueada: más adelante...");
            if (pepito != null) pepito.Say("Miau... Plorp dice que detrás de esas nubes hay más tierra. Se abrirá más adelante. Él tampoco sabe cuándo.", 7f);
        };

        // Red badge on the Logros tab with how many achievements are ready to claim.
        achievementBadge = hudUi.Panel(content, "Achievements badge", 296, 948, 30, 26, PixelUIFactory.Hex("#D23C3C"));
        achievementBadgeText = hudUi.Label(achievementBadge, "Count", "", 0, 0, 30, 26, 13, Color.white, TextAnchor.MiddleCenter);

        // Real-money gem packs (Unity IAP, or test mode).
        if (!preview) PurchaseService.Initialize(state, ShowMessage);

        Navigate(0);
        if (preview) return;

        // Pepito's welcome tour: the first time in each section he explains what everything is.
        guide = PepitoGuide.Create(content);
        AddGuideButton(content);

        // How long the player was away (read now, before "last seen" is updated again).
        double awayAtStart = OfflinePanel.SecondsAway();

        // Title screen -> loading -> farm tour (first time) -> "Mientras no estabas" -> Pepito starts visiting.
        StartScreens.Create(content, state, () =>
        {
            guideReady = true;
            bool firstTime = !PepitoGuide.Seen(0);
            guide.ShowIfNew(0, () =>
                OfflinePanel.ShowIfAway(content, state, awayAtStart, ShowMessage, () =>
                {
                    OfflinePanel.MarkSeen();
                    pepito.Begin(!firstTime);
                }));
        });

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

    FarmGameState gameState;
    Text boostLabel;
    RectTransform achievementBadge;
    Text achievementBadgeText;
    float nextHudRefresh;
    int currentTab;
    PepitoController farmPepito;
    PepitoGuide guide;
    bool guideReady;
    float nextSeenSave;

    /// <summary>"VER GUÍA DE PEPITO" in Ajustes: shows every section's tour again, starting with the farm.</summary>
    void AddGuideButton(Transform root)
    {
        var settings = FindChildRecursive(root, "Page - Ajustes");
        if (settings == null) return;
        new PixelUIFactory().ActionButton(settings, "Guide button", "VER GUÍA DE PEPITO", 110, 656, 350, 60, () =>
        {
            PepitoGuide.ResetAll();
            Navigate(0);
        });
    }
    float nextFarmTip = 25f;
    static readonly string[] FarmTips =
    {
        "¡Toca los tickets dorados!", "¡Arrastra para recorrer la granja!", "¡Cada 10 compras, algo nuevo!",
        "¡Los trabajadores cosechan solos!", "¡Mira la planta en JARDINERÍA!",
    };

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight) FitScreen();
        // Arched tips over the farm now and then.
        // The farm Pepito only walks around on the farm, and never during the welcome tour.
        bool popupOpen = PepitoGuide.Active || OfflinePanel.Active;
        if (farmPepito != null) farmPepito.Suppressed = currentTab != 0 || popupOpen;
        // Remember that the player is here (for "Mientras no estabas").
        if (guideReady && !OfflinePanel.Active && Time.unscaledTime >= nextSeenSave)
        {
            nextSeenSave = Time.unscaledTime + 10f;
            OfflinePanel.MarkSeen();
        }
        if (gameState != null && currentTab == 0 && content != null && !popupOpen)
        {
            nextFarmTip -= Time.unscaledDeltaTime;
            if (nextFarmTip <= 0f)
            {
                nextFarmTip = 35f + UnityEngine.Random.value * 20f;
                string tip = gameState.GoalReady ? "¡Entrégale el pedido a Plorp!"
                    : AchievementCatalog.AnyReady(gameState) ? "¡Hay logros por cobrar!"
                    : FarmTips[UnityEngine.Random.Range(0, FarmTips.Length)];
                CurvedPopup.Show(content, tip, new Vector2(285f, 330f), PixelUIFactory.Gold, 24);
            }
        }

        if (gameState != null && Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 1f;
            if (boostLabel != null) boostLabel.text = gameState.Boosts.Summary;
            if (achievementBadge != null)
            {
                int ready = AchievementCatalog.ReadyCount(gameState);
                achievementBadge.gameObject.SetActive(ready > 0);
                achievementBadgeText.text = ready.ToString();
            }
        }
        // The garden plant keeps living while the player is on other tabs.
        gameState?.Garden?.Tick(Time.unscaledDeltaTime);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            gameState?.Garden?.Save();
            if (guideReady && !OfflinePanel.Active) OfflinePanel.MarkSeen();
            PlayerPrefs.Save();
            return;
        }
        // Back from the background (phone): show what the farm made meanwhile.
        if (!guideReady || gameState == null || content == null || OfflinePanel.Active || PepitoGuide.Active) return;
        double away = OfflinePanel.SecondsAway();
        if (away < OfflinePanel.MinSeconds) return;
        if (currentTab != 0) Navigate(0);
        OfflinePanel.ShowIfAway(content, gameState, away, ShowMessage, OfflinePanel.MarkSeen);
    }

    void OnApplicationQuit()
    {
        gameState?.Garden?.Save();
        if (guideReady && !OfflinePanel.Active) OfflinePanel.MarkSeen();
        PlayerPrefs.Save();
    }

    /// <summary>The bottom tab "Mejoras" becomes "Jardinería" with a seed-bag icon.</summary>
    static void RenameGardenTab(Transform root)
    {
        var label = FindChildRecursive(root, "Navigation label Mejoras");
        var text = label != null ? label.GetComponent<Text>() : null;
        if (text != null)
        {
            text.text = "JARDINERÍA";
            text.fontSize = 13;
        }
        var icon = FindChildRecursive(root, "Navigation icon Mejoras");
        var raw = icon != null ? icon.GetComponent<RawImage>() : null;
        var seeds = Resources.Load<Texture2D>("UI/Icons/semillas");
        if (raw != null && seeds != null)
        {
            seeds.filterMode = FilterMode.Point;
            raw.texture = seeds;
        }
    }

    void FitScreen()
    {
        if (content == null) return;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        float scale = Mathf.Min(Screen.width / Mathf.Max(1f, content.rect.width),
            Screen.height / Mathf.Max(1f, content.rect.height));
        content.localScale = authoredScale * scale;
    }

    void Navigate(int tab)
    {
        currentTab = tab;
        if (farmPepito != null) farmPepito.Suppressed = tab != 0;
        farm.SelectTab(tab);
        menu.Show(tab);
        if (guideReady && guide != null)
        {
            guide.LeaveSection(tab);
            guide.ShowIfNew(tab);
        }
        if (tab != 0 && content != null) CurvedPopup.Dismiss(content);
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
