using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
#if PLORP_REFUNDS && PLORP_PGS && UNITY_ANDROID
using GooglePlayGames;
#endif

/// <summary>
/// Talks to the refund server (Server/ in the repo, Firebase functions):
/// - every purchase the game delivers is registered there with the player's Play Games id;
/// - every few minutes it asks for "deductions": purchases Google voided (refund, chargeback). Their gems are
///   taken back (the balance may go below zero) and Pepito says why.
///
/// Turned on with the scripting defines <c>PLORP_REFUNDS</c> and <c>PLORP_PGS</c> (it needs the player
/// signed in to Google Play Juegos). Without them it does nothing. Pending registrations are kept in the save
/// (also in the cloud copy) and retried until the server confirms them.
/// </summary>
public sealed class RefundSync : MonoBehaviour
{
    /// <summary>Where the functions are deployed: https://REGION-PROJECT.cloudfunctions.net (no slash at the end).</summary>
    public const string ServerUrl = "https://southamerica-east1-PONER-AQUI-EL-ID-DEL-PROYECTO.cloudfunctions.net";

    const string QueueKey = "GlorpFarm.refund.queue";     // "productId\ttoken" per line, not yet registered
    const string AppliedKey = "GlorpFarm.refund.applied"; // deduction ids already taken from the balance
    const float SyncEvery = 600f, RetryEvery = 120f;

    /// <summary>Gems taken back for a refunded purchase (Pepito explains it).</summary>
    public static event Action<int> Revoked;

    static RefundSync instance;
    float nextSync = 20f;
    bool running;

    public static bool Enabled
    {
        get
        {
#if PLORP_REFUNDS && PLORP_PGS && UNITY_ANDROID
            return !Application.isEditor;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || instance != null) return;
        var go = new GameObject("Plorp refund sync");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RefundSync>();
    }

    /// <summary>The game just delivered this purchase: register it (now or as soon as possible).</summary>
    public static void Register(string productId, string purchaseToken)
    {
        if (!Enabled || string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(purchaseToken)) return;
        var queue = Lines(QueueKey);
        string line = productId + "\t" + purchaseToken;
        if (!queue.Contains(line)) queue.Add(line);
        WriteLines(QueueKey, queue);
        SaveStore.Save();
        if (instance != null) instance.nextSync = 0f;
    }

    void Update()
    {
        if (running || !CloudSave.SignedIn || Time.unscaledTime < nextSync) return;
        StartCoroutine(Sync());
    }

    IEnumerator Sync()
    {
        running = true;
        bool failed = false;

        // 1. Purchases waiting to be registered.
        foreach (string line in Lines(QueueKey).ToArray())
        {
            string[] parts = line.Split('\t');
            if (parts.Length != 2) { RemoveLine(QueueKey, line); continue; }
            string code = null;
            yield return AuthCode(c => code = c);
            if (string.IsNullOrEmpty(code)) { failed = true; break; }
            var body = JsonUtility.ToJson(new RegisterBody { authCode = code, productId = parts[0], purchaseToken = parts[1] });
            long status = 0;
            yield return Post("/registerpurchase", body, (s, _) => status = s);
            // 200 registered · 4xx the server will never accept it (unknown, someone else's): stop retrying.
            if (status == 200 || (status >= 400 && status < 500 && status != 401 && status != 429)) RemoveLine(QueueKey, line);
            else { failed = true; break; }
        }

        // 2. Deductions: confirm the ones applied, apply the new ones.
        if (!failed)
        {
            string code = null;
            yield return AuthCode(c => code = c);
            if (string.IsNullOrEmpty(code)) failed = true;
            else
            {
                var applied = Lines(AppliedKey);
                var body = JsonUtility.ToJson(new SyncBody { authCode = code, applied = applied.ToArray() });
                string response = null;
                long status = 0;
                yield return Post("/refundsync", body, (s, text) => { status = s; response = text; });
                if (status == 200 && response != null) Apply(response, applied);
                else failed = true;
            }
        }

        SaveStore.Save();
        nextSync = Time.unscaledTime + (failed ? RetryEvery : SyncEvery);
        running = false;
    }

    static void Apply(string response, List<string> applied)
    {
        SyncResponse parsed = null;
        try { parsed = JsonUtility.FromJson<SyncResponse>(response); }
        catch (Exception) { }
        var stillPending = new HashSet<string>();
        int total = 0;
        if (parsed?.pending != null)
        {
            foreach (var d in parsed.pending)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || d.gems <= 0) continue;
                stillPending.Add(d.id);
                if (applied.Contains(d.id)) continue; // already taken; the server learns it on this sync
                var state = FarmEconomy.Current;
                if (state == null) continue;
                state.RevokeGems(d.gems);
                applied.Add(d.id);
                total += d.gems;
            }
        }
        // Ids the server no longer returns are confirmed there: forget them.
        applied.RemoveAll(id => !stillPending.Contains(id));
        WriteLines(AppliedKey, applied);
        if (total > 0) Revoked?.Invoke(total);
    }

    // ---------- Helpers ----------

    static IEnumerator AuthCode(Action<string> done)
    {
#if PLORP_REFUNDS && PLORP_PGS && UNITY_ANDROID
        bool finished = false;
        string result = null;
        PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => { result = code; finished = true; });
        float giveUp = Time.unscaledTime + 30f;
        while (!finished && Time.unscaledTime < giveUp) yield return null;
        done(result);
#else
        done(null);
        yield break;
#endif
    }

    static IEnumerator Post(string path, string json, Action<long, string> done)
    {
        using (var request = new UnityWebRequest(ServerUrl + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;
            yield return request.SendWebRequest();
            done(request.result == UnityWebRequest.Result.ConnectionError ? 0 : request.responseCode,
                request.downloadHandler != null ? request.downloadHandler.text : null);
        }
    }

    static List<string> Lines(string key)
    {
        var list = new List<string>();
        foreach (string line in SaveStore.GetString(key, "").Split('\n'))
            if (line.Length > 0) list.Add(line);
        return list;
    }

    static void WriteLines(string key, List<string> lines) => SaveStore.SetString(key, string.Join("\n", lines));

    static void RemoveLine(string key, string line)
    {
        var list = Lines(key);
        list.Remove(line);
        WriteLines(key, list);
    }

    [Serializable] sealed class RegisterBody { public string authCode, productId, purchaseToken; }
    [Serializable] sealed class SyncBody { public string authCode; public string[] applied; }
    [Serializable] sealed class SyncResponse { public PendingDeduction[] pending; }
    [Serializable] sealed class PendingDeduction { public string id; public int gems; public string productId; }
}
