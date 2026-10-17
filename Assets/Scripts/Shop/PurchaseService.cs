using System;
using UnityEngine;
#if PLORP_IAP
using Unity.Services.Core;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

/// <summary>
/// Real-money purchases of gem packs (Google Play / App Store) through Unity In-App Purchasing.
///
/// How it works:
///  1. Initialize() registers every ShopCatalog.Packs product id with the store.
///  2. Buy(pack) opens the store's payment sheet.
///  3. When the store confirms, ProcessPurchase grants the pack (FarmGameState.GrantPack) and saves.
///     Purchases the store still has pending (app closed mid-purchase) are delivered on the next start.
///
/// Requirements: the "In-App Purchasing" package (com.unity.purchasing) and the PLORP_IAP scripting
/// define (both already set in Packages/manifest.json and ProjectSettings). Without them the store runs
/// in TEST MODE: purchases are simulated and granted immediately, so the flow can be tried in the Editor.
///
/// Before publishing: create the same product ids in Google Play Console (consumables; the starter pack
/// as a one-time product) and validate receipts on a server (see ValidateReceipt).
/// </summary>
public static class PurchaseService
{
    static FarmGameState state;
    static Action<string> message;

    public static bool TestMode
    {
        get
        {
#if PLORP_IAP
            return false;
#else
            return true;
#endif
        }
    }

    /// <summary>Raised when prices load or a purchase finishes (store page refreshes).</summary>
    public static event Action Changed;

    public static void Initialize(FarmGameState gameState, Action<string> showMessage)
    {
        state = gameState;
        message = showMessage;
#if PLORP_IAP
        Store.Init();
#endif
    }

    public static bool Ready
    {
        get
        {
#if PLORP_IAP
            return Store.Controller != null;
#else
            return true;
#endif
        }
    }

    /// <summary>Localized price from the store ("$500"), or the CLP fallback while loading / in test mode.</summary>
    public static string PriceOf(GemPack pack)
    {
#if PLORP_IAP
        var product = Store.Controller?.products.WithID(pack.ProductId);
        if (product != null && product.availableToPurchase && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
            return product.metadata.localizedPriceString;
#endif
        return pack.FallbackPrice;
    }

    public static void Buy(GemPack pack)
    {
        if (pack == null) return;
        if (pack.OncePerPlayer && state != null && state.StarterPackBought)
        {
            message?.Invoke("Ya compraste este pack");
            return;
        }
#if PLORP_IAP
        if (Store.Controller == null)
        {
            message?.Invoke("La tienda aún no está lista. Revisa tu conexión.");
            Store.Init();
            return;
        }
        Store.Controller.InitiatePurchase(pack.ProductId);
#else
        Grant(pack);
        message?.Invoke("Modo prueba: compra simulada de " + pack.Name);
#endif
    }

    /// <summary>iOS needs a "restore purchases" button for the one-time starter pack.</summary>
    public static void Restore()
    {
#if PLORP_IAP && UNITY_IOS
        Store.Extensions?.GetExtension<IAppleExtensions>()?.RestoreTransactions((ok, error) =>
            message?.Invoke(ok ? "Compras restauradas" : "No se pudo restaurar: " + error));
#else
        message?.Invoke("En Android las compras se restauran solas al abrir el juego");
#endif
    }

    static void Grant(GemPack pack)
    {
        if (pack == null || state == null) return;
        if (pack.OncePerPlayer && state.StarterPackBought) return; // already delivered (restore)
        state.GrantPack(pack);
        message?.Invoke("¡Gracias! +" + pack.Gems + " gemas");
        Changed?.Invoke();
    }

    /// <summary>
    /// Hook for receipt validation. In production send <paramref name="receipt"/> to your server
    /// (for example Unity Cloud Code or your own backend calling the Google Play Developer API)
    /// and only grant when it confirms. Returning true trusts the store response.
    /// </summary>
    static bool ValidateReceipt(string receipt) => !string.IsNullOrEmpty(receipt);

#if PLORP_IAP
    sealed class Store : IStoreListener
    {
        public static IStoreController Controller;
        public static IExtensionProvider Extensions;
        static bool initializing;

        public static async void Init()
        {
            if (Controller != null || initializing) return;
            initializing = true;
            try
            {
                await UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Unity Services init failed (IAP continues): " + e.Message);
            }
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var pack in ShopCatalog.Packs)
                builder.AddProduct(pack.ProductId, pack.OncePerPlayer ? ProductType.NonConsumable : ProductType.Consumable);
#pragma warning disable 0618
            UnityPurchasing.Initialize(new Store(), builder);
#pragma warning restore 0618
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Controller = controller;
            Extensions = extensions;
            initializing = false;
            Changed?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string details)
        {
            initializing = false;
            Debug.LogWarning("IAP init failed: " + error + " " + details);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            var pack = ShopCatalog.Pack(product.definition.id);
            if (pack == null) return PurchaseProcessingResult.Complete;
            if (!ValidateReceipt(product.receipt))
            {
                message?.Invoke("No se pudo verificar la compra");
                return PurchaseProcessingResult.Complete;
            }
            Grant(pack);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            if (reason != PurchaseFailureReason.UserCancelled) message?.Invoke("La compra no se completó");
        }
    }
#endif
}
