using System;
using System.Text;
using UnityEngine;
#if PLORP_PGS && UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

/// <summary>
/// Copies the saved game (<see cref="SaveStore"/>) to the player's Google Play Juegos account
/// ("Partidas guardadas"), so it comes back after reinstalling or on another phone.
///
/// - At start it signs in on its own (Play Games v2 does it without a button) and reads the cloud copy.
///   If the cloud copy was played longer than this phone's, it replaces the local save and the farm reloads.
///   If not, the local save is uploaded.
/// - Then it uploads after changes (at most once a minute), and when the game goes to the background.
/// - It never overwrites a cloud copy that was played longer (another phone kept playing).
/// - Without sign-in, without internet or without the plugin, the game keeps saving only on the phone.
///
/// Turned on with the scripting define <c>PLORP_PGS</c> (Android) once the Google Play Games plugin is
/// imported; without it this class only reports "solo en este teléfono".
/// </summary>
public sealed class CloudSave : MonoBehaviour
{
    public enum State { Off, SigningIn, SignedOut, Syncing, Ready, Error }

    const string FileName = "plorp_farm";
    const float UploadEvery = 60f, RetryEvery = 90f;
    // Cloud must be ahead by more than this to replace the phone's save (clock noise between saves).
    const double RestoreMargin = 30d;

    static CloudSave instance;
    static string pendingMessage;

    /// <summary>What the cloud save is doing now (shown in AJUSTES ▸ PARTIDA GUARDADA).</summary>
    public static State Status { get; private set; } = State.Off;
    /// <summary>Raised when <see cref="Status"/> changes.</summary>
    public static event Action StatusChanged;
    /// <summary>Local time of the last successful upload, or null.</summary>
    public static DateTime? LastUpload { get; private set; }

    /// <summary>True when this build can use Google Play Juegos (plugin imported, Android phone).</summary>
    public static bool Available
    {
        get
        {
#if PLORP_PGS && UNITY_ANDROID
            return !Application.isEditor;
#else
            return false;
#endif
        }
    }

    public static bool SignedIn => Status == State.Ready || Status == State.Syncing;

    /// <summary>One line for the PARTIDA GUARDADA card.</summary>
    public static string StatusLine
    {
        get
        {
            if (!Available) return "Tu partida se guarda solo en este teléfono.";
            switch (Status)
            {
                case State.SigningIn: return "Conectando con Google Play Juegos...";
                case State.Syncing: return "Sincronizando tu partida con Google Play Juegos...";
                case State.Ready:
                    if (cloudIsAhead)
                        return "Tu cuenta de Google Play Juegos tiene una partida más avanzada. Cierra y vuelve a abrir el juego para traerla.";
                    return "Tu partida se guarda en este teléfono y en tu cuenta de Google Play Juegos." +
                           (LastUpload.HasValue ? " Última copia: " + LastUpload.Value.ToString("HH:mm") + "." : "");
                case State.Error: return "No se pudo conectar con Google Play Juegos. Se reintentará solo.";
                default: return "No iniciaste sesión en Google Play Juegos: tu partida se guarda solo en este teléfono.";
            }
        }
    }

    // The cloud was played longer than this phone (another phone kept playing): never overwrite it.
    // Its copy comes back the next time the game starts.
    static bool cloudIsAhead;
    bool dirty, busy;
    float nextUpload, nextRetry;
    double unsavedPlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("Plorp cloud save");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CloudSave>();
    }

    void Start()
    {
        SaveStore.Saved += MarkDirty;
        if (Available) SignIn(false);
    }

    void OnDestroy() => SaveStore.Saved -= MarkDirty;

    void MarkDirty() => dirty = true;

    /// <summary>"CONECTAR" button: asks Google Play Juegos to sign in (shows Google's window).</summary>
    public static void Connect(Action<string> message)
    {
        if (!Available)
        {
            message?.Invoke("Esta versión del juego guarda solo en el teléfono.");
            return;
        }
        if (instance == null) Boot();
        if (SignedIn)
        {
            message?.Invoke("Ya estás conectado a Google Play Juegos.");
            return;
        }
        instance.SignIn(true);
    }

    /// <summary>After a restore the farm reloads; Pepito tells it once the farm is on screen.</summary>
    public static void AnnounceTo(PepitoController pepito)
    {
        if (pendingMessage == null || pepito == null) return;
        pepito.Say(pendingMessage, 8f);
        pendingMessage = null;
    }

    static void SetStatus(State value)
    {
        if (Status == value) return;
        Status = value;
        StatusChanged?.Invoke();
    }

    void Update()
    {
        // Play time decides which copy wins: count it while the game is in front.
        if (Application.isFocused) unsavedPlay += Time.unscaledDeltaTime;
        if (unsavedPlay >= 10d)
        {
            SaveStore.AddPlayTime(unsavedPlay);
            unsavedPlay = 0d;
        }

        if (!Available || busy) return;
        float now = Time.unscaledTime;
        if (Status == State.Error && now >= nextRetry) SignIn(false);
        else if (Status == State.Ready && dirty && !cloudIsAhead && now >= nextUpload) Upload();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        SaveStore.AddPlayTime(unsavedPlay);
        unsavedPlay = 0d;
        SaveStore.Save();
        if (Available && Status == State.Ready && !busy && !cloudIsAhead) Upload();
    }

    void OnApplicationQuit()
    {
        SaveStore.AddPlayTime(unsavedPlay);
        unsavedPlay = 0d;
        SaveStore.Save();
    }

#if PLORP_PGS && UNITY_ANDROID
    static ISavedGameClient Client => PlayGamesPlatform.Instance.SavedGame;

    void SignIn(bool manual)
    {
        if (busy) return;
        busy = true;
        SetStatus(State.SigningIn);
        PlayGamesPlatform.Activate();
        Action<SignInStatus> done = result =>
        {
            busy = false;
            if (result == SignInStatus.Success) Download();
            else SetStatus(State.SignedOut);
        };
        if (manual) PlayGamesPlatform.Instance.ManuallyAuthenticate(done);
        else PlayGamesPlatform.Instance.Authenticate(done);
    }

    void Download()
    {
        busy = true;
        SetStatus(State.Syncing);
        Client.OpenWithAutomaticConflictResolution(FileName, DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseLongestPlaytime, (status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    Fail();
                    return;
                }
                Client.ReadBinaryData(game, (readStatus, data) =>
                {
                    busy = false;
                    if (readStatus != SavedGameRequestStatus.Success)
                    {
                        Fail();
                        return;
                    }
                    Decide(data);
                });
            });
    }

    void Upload()
    {
        busy = true;
        dirty = false;
        SaveStore.AddPlayTime(unsavedPlay);
        unsavedPlay = 0d;
        var snapshot = SaveStore.Capture();
        byte[] bytes = Encoding.UTF8.GetBytes(SaveStore.ToJson(snapshot));
        Client.OpenWithAutomaticConflictResolution(FileName, DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseLongestPlaytime, (status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    busy = false;
                    dirty = true;
                    nextUpload = Time.unscaledTime + UploadEvery;
                    return;
                }
                // Another phone played longer in the meantime: keep its copy (it comes back on the next start).
                if (game.TotalTimePlayed.TotalSeconds > snapshot.playSeconds + RestoreMargin)
                {
                    busy = false;
                    cloudIsAhead = true;
                    StatusChanged?.Invoke();
                    return;
                }
                var update = new SavedGameMetadataUpdate.Builder()
                    .WithUpdatedPlayedTime(TimeSpan.FromSeconds(snapshot.playSeconds))
                    .WithUpdatedDescription("Granja de Plorp · nivel " + SaveStore.GetInt("GlorpFarm.farmLevel", 1))
                    .Build();
                Client.CommitUpdate(game, update, bytes, (commitStatus, saved) =>
                {
                    busy = false;
                    nextUpload = Time.unscaledTime + UploadEvery;
                    if (commitStatus == SavedGameRequestStatus.Success)
                    {
                        LastUpload = DateTime.Now;
                        StatusChanged?.Invoke();
                    }
                    else dirty = true;
                });
            });
    }
#else
    void SignIn(bool manual) { }
    void Upload() { }
#endif

    /// <summary>First read of the cloud copy: bring it back, or upload this phone's save.</summary>
    void Decide(byte[] data)
    {
        var cloud = data != null && data.Length > 0 ? SaveStore.FromJson(Encoding.UTF8.GetString(data)) : null;
        SetStatus(State.Ready);
        if (cloud != null && cloud.playSeconds > SaveStore.PlaySeconds + unsavedPlay + RestoreMargin)
        {
            // A purchase half-delivered on this phone must finish first (its journal holds absolute balances).
            if (PurchaseDeliveryJournal.HasPendingDelivery)
            {
                Debug.LogWarning("Plorp: hay una compra a medio entregar; no se trae la partida de la nube todavía.");
                cloudIsAhead = true;
                StatusChanged?.Invoke();
                return;
            }
            unsavedPlay = 0d;
            SaveStore.Apply(cloud);
            SaveStore.Frozen = true; // until the farm is rebuilt from it
            dirty = false;
            pendingMessage = "Miau... ¡Plorp encontró tu granja en Google Play Juegos! La traje tal como la dejaste.";
            FarmUIBootstrap.ReloadFarm();
            return;
        }
        if (cloudIsAhead) return;
        dirty = true;
        nextUpload = 0f; // first copy right away
    }

    void Fail()
    {
        busy = false;
        nextRetry = Time.unscaledTime + RetryEvery;
        SetStatus(State.Error);
    }
}
