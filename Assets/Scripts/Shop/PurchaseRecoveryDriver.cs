using UnityEngine;

/// <summary>Retries interrupted store operations while the app is running.</summary>
public sealed class PurchaseRecoveryDriver : MonoBehaviour
{
    static PurchaseRecoveryDriver instance;

    public static void EnsureExists()
    {
        if (instance != null) return;
        var runner = new GameObject("Purchase recovery");
        instance = runner.AddComponent<PurchaseRecoveryDriver>();
        DontDestroyOnLoad(runner);
    }

    void Update() => PurchaseService.Tick();
    void OnApplicationPause(bool paused) { if (!paused) PurchaseService.Tick(); }
    void OnDestroy() { if (instance == this) instance = null; }
}
