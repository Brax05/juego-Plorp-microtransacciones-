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

        // Crows, rain, foxes and the fair truck now and then.
        var worldRoot = FindChildRecursive(content, "Modular farm world");
        var worldController = worldRoot != null ? worldRoot.GetComponent<FarmWorldController>() : null;
        // Day/night and seasons over the farm, with the season badge on the HUD.
        if (worldController != null) ambience = FarmAmbience.Create(worldController, content, layer);
        if (ambience != null)
            ambience.BadgeTapped = () =>
            {
                var season = FarmClock.CurrentSeason;
                string crop = FarmItems.ForCrop(FarmClock.SeasonCrop(season)).Name.ToLowerInvariant();
                string sky = FarmClock.IsNight
                    ? "Ahora es de noche: las cosechas doradas salen el doble."
                    : "Falta " + Mathf.Max(1, FarmClock.SecondsToNight / 60) + " min para la noche (cosechas doradas x2).";
                pepito.Say("Miau. Es " + FarmClock.SeasonName(season) + ": los " + crop + " valen x1,5 hoy. " +
                           "Mañana cambia la estación. " + sky, 8f);
            };
        if (worldController != null) farmEvents = FarmEvents.Create(worldController, state, pepito, content, ShowMessage);

        // Areas behind the clouds (pond, orchard, hives, launch pad) and the games room.
        if (worldController != null) FarmZones.Create(worldController, state, pepito, content, ShowMessage);
        if (worldController != null) GamesSign.Create(worldController, () => GamesHub.Open(content, state, ShowMessage));

        // Plorp's kitchen: sign on the farm (opens tab 5) and Pepito says when a dish is done.
        if (worldController != null) KitchenSign.Create(worldController, () => Navigate(5));
        Kitchen.DishReady += recipe =>
        {
            if (pepito != null && currentTab == 0 && recipe != null)
                pepito.Say("Miau... ¡" + recipe.Name + " lista! Toca la COCINA para sacarla de la olla.", 6f);
        };

        // Pepito's welcome tour: the first time in each section he explains what everything is.
        guide = PepitoGuide.Create(content);
        AddGuideButton(content);

        // How long the player was away (read now, before "last seen" is updated again).
        double awayAtStart = OfflinePanel.SecondsAway();
        DailyMissions.RegisterLogin();

        // Title screen -> loading -> farm tour (first time) -> "Mientras no estabas" -> Pepito starts visiting.
        StartScreens.Create(content, state, () =>
        {
            guideReady = true;
            bool firstTime = !PepitoGuide.Seen(0);
            guide.ShowIfNew(0, () =>
                OfflinePanel.ShowIfAway(content, state, awayAtStart, ShowMessage, () =>
                {
                    OfflinePanel.MarkSeen();
                    StreakPanel.ShowIfReady(content, state, ShowMessage, () =>
                    {
                        pepito.Begin(!firstTime);
                        StarTrip.AfterTrip(pepito);
                    });
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
    FarmEvents farmEvents;
    FarmAmbience ambience;
    bool wasNight;
    const string SeasonAnnouncedKey = "GlorpFarm.season.announced";

    /// <summary>Pepito tells the new season (once a day) and when night falls.</summary>
    void AnnounceClock(bool canTalk)
    {
        bool night = FarmClock.IsNight;
        bool nightFell = night && !wasNight;
        wasNight = night;
        if (!canTalk || farmPepito == null) return;
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (PlayerPrefs.GetString(SeasonAnnouncedKey, "") != today)
        {
            PlayerPrefs.SetString(SeasonAnnouncedKey, today);
            farmPepito.Say(FarmClock.SeasonLine(FarmClock.CurrentSeason), 8f);
            return;
        }
        if (nightFell)
            farmPepito.Say("Miau... se hizo de noche. Mira las luciérnagas. De noche las cosechas doradas salen el doble.", 6f);
    }

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
        "¡Hay zonas por abrir en las nubes!", "¡Prueba la sala de JUEGOS!", "¡Cocina platos para Plorp!",
    };

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight) FitScreen();
        // Arched tips over the farm now and then.
        // The farm Pepito only walks around on the farm, and never during the welcome tour.
        bool popupOpen = PepitoGuide.Active || OfflinePanel.Active || StreakPanel.Active || Popup.AnyOpen;
        if (farmPepito != null) farmPepito.Suppressed = currentTab != 0 || popupOpen;
        bool farmQuiet = guideReady && farmPepito != null && farmPepito.Started && currentTab == 0 && !popupOpen;
        if (farmEvents != null) farmEvents.Running = farmQuiet;
        AnnounceClock(farmQuiet);
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
                    : Kitchen.ReadyCount() > 0 ? "¡Hay platos listos en la COCINA!"
                    : DailyMissions.ReadyCount(gameState) > 0 ? "¡Hay misiones por cobrar!"
                    : AchievementCatalog.AnyReady(gameState) ? "¡Hay logros por cobrar!"
                    : FarmTips[UnityEngine.Random.Range(0, FarmTips.Length)];
                CurvedPopup.Show(content, tip, new Vector2(285f, 330f), PixelUIFactory.Gold, 24);
            }
        }

        if (gameState != null && Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 1f;
            if (boostLabel != null)
            {
                string stars = StarTrip.Stars > 0 ? "Semillas estelares " + StarTrip.Stars + " (+" + StarTrip.Stars * 10 + "%)" : "";
                string boosts = gameState.Boosts.Summary;
                boostLabel.text = stars + (stars.Length > 0 && boosts.Length > 0 ? " · " : "") + boosts;
            }
            if (achievementBadge != null)
            {
                int ready = AchievementCatalog.ReadyCount(gameState) + DailyMissions.ReadyCount(gameState);
                achievementBadge.gameObject.SetActive(ready > 0);
                achievementBadgeText.text = ready.ToString();
            }
        }
        Kitchen.Tick();
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
        if (!guideReady || gameState == null || content == null || OfflinePanel.Active || PepitoGuide.Active ||
            StreakPanel.Active) return;
        DailyMissions.RegisterLogin(); // it may be a new day
        double away = OfflinePanel.SecondsAway();
        if (away < OfflinePanel.MinSeconds) return;
        if (currentTab != 0) Navigate(0);
        OfflinePanel.ShowIfAway(content, gameState, away, ShowMessage, () =>
        {
            OfflinePanel.MarkSeen();
            StreakPanel.ShowIfReady(content, gameState, ShowMessage, null);
        });
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
