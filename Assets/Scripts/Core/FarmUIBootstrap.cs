using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Starts the game: builds (or reuses, if baked) the UI on PixelFarmCanvas, connects every system and
/// handles tab changes. The layout itself lives in the canvas — see PixelUIFactory and
/// "Plorp ▸ Hornear UI en el canvas".
/// Tabs: 0 Granja, 1 Tienda, 2 Logros, 3 Jardinería, 4 Ajustes, 5 Casa de Plorp, 6 Cocina.
/// </summary>
public sealed class FarmUIBootstrap : MonoBehaviour
{
    const string SeasonAnnouncedKey = "GlorpFarm.season.announced";
    static readonly string[] FarmTips =
    {
        "¡Toca los tickets dorados!", "¡Arrastra para recorrer la granja!", "¡Cada 10 compras, algo nuevo!",
        "¡Los trabajadores cosechan solos!", "¡Mira la planta en JARDINERÍA!",
        "¡Hay zonas por abrir en las nubes!", "¡Juega en el computador de Plorp!", "¡Cocina platos para Plorp!",
    };

    RectTransform content;
    Vector3 authoredScale;
    int lastWidth, lastHeight;

    FarmGameState gameState;
    FarmScreen farm;
    FarmMenuScreen menu;
    PepitoController farmPepito;
    PepitoGuide guide;
    FarmEvents farmEvents;
    FarmAmbience ambience;

    Text messageLabel, boostLabel, achievementBadgeText;
    RectTransform achievementBadge;
    Coroutine messageRoutine;

    int currentTab;
    bool guideReady, wasNight;
    float nextHudRefresh, nextSeenSave, nextFarmTip = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<FarmUIBootstrap>() != null) return;
        new GameObject("PlorpFarm UI").GetOrAdd<FarmUIBootstrap>();
    }

    void Awake() => Compose(false);

    /// <summary>
    /// Reloads the farm scene and builds the whole UI again from the save (star trip, a save brought back
    /// from Google Play Juegos).
    /// </summary>
    public static void ReloadFarm()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += RebuildAfterReload;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    static void RebuildAfterReload(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= RebuildAfterReload;
        SaveStore.Frozen = false;
        if (FindAnyObjectByType<FarmUIBootstrap>() == null) new GameObject("PlorpFarm UI").GetOrAdd<FarmUIBootstrap>();
    }

    /// <summary>
    /// Editor only (Plorp ▸ Hornear UI en el canvas): builds the whole UI on the canvas without playing, so
    /// it can be saved into PixelFarmCanvas and edited in Unity. Nothing that needs Play mode runs here
    /// (title screen flow, store purchases, timers, EventSystem).
    /// </summary>
    public void BakeInto() => Compose(true);

    void Compose(bool bake)
    {
        var state = new FarmGameState();
        gameState = state;

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
#if UNITY_EDITOR
        if (canvas == null && !bake)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PixelFarmCanvas.prefab");
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                canvas = instance.GetComponent<Canvas>();
                Debug.LogWarning("PixelFarmCanvas no estaba en la escena: se instanció desde el prefab. Abre 'Assets/Scenes/Granja.unity' para editarlo.", instance);
            }
        }
#endif
        if (canvas == null)
        {
            Debug.LogError("PixelFarmCanvas debe estar en la escena. Abre 'Assets/Scenes/Granja.unity' antes de dar Play.");
            enabled = false;
            return;
        }
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        if (!canvas.enabled) canvas.enabled = true;

        content = FindChildRecursive(canvas.transform, "Portrait screen") as RectTransform;
        if (content == null)
        {
            Debug.LogError("Falta 'Portrait screen' en PixelFarmCanvas.", canvas);
            enabled = false;
            return;
        }
        authoredScale = content.localScale;
        // Fills the phone: width-fill scale, taller content and the bottom block moved down.
        ScreenLayout.Prepare(content, authoredScale, bake);
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        // ---------- Layout (baked into the canvas and reused from it) ----------
        RenameGardenTab(content);
        farm = new FarmScreen(content, state, Navigate, ShowMessage);
        menu = new FarmMenuScreen(content, state, Navigate, ShowMessage);

        var feedback = FindChildRecursive(content, "Feedback");
        messageLabel = feedback != null ? feedback.GetComponent<Text>() : null;
        if (messageLabel != null) messageLabel.gameObject.SetActive(false);

        // Plorp's order and Pepito sit above the farm but under the menus and the feedback text.
        int layer = feedback != null ? feedback.GetSiblingIndex() : content.childCount;
        var pepito = PepitoController.Create(content, layer, state, ShowMessage);
        var goalPanel = PlorpGoalPanel.Create(content, layer, state, ShowMessage, pepito);
        farmPepito = pepito;

        var hud = new PixelUIFactory();
        boostLabel = hud.Label(content, "Boost timers", "", 300, 112, 262, 22, 14, PixelUIFactory.Gold, TextAnchor.MiddleRight);
        boostLabel.transform.SetSiblingIndex(layer);
        achievementBadge = hud.Panel(content, "Achievements badge", 296, 948, 30, 26, PixelUIFactory.Hex("#D23C3C"));
        achievementBadgeText = hud.Label(achievementBadge, "Count", "", 0, 0, 30, 26, 13, Color.white, TextAnchor.MiddleCenter);
        RewardFx.Create(content, FindChildRecursive(content, "Gold"));
        RewardFx.SetFoodTarget(goalPanel.transform.Find("Plorp face") ?? goalPanel.transform);

        var worldRoot = FindChildRecursive(content, "Modular farm world");
        var world = worldRoot != null ? worldRoot.GetComponent<FarmWorldController>() : null;
        if (world != null)
        {
            ambience = FarmAmbience.Create(world, content, layer);                     // Módulo 5
            farmEvents = FarmEvents.Create(world, state, pepito, content, ShowMessage); // Módulo 3
            FarmZones.Create(world, state, pepito, content, ShowMessage);              // Módulos 6, 9, 10
            HouseEntrance.Create(world, () => Navigate(5));                           // Casa de Plorp
        }

        guide = PepitoGuide.Create(content);
        AddGuideButton(content);
        if (!bake) ScreenLayout.Fit(content);

        if (bake)
        {
            // Saved hidden so the title screen doesn't cover the farm while editing.
            StartScreens.Create(content, state, null).gameObject.SetActive(false);
            Navigate(0);
            return;
        }

        // ---------- Play mode only ----------
        PurchaseService.Initialize(state, ShowMessage);
        state.Garden.Alert += text => { if (currentTab == 0 && farmPepito != null) farmPepito.Say(text, 7f); };
        Kitchen.DishReady += OnDishReady;
        RefundSync.Revoked += OnGemsRevoked;
        if (ambience != null) ambience.BadgeTapped = () => farmPepito.Say(FarmClock.BadgeLine(), 8f);
        Navigate(0);

        // How long the player was away (read now, before "last seen" is updated again).
        double awayAtStart = OfflinePanel.SecondsAway();
        DailyMissions.RegisterLogin();

        // Title → loading → save notice (first time) → farm tour (first time) → "Mientras no estabas" →
        // daily streak → Pepito.
        StartScreens.Create(content, state, () =>
        {
            guideReady = true;
            bool firstTime = !PepitoGuide.Seen(0);
            SaveNotice.ShowIfNew(content, () =>
                guide.ShowIfNew(0, () =>
                    OfflinePanel.ShowIfAway(content, state, awayAtStart, ShowMessage, () =>
                    {
                        OfflinePanel.MarkSeen();
                        StreakPanel.ShowIfReady(content, state, ShowMessage, () =>
                        {
                            pepito.Begin(!firstTime);
                            StarTrip.AfterTrip(pepito);
                            CloudSave.AnnounceTo(pepito);
                        });
                    })));
        });

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
    }

    void OnDestroy()
    {
        Kitchen.DishReady -= OnDishReady;
        RefundSync.Revoked -= OnGemsRevoked;
    }

    void OnGemsRevoked(int gems)
    {
        ShowMessage("-" + gems + " gemas por una compra reembolsada");
        if (farmPepito != null)
            farmPepito.Say("Miau... Google nos avisó que una compra se reembolsó, así que Plorp tuvo que devolver " + gems +
                           " gemas. Reglas de la nave.", 9f);
    }

    void OnDishReady(Recipe recipe)
    {
        GameAudio.Play("sfx_dish_ready");
        if (farmPepito != null && currentTab == 0 && recipe != null)
            farmPepito.Say("Miau... ¡" + recipe.Name + " lista! Entra a la CASA DE PLORP (el granero amarillo) para sacarla de la olla.", 6f);
    }

    /// <summary>"VER GUÍA DE PEPITO" in Ajustes: shows every section's tour again, starting with the farm.</summary>
    void AddGuideButton(Transform root)
    {
        var settings = FindChildRecursive(root, "Page - Ajustes");
        if (settings == null) return;
        // PARTIDA GUARDADA card: cloud save status, CONECTAR and VER AVISO.
        SaveNotice.BindSettings(settings, root, ShowMessage);
        new PixelUIFactory().ActionButton(settings, "Guide button", "VER GUÍA DE PEPITO", 110, 656, 350, 60, () =>
        {
            PepitoGuide.ResetAll();
            Navigate(0);
        });
    }

    // ---------- Every frame ----------

    void Update()
    {
        if (gameState == null) return;
        if (Screen.width != lastWidth || Screen.height != lastHeight) FitScreen();

        // One rule for "something is on top": the guide or any window (Popup counts them all).
        bool popupOpen = PepitoGuide.Active || Popup.AnyOpen;
        if (farmPepito != null) farmPepito.Suppressed = currentTab != 0 || popupOpen;
        bool farmQuiet = guideReady && farmPepito != null && farmPepito.Started && currentTab == 0 && !popupOpen;
        if (farmEvents != null) farmEvents.Running = farmQuiet;
        AnnounceClock(farmQuiet);
        if (farmQuiet) UpdateFarmTips();
        UpdateAudio(popupOpen);

        // Remember that the player is here (for "Mientras no estabas").
        if (guideReady && !OfflinePanel.Active && Time.unscaledTime >= nextSeenSave)
        {
            nextSeenSave = Time.unscaledTime + 10f;
            OfflinePanel.MarkSeen();
        }

        if (Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 1f;
            RefreshHud();
        }
        Kitchen.Tick();
        gameState.Garden?.Tick(Time.unscaledDeltaTime); // the plant lives on in every tab
    }

    /// <summary>
    /// Music for the place you are in (title, farm by day / night, garden, Plorp's house, or the window on
    /// top), and the ambience beds under it (wind, birds or crickets, rain, the pot bubbling).
    /// </summary>
    void UpdateAudio(bool popupOpen)
    {
        var start = content.Find("Start screens");
        bool title = start != null && start.gameObject.activeSelf;
        GameAudio.FarmVisible = !title && currentTab == 0 && !popupOpen;
        string music = title ? "music_menu_loading"
            : GameAudio.MusicOverride ?? (currentTab == 3 ? "music_garden"
                : currentTab >= 5 ? "music_house"
                : FarmClock.IsNight ? "music_farm_night" : "music_farm_day");
        GameAudio.SetMusic(music);

        if (title || GameAudio.MusicOverride != null) GameAudio.SetAmbience(null);
        else if (currentTab == 0)
            GameAudio.SetAmbience("amb_wind_loop", FarmClock.IsNight ? "amb_night_crickets_loop" : "amb_day_birds_loop",
                FarmEvents.GrowthMultiplier > 1f ? "amb_rain_loop" : null);
        else if (currentTab == 6 && Kitchen.CookingCount() > 0) GameAudio.SetAmbience("amb_pot_bubbling_loop");
        else GameAudio.SetAmbience(null);
    }

    void UpdateFarmTips()
    {
        nextFarmTip -= Time.unscaledDeltaTime;
        if (nextFarmTip > 0f) return;
        nextFarmTip = 35f + Random.value * 20f;
        string tip = gameState.GoalReady ? "¡Entrégale el pedido a Plorp!"
            : Kitchen.ReadyCount() > 0 ? "¡Hay platos listos en la casa de Plorp!"
            : DailyMissions.ReadyCount(gameState) > 0 ? "¡Hay misiones por cobrar!"
            : AchievementCatalog.AnyReady(gameState) ? "¡Hay logros por cobrar!"
            : FarmTips[Random.Range(0, FarmTips.Length)];
        CurvedPopup.Show(content, tip, new Vector2(285f, 330f), PixelUIFactory.Gold, 24);
    }

    void RefreshHud()
    {
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

    /// <summary>Pepito tells the new season (once a day) and when night falls.</summary>
    void AnnounceClock(bool canTalk)
    {
        bool night = FarmClock.IsNight;
        bool nightFell = night && !wasNight;
        wasNight = night;
        if (!canTalk) return;
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (SaveStore.GetString(SeasonAnnouncedKey, "") != today)
        {
            SaveStore.SetString(SeasonAnnouncedKey, today);
            farmPepito.Say(FarmClock.SeasonLine(FarmClock.CurrentSeason), 8f);
        }
        else if (nightFell)
            farmPepito.Say("Miau... se hizo de noche. Mira las luciérnagas. De noche las cosechas doradas salen el doble.", 6f);
    }

    // ---------- App pause / quit ----------

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            gameState?.Garden?.Save();
            if (guideReady && !OfflinePanel.Active) OfflinePanel.MarkSeen();
            SaveStore.Save();
            return;
        }
        // Back from the background (phone): show what the farm made meanwhile.
        if (!guideReady || gameState == null || content == null || PepitoGuide.Active || Popup.AnyOpen) return;
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
        SaveStore.Save();
    }

    // ---------- Navigation and messages ----------

    void Navigate(int tab)
    {
        currentTab = tab;
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
        if (messageLabel == null || !Application.isPlaying) return;
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

    void FitScreen()
    {
        if (content == null) return;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        ScreenLayout.Apply(content, authoredScale);
    }

    /// <summary>
    /// The old "MEJORAS" tab becomes "JARDINERÍA" with a seed-bag icon. Only changes the original
    /// values, so whatever is edited in the canvas afterwards is kept.
    /// </summary>
    static void RenameGardenTab(Transform root)
    {
        var label = FindChildRecursive(root, "Navigation label Mejoras");
        var text = label != null ? label.GetComponent<Text>() : null;
        if (text != null && text.text.ToUpperInvariant() == "MEJORAS")
        {
            text.text = "JARDINERÍA";
            text.fontSize = 13;
        }
        var icon = FindChildRecursive(root, "Navigation icon Mejoras");
        var raw = icon != null ? icon.GetComponent<RawImage>() : null;
        if (raw != null && (raw.texture == null || raw.texture.name == "mejora")) raw.texture = Art.Tex("semillas");
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
}
