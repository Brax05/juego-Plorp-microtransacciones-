using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pepito's welcome tour. The first time the player enters each section (Granja, Tienda, Logros,
/// Jardinería, Ajustes) Pepito shows up, dims the screen around one thing at a time (golden frame and a
/// bouncing arrow) and explains what it is. SIGUIENTE goes on, SALTAR closes it. Each section is shown
/// once (saved in PlayerPrefs); "VER GUÍA DE PEPITO" in Ajustes shows them all again.
/// All rectangles are in the 570x1018 portrait; menu pages start at y = 108.
/// </summary>
public sealed class PepitoGuide : MonoBehaviour
{
    const float W = 570f, H = 1018f;
    const float PanelH = 178f;
    const string SeenKey = "GlorpFarm.guide.";
    const float CharsPerSecond = 55f;

    static readonly Color Dim = new Color(0f, 0f, .05f, .66f);
    static readonly Color Gold = PixelUIFactory.Hex("#FFD44F");
    static readonly Color Bubble = PixelUIFactory.Hex("#F6E7C8");
    static readonly Color Slate = PixelUIFactory.Hex("#4B5160");

    /// <summary>True while the tour is on screen (other popups and toasts wait).</summary>
    public static bool Active { get; private set; }

    struct Step
    {
        public Rect Area;     // what to highlight; width 0 = nothing (Pepito just talks)
        public string Text;
        public Step(string text) { Area = Rect.zero; Text = text; }
        public Step(float x, float y, float w, float h, string text) { Area = new Rect(x, y, w, h); Text = text; }
    }

    static readonly Step[][] Tours =
    {
        // 0 Granja
        new[]
        {
            new Step("¡Miau! Soy Pepito, un gato callejero. Plorp me subió a su nave para que le traduzca. " +
                     "Él solo dice \"plorp\"... pero yo le entiendo. Te muestro la granja."),
            new Step(6, 6, 352, 100, "Este es el NIVEL DE GRANJA. Sube mientras cosechas y compras mejoras. " +
                     "La barra amarilla muestra cuánto falta para el siguiente."),
            new Step(360, 6, 206, 100, "Tus MONEDAS y GEMAS. Las monedas salen de la granja; las gemas, de pedidos, " +
                     "logros y regalos de Plorp. Toca el + para ir a la tienda."),
            new Step(4, 108, 184, 136, "El PEDIDO DE PLORP. Junta lo que pide y toca ENTREGAR. " +
                     "Si come lo suficiente queda conforme y te da monedas y gemas. Miau."),
            new Step(0, 244, 570, 388, "Esta es la granja. Arrastra con el dedo para recorrerla. Cuando algo está listo " +
                     "aparece una palabra dorada como ¡COSECHA! o ¡HUEVO!: tócala para recogerlo."),
            new Step(0, 634, 570, 104, "SEMILLAS: cada compra hace que los cultivos valgan más. " +
                     "Cada 10 compras se abre un campo con un cultivo mejor. Mira los cuadritos amarillos."),
            new Step(0, 738, 570, 104, "ANIMALES: cada compra trae un animal más. Cada 10 llega una especie nueva: " +
                     "gallinas, ovejas y vacas. Toca su palabra dorada para recoger huevos, queso o leche."),
            new Step(0, 842, 570, 104, "TRABAJADORES: cosechan solos aunque no toques nada, y cada uno suma +5% a todo. " +
                     "Si un precio sale en rojo, te faltan monedas."),
            new Step(0, 946, 570, 72, "Abajo están TIENDA, LOGROS, JARDINERÍA y AJUSTES. La primera vez que entres " +
                     "a cada una te explico qué hay. Yo me paseo por aquí de vez en cuando. ¡Miau!"),
        },
        // 1 Tienda
        new[]
        {
            new Step("Miau, la TIENDA. Plorp dice que aquí se gasta lo que se gana. Yo solo quiero atún."),
            new Step(26, 216, 258, 54, "BOOSTS: usa gemas para acelerar la granja un rato, ganar el doble o saltar " +
                     "horas de producción de golpe."),
            new Step(286, 216, 258, 54, "GEMAS: packs de gemas con dinero real, desde $50 hasta $1.000. " +
                     "No hace falta comprar nada para avanzar, ¿eh?"),
            new Step(14, 276, 542, 660, "Desliza la lista para ver todo. Si algo cuesta más de lo que tienes, " +
                     "el botón te dice cuánto falta."),
        },
        // 2 Logros
        new[]
        {
            new Step("Miau, aquí están las MISIONES y los LOGROS. Plorp anota todo en una libreta... que se comió una vez."),
            new Step(26, 216, 518, 54, "MISIONES: 3 nuevas cada día (fácil, media y difícil). LOGROS: metas para siempre. " +
                     "Los números entre paréntesis son premios por cobrar."),
            new Step(14, 274, 542, 190, "La RACHA DIARIA: si entras días seguidos, cada día te espera un regalo mejor. " +
                     "¡El día 7 es el grande! Si faltas un día, vuelve al día 1."),
            new Step(14, 464, 542, 470, "Cuando una barra se llena, toca COBRAR. Si cobras las 3 misiones del día, " +
                     "se abre el COFRE DEL DÍA. Desliza para ver más."),
            new Step(228, 946, 116, 72, "Cuando hay algo listo, aparece un número rojo en esta pestaña. ¡No lo dejes ahí!"),
        },
        // 3 Jardinería
        new[]
        {
            new Step("Miau... esta es la JARDINERÍA. Plorp trajo una planta de su planeta y me pidió que la cuidemos."),
            new Step(96, 306, 390, 82, "Estas barras son AGUA, LUZ y CUIDADOS. La zona verde de cada barra es lo que le gusta " +
                     "a la planta. Con los cuidados al 100% sube de nivel; en el nivel 5 da flor."),
            new Step(126, 384, 318, 236, "La planta. Toca las hojas sucias para limpiarlas y aplasta los bichitos: " +
                     "dan monedas de flor. Si los dejas mucho rato, marchitan las hojas."),
            new Step(26, 784, 520, 160, "HERRAMIENTAS: arrástralas con el dedo hasta la planta. Regadera = agua, lámpara = luz, " +
                     "tijeras = cortar hojas secas, abono = revive hojas (si echas mucho, se ensucian)."),
            new Step(88, 308, 398, 272, "La ventana. A veces entra mucho sol o se nubla y hay humedad (no hay que regar). " +
                     "Y si tienes suerte, Plorp pasa en su nave y te tira gemas: ¡tócalas!"),
            new Step(374, 640, 100, 64, "Este botón rojo cierra y abre las CORTINAS. Úsalo cuando entre demasiado sol."),
            new Step(2, 200, 100, 42, "Tus MONEDAS DE FLOR. Salen de los bichitos y de la flor, que las va juntando con el tiempo."),
            new Step(408, 214, 64, 64, "El macetero con el brote es la TIENDA DE SEMILLAS. Ahí se compran otras plantas " +
                     "y cada una da un bonus distinto a la granja. Miau."),
        },
        // 4 Ajustes
        new[]
        {
            new Step(16, 228, 538, 308, "AJUSTES: aquí prendes o apagas la música y los sonidos."),
            new Step(104, 764, 362, 72, "Y si quieres que te vuelva a explicar todo, toca VER GUÍA DE PEPITO. " +
                     "Me encanta hablar. Miau."),
        },
        // 5 Casa de Plorp (the yellow barn; page starts at y = 108)
        new[]
        {
            new Step("¡Miau! Bienvenido a la CASA DE PLORP. Aquí vive... bueno, aquí guarda sus cosas. Vive en la nave."),
            new Step(30, 330, 172, 330, "La COCINA: toca la estufa para cocinar platos con lo del granero. " +
                     "Cuando la olla brilla y dice ¡LISTO!, hay un plato esperando."),
            new Step(308, 398, 208, 252, "El COMPUTADOR de Plorp. Tócalo y se abre PLORP-OS con los minijuegos: " +
                     "pesca, abducción y atrapa huevos. El botón rojo lo apaga."),
            new Step(20, 640, 530, 300, "Y estos son los gatos de la casa. No hacen nada útil, igual que yo. Tócalos, les gusta."),
            new Step(8, 112, 112, 48, "Con VOLVER regresas a la granja. El granero amarillo te avisa cuando hay un plato listo."),
        },
        // 6 Cocina (the stove in the house; page starts at y = 108)
        new[]
        {
            new Step("¡Miau! La COCINA DE PLORP. Aquí lo del granero se convierte en platos. Yo pruebo todo, por seguridad."),
            new Step(16, 222, 538, 214, "Las OLLAS. Cada una cocina un plato con el tiempo, aunque cierres el juego. " +
                     "Cuando brilla y dice ¡LISTO!, tócala para sacar el plato. La tercera olla se compra con gemas."),
            new Step(14, 440, 542, 500, "Las RECETAS. Verde: tienes los ingredientes; rojo: faltan. Toca COCINAR. " +
                     "Los platos valen más que sus ingredientes y Plorp los pide en sus pedidos."),
            new Step(20, 128, 112, 46, "Con VOLVER regresas a la casa de Plorp. Miau."),
        },
    };

    PixelUIFactory ui;
    RectTransform root, frame, arrow, panel;
    readonly Image[] dims = new Image[4];
    readonly Image[] frameEdges = new Image[4];
    SpriteStripAnimator face;
    Text text, counter, nextCaption;
    Step[] steps;
    int section = -1, index;
    float typed;
    Rect shownArea, fromArea;
    float moveT = 1f;
    Action onDone;
    readonly Queue<int> pending = new Queue<int>();

    public static PepitoGuide Create(Transform portrait)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(portrait, "Pepito guide", 0, 0, W, H);
        var guide = rect.gameObject.GetOrAdd<PepitoGuide>();
        guide.Build(ui, rect);
        return guide;
    }

    public static bool Seen(int section) => PlayerPrefs.GetInt(SeenKey + section, 0) == 1;

    /// <summary>Shows the guide for every section again ("VER GUÍA DE PEPITO").</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < Tours.Length; i++) PlayerPrefs.DeleteKey(SeenKey + i);
        PlayerPrefs.Save();
    }

    /// <summary>Shows the tour of a section if the player hasn't seen it. <paramref name="done"/> always runs.</summary>
    public void ShowIfNew(int tab, Action done = null)
    {
        if (tab < 0 || tab >= Tours.Length || Seen(tab))
        {
            done?.Invoke();
            return;
        }
        if (Active)
        {
            if (section != tab && !pending.Contains(tab)) pending.Enqueue(tab);
            return;
        }
        Begin(tab, done);
    }

    /// <summary>The player left the section: its tour (if open) closes without being marked as seen.</summary>
    public void LeaveSection(int tab)
    {
        if (Active && section != tab) Close(false);
    }

    void Build(PixelUIFactory factory, RectTransform rect)
    {
        ui = factory;
        root = rect;

        // Invisible blocker: nothing under the tour can be tapped (not even inside the highlight).
        ui.Backdrop(root, "Blocker", W, H, new Color(0f, 0f, 0f, 0f));

        // Four dark blocks leave a "hole" over the highlighted thing; they also swallow taps.
        for (int i = 0; i < 4; i++)
        {
            dims[i] = ui.Block(root, "Dim " + i, 0, 0, W, H, Dim);
            dims[i].raycastTarget = true;
        }

        frame = ui.Rect(root, "Highlight", 0, 0, 10, 10);
        for (int i = 0; i < 4; i++) frameEdges[i] = ui.Block(frame, "Edge " + i, 0, 0, 4, 4, Gold);

        // Little pixel arrow that bounces above the highlight.
        arrow = ui.Texture(root, "Arrow", "guide_arrow", 0, 0, 30, 24).rectTransform; // Art/UI/guide_arrow.png

        // Pepito's panel: portrait with name plate, bubble, step counter and buttons.
        panel = ui.Rect(root, "Panel", 0, 0, W, PanelH);
        var portraitFrame = ui.Panel(panel, "Portrait frame", 8, 10, 92, 90, Slate);
        portraitFrame.GetComponent<Image>().raycastTarget = true;
        face = SpriteStripAnimator.Create(panel, "Pepito face", "pepito_talk", 16, 17, 76);
        var plate = ui.Panel(panel, "Name plate", 6, 100, 96, 28, PixelUIFactory.Hex("#8A8F9C"));
        ui.Label(plate, "Name", "PEPITO", 0, 0, 96, 28, 15, Color.white, TextAnchor.MiddleCenter);

        ui.Block(panel, "Bubble tail", 100, 44, 10, 16, PixelUIFactory.Ink);
        var bubble = ui.Rect(panel, "Bubble", 106, 6, 458, 166);
        var bg = bubble.gameObject.GetOrAdd<Image>();
        bg.color = PixelUIFactory.Ink;
        ui.Block(bubble, "Fill", 3, 3, 452, 160, Bubble);
        ui.Block(panel, "Bubble tail fill", 103, 47, 10, 10, Bubble);
        text = ui.Paragraph(bubble, "Text", "", 14, 8, 430, 100, 18, PixelUIFactory.Ink);
        text.alignment = TextAnchor.UpperLeft;
        var outline = text.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        // Tapping the bubble finishes the sentence (or goes on).
        var tap = bubble.gameObject.GetOrAdd<Button>();
        tap.targetGraphic = bg;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Next);

        counter = ui.Label(bubble, "Counter", "", 14, 124, 70, 30, 14, PixelUIFactory.Hex("#9A7A5A"));
        var outline2 = counter.GetComponent<Outline>();
        if (outline2 != null) outline2.enabled = false;
        ui.ActionButton(bubble, "Skip", "SALTAR", 92, 118, 132, 40, Skip);
        var skipFace = FarmUIBootstrap.FindChildRecursive(bubble, "Skip");
        if (skipFace != null)
        {
            var faceFill = skipFace.Find("Face");
            if (faceFill != null) faceFill.GetComponent<Image>().color = PixelUIFactory.Hex("#8A6A55");
            var caption = skipFace.Find("Caption");
            if (caption != null) caption.GetComponent<Text>().fontSize = 16;
        }
        ui.ActionButton(bubble, "Next", "SIGUIENTE", 232, 118, 214, 40, Next);
        var nextPanel = FarmUIBootstrap.FindChildRecursive(bubble, "Next");
        nextCaption = nextPanel != null ? nextPanel.Find("Caption").GetComponent<Text>() : null;
        if (nextCaption != null) nextCaption.fontSize = 17;

        root.gameObject.SetActive(false);
    }

    void Begin(int tab, Action done)
    {
        section = tab;
        steps = Tours[tab];
        // Seen from the moment it opens: closing the game halfway must not bring the whole tour back
        // (it can be watched again with "VER GUÍA DE PEPITO" in Ajustes).
        PlayerPrefs.SetInt(SeenKey + tab, 1);
        PlayerPrefs.Save();
        onDone = done;
        index = 0;
        Active = true;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        CurvedPopup.Dismiss(root.parent);
        shownArea = Center();
        ShowStep(true);
        Layout(shownArea, steps[0].Area.width > 0f);
    }

    static Rect Center() => new Rect(W / 2f, H / 2f, 0f, 0f);

    void ShowStep(bool instant)
    {
        var step = steps[index];
        fromArea = shownArea;
        moveT = instant ? 1f : 0f;
        if (instant) shownArea = Target(step);
        typed = 0f;
        text.text = "";
        counter.text = (index + 1) + "/" + steps.Length;
        if (nextCaption != null) nextCaption.text = index == steps.Length - 1 ? "¡ENTENDIDO!" : "SIGUIENTE";
        if (face != null) face.Talk(3);

        // Pepito's panel goes where it doesn't cover the highlight.
        bool highlightLow = step.Area.width > 0f && step.Area.center.y > H * .5f;
        float y = highlightLow ? 112f : H - PanelH - 82f;
        if (step.Area.width > 0f && !highlightLow && step.Area.yMax > y) y = Mathf.Max(step.Area.yMax + 10f, 112f);
        if (step.Area.width > 0f && highlightLow && step.Area.yMin < y + PanelH) y = Mathf.Max(4f, step.Area.yMin - PanelH - 34f);
        panel.anchoredPosition = new Vector2(0f, -y);
        panel.SetAsLastSibling();
    }

    static Rect Target(Step step) => step.Area.width > 0f ? step.Area : Center();

    void Next()
    {
        if (steps == null) return;
        string full = steps[index].Text;
        if (typed < full.Length)
        {
            typed = full.Length; // first tap completes the sentence
            text.text = full;
            return;
        }
        if (index < steps.Length - 1)
        {
            index++;
            ShowStep(false);
        }
        else Close(true);
    }

    void Skip() => Close(true);

    void Close(bool markSeen)
    {
        if (!Active) return;
        if (markSeen && section >= 0)
        {
            PlayerPrefs.SetInt(SeenKey + section, 1);
            PlayerPrefs.Save();
        }
        Active = false;
        steps = null;
        root.gameObject.SetActive(false);
        var done = onDone;
        onDone = null;
        int closed = section;
        section = -1;
        done?.Invoke();
        if (pending.Count > 0)
        {
            int next = pending.Dequeue();
            if (next != closed) ShowIfNew(next);
        }
    }

    void Update()
    {
        if (!Active || steps == null) return;
        float dt = Time.unscaledDeltaTime;
        var step = steps[index];

        string full = step.Text;
        if (typed < full.Length)
        {
            typed = Mathf.Min(full.Length, typed + dt * CharsPerSecond);
            text.text = full.Substring(0, (int)typed);
        }

        // Highlight glides to its next target.
        var target = Target(step);
        if (moveT < 1f)
        {
            moveT = Mathf.Min(1f, moveT + dt / .3f);
            float e = 1f - Mathf.Pow(1f - moveT, 3f);
            shownArea = new Rect(
                Mathf.Lerp(fromArea.x, target.x, e), Mathf.Lerp(fromArea.y, target.y, e),
                Mathf.Lerp(fromArea.width, target.width, e), Mathf.Lerp(fromArea.height, target.height, e));
        }
        else shownArea = target;
        Layout(shownArea, step.Area.width > 0f);
    }

    void Layout(Rect a, bool highlight)
    {
        // Dims: top, bottom, left, right of the hole.
        SetRect(dims[0].rectTransform, 0, 0, W, a.yMin);
        SetRect(dims[1].rectTransform, 0, a.yMax, W, H - a.yMax);
        SetRect(dims[2].rectTransform, 0, a.yMin, a.xMin, a.height);
        SetRect(dims[3].rectTransform, a.xMax, a.yMin, W - a.xMax, a.height);

        frame.gameObject.SetActive(highlight && a.width > 1f);
        arrow.gameObject.SetActive(highlight && a.width > 1f);
        if (!highlight) return;

        float t = Time.unscaledTime;
        float pulse = .5f + .5f * Mathf.Sin(t * 5f);
        const float b = 4f;
        SetRect(frame, a.x - b, a.y - b, a.width + 2 * b, a.height + 2 * b);
        SetRect(frameEdges[0].rectTransform, 0, 0, a.width + 2 * b, b);
        SetRect(frameEdges[1].rectTransform, 0, a.height + b, a.width + 2 * b, b);
        SetRect(frameEdges[2].rectTransform, 0, 0, b, a.height + 2 * b);
        SetRect(frameEdges[3].rectTransform, a.width + b, 0, b, a.height + 2 * b);
        var c = Color.Lerp(Gold, Color.white, pulse * .6f);
        foreach (var edge in frameEdges) edge.color = c;

        // Arrow above the highlight (below it when the highlight touches the top of the screen).
        bool above = a.yMin > 40f;
        float bounce = Mathf.Sin(t * 6f) * 5f;
        float ay = above ? a.yMin - 34f + bounce : a.yMax + 34f - bounce; // flipped arrow grows upwards
        arrow.anchoredPosition = new Vector2(a.center.x - 15f, -ay);
        arrow.localScale = new Vector3(1f, above ? 1f : -1f, 1f);
    }

    static void SetRect(RectTransform r, float x, float y, float w, float h)
    {
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
    }

    void OnDestroy()
    {
        if (Active) Active = false;
    }
}
