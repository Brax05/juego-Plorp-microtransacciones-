using System;
using UnityEngine;
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Purchasing;
#endif

/// <summary>Google Play purchases with signed receipt validation and durable local delivery.</summary>
public static class PurchaseService
{
    static FarmGameState state;
    static Action<string> message;
    public static event Action Changed;

    public static bool TestMode
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static void Initialize(FarmGameState gameState, Action<string> showMessage)
    {
        state = gameState;
        message = showMessage;
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        PurchaseRecoveryDriver.EnsureExists();
        Store.Refresh();
#endif
    }

    public static bool Ready
    {
        get
        {
            if (state == null || PurchaseDeliveryJournal.Faulted) return false;
#if UNITY_EDITOR
            return true;
#elif PLORP_IAP && UNITY_ANDROID
            return Store.ProductsReady && Store.ValidatorReady;
#else
            return false;
#endif
        }
    }

    public static bool Busy
    {
        get
        {
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
            return Store.IsBusy;
#else
            return false;
#endif
        }
    }

    public static string Status
    {
        get
        {
            if (PurchaseDeliveryJournal.Faulted) return "No se pudo guardar una compra. Libera espacio y vuelve a abrir el juego.";
            if (TestMode) return "Modo editor: las compras se simulan";
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
            return Store.Status;
#else
            return "Las compras no están disponibles en esta plataforma";
#endif
        }
    }

    public static bool CanBuy(GemPack pack)
    {
        if (pack == null || !Ready || Busy || (pack.OncePerPlayer && state.StarterPackBought)) return false;
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        var product = Store.Product(pack.ProductId);
        return !Store.DeferredProducts.Contains(pack.ProductId) && !Store.PendingProducts.Contains(pack.ProductId) &&
            product != null && product.availableToPurchase &&
            !string.IsNullOrEmpty(product.metadata.localizedPriceString);
#else
        return TestMode;
#endif
    }

    public static string PriceOf(GemPack pack)
    {
        if (TestMode) return pack.FallbackPrice;
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        var product = Store.Product(pack.ProductId);
        if (product != null && product.availableToPurchase && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
            return product.metadata.localizedPriceString;
        return Store.Loading ? "Cargando…" : "No disponible";
#else
        return "No disponible";
#endif
    }

    public static void Buy(GemPack pack)
    {
        if (pack == null) return;
        if (!CanBuy(pack))
        {
            message?.Invoke(pack.OncePerPlayer && state != null && state.StarterPackBought
                ? "Ya compraste este pack" : Busy ? "Espera a que termine la compra actual" : "La tienda no está disponible. Revisa tu conexión e inténtalo otra vez.");
            return;
        }
#if UNITY_EDITOR
        Grant(pack, "editor-" + Guid.NewGuid().ToString("N"));
#elif PLORP_IAP && UNITY_ANDROID
        Store.Buy(pack);
#endif
    }

    public static void Restore()
    {
        if (Busy) { message?.Invoke("Espera a que termine la compra actual"); return; }
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        message?.Invoke("Revisando compras con Google Play…");
        Store.Refresh(true);
#else
        message?.Invoke(TestMode ? "Las compras del editor son simuladas" : "Las compras no están disponibles");
#endif
    }

    static void Grant(GemPack pack, string token)
    {
        if (state == null) throw new InvalidOperationException("Game state is not ready.");
        if (state.GrantPack(pack, token)) message?.Invoke("¡Gracias! +" + pack.Gems + " gemas");
        NotifyChanged();
    }

    static void NotifyChanged() => Changed?.Invoke();

    public static void Tick()
    {
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        Store.Tick();
#endif
    }

#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
    static class Store
    {
        public static bool ProductsReady, ValidatorReady;
        public static string ActiveProduct;
        public static string Status = "Conectando con Google Play…";
        public static readonly HashSet<string> DeferredProducts = new HashSet<string>();
        public static readonly HashSet<string> PendingProducts = new HashSet<string>();
        static readonly HashSet<string> confirming = new HashSet<string>();
        static StoreController controller;
        static GooglePlayReceiptValidation validator;
        static bool connected, connecting, fetchingProducts, fetchingPurchases, needsRetry, manualRestore;
        static float nextRetry, activeSince;
        public static bool Loading => connecting || fetchingProducts;
        public static bool IsBusy => ActiveProduct != null || confirming.Count > 0 || fetchingPurchases;

        public static Product Product(string id) => ProductsReady ? controller.GetProducts().FirstOrDefault(p => p.definition.id == id) : null;

        static void UpdateStatus(string value)
        {
            Status = value;
            NotifyChanged();
        }

        static void RetryLater(string status)
        {
            needsRetry = true;
            nextRetry = Time.realtimeSinceStartup + 20f;
            UpdateStatus(status);
        }

        public static async void Refresh(bool manual = false)
        {
            if (state == null || PurchaseDeliveryJournal.Faulted || connecting || fetchingProducts || fetchingPurchases) return;
            manualRestore |= manual;
            try
            {
                if (validator == null)
                {
                    if (!GooglePlayLicenseKey.IsConfigured) { UpdateStatus("La tienda está temporalmente fuera de servicio"); return; }
                    validator = new GooglePlayReceiptValidation(GooglePlayLicenseKey.Data(), Application.identifier);
                    ValidatorReady = true;
                }
                if (controller == null)
                {
                    controller = UnityIAPServices.StoreController();
                    controller.ProcessPendingOrdersOnPurchasesFetched(false);
                    controller.OnStoreConnected += OnConnected;
                    controller.OnStoreDisconnected += OnDisconnected;
                    controller.OnProductsFetched += OnProductsFetched;
                    controller.OnProductsFetchFailed += OnProductsFetchFailed;
                    controller.OnPurchasesFetched += OnPurchasesFetched;
                    controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                    controller.OnPurchasePending += OnPurchasePending;
                    controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    controller.OnPurchaseFailed += OnPurchaseFailed;
                    controller.OnPurchaseDeferred += OnPurchaseDeferred;
                }
                if (!connected)
                {
                    connecting = true;
                    UpdateStatus("Conectando con Google Play…");
                    await controller.Connect();
                    connecting = false;
                    if (!connected) return;
                }
                needsRetry = false;
                if (!ProductsReady)
                {
                    fetchingProducts = true;
                    UpdateStatus("Cargando precios de Google Play…");
                    controller.FetchProducts(ShopCatalog.Packs.Select(p => new ProductDefinition(p.ProductId,
                        p.OncePerPlayer ? ProductType.NonConsumable : ProductType.Consumable)).ToList());
                }
                else FetchPurchases();
            }
            catch
            {
                connecting = fetchingProducts = fetchingPurchases = false;
                RetryLater("No se pudo conectar con la tienda. Puedes reintentar.");
                Debug.LogWarning("IAP: no se pudo preparar la tienda o la clave de validación.");
            }
        }

        static void OnConnected() { connected = true; }
        static void OnDisconnected(StoreConnectionFailureDescription failure)
        {
            connected = ProductsReady = false;
            fetchingProducts = fetchingPurchases = false;
            RetryLater("Se perdió la conexión con la tienda. Reintentando…");
        }

        static void OnProductsFetched(List<Product> products)
        {
            fetchingProducts = false;
            ProductsReady = products != null && products.Any(p => p.availableToPurchase);
            if (!ProductsReady) { RetryLater("No hay productos disponibles. Puedes reintentar."); return; }
            UpdateStatus("Pago con Google Play");
            FetchPurchases();
        }

        static void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            fetchingProducts = false;
            ProductsReady = false;
            RetryLater("No se pudieron cargar los precios. Revisa tu conexión.");
        }

        static void FetchPurchases()
        {
            if (fetchingPurchases || !connected) return;
            fetchingPurchases = true;
            NotifyChanged();
            controller.FetchPurchases();
        }

        static void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            fetchingPurchases = false;
            RetryLater("No se pudieron revisar las compras. Reintentaremos al conectar.");
        }

        static void OnPurchasesFetched(Orders orders)
        {
            fetchingPurchases = false;
            DeferredProducts.Clear();
            if (orders == null) { RetryLater("No se pudieron revisar las compras"); return; }
            PendingProducts.Clear();
            var pendingTokens = new HashSet<string>();
            if (orders.PendingOrders != null)
                foreach (var pending in orders.PendingOrders)
                    if (pending?.Info?.Google?.PurchaseToken != null) pendingTokens.Add(pending.Info.Google.PurchaseToken);
            confirming.RemoveWhere(token => !pendingTokens.Contains(token));
            if (orders.DeferredOrders != null)
                foreach (var order in orders.DeferredOrders) RememberDeferred(order);
            if (orders.PendingOrders != null)
                foreach (var order in orders.PendingOrders) OnPurchasePending(order);
            if (orders.ConfirmedOrders != null)
                foreach (var order in orders.ConfirmedOrders)
                {
                    var pack = PackOf(order);
                    if (pack == null || !pack.OncePerPlayer || state.StarterPackBought) continue;
                    if (!TryDeliver(order, pack)) RetryLater("Una compra necesita revisión. Puedes reintentar.");
                }
            if (manualRestore)
            {
                manualRestore = false;
                message?.Invoke(needsRetry ? "Hay compras pendientes de revisión. Puedes reintentar." : "Compras revisadas con Google Play");
            }
            if (ActiveProduct == null && !needsRetry) UpdateStatus(DeferredProducts.Count > 0
                ? "Hay una compra esperando aprobación o pago" : "Pago con Google Play");
        }

        static GemPack PackOf(Order order)
        {
            var items = order?.CartOrdered?.Items();
            return items != null && items.Count == 1 && items[0].Quantity == 1
                ? ShopCatalog.Pack(items[0].Product.definition.id) : null;
        }

        static bool TryDeliver(Order order, GemPack pack)
        {
            try
            {
                string token = validator.Validate(order.Info.Receipt, pack.ProductId, order.Info.Google?.PurchaseToken);
                Grant(pack, token);
                return true;
            }
            catch
            {
                message?.Invoke(PurchaseDeliveryJournal.Faulted
                    ? "No se pudo guardar la compra. Libera espacio y vuelve a abrir el juego."
                    : "No se pudo verificar la compra. Revisa tus compras e inténtalo otra vez.");
                Debug.LogWarning("IAP: entrega detenida por un recibo inválido o un error de guardado.");
                return false;
            }
        }

        public static void Buy(GemPack pack)
        {
            ActiveProduct = pack.ProductId;
            activeSince = Time.realtimeSinceStartup;
            UpdateStatus("Compra en curso. Completa el pago en Google Play…");
            try { controller.PurchaseProduct(Product(pack.ProductId)); }
            catch
            {
                ActiveProduct = null;
                RetryLater("No se pudo abrir el pago. Puedes reintentar.");
            }
        }

        static void OnPurchasePending(PendingOrder order)
        {
            var pack = PackOf(order);
            string token = order?.Info?.Google?.PurchaseToken;
            if (pack == null || string.IsNullOrEmpty(token))
            {
                ReleaseActive(order);
                RetryLater("No se pudo verificar una compra. Revisa tus compras.");
                return;
            }
            if (confirming.Contains(token)) return;
            PendingProducts.Add(pack.ProductId);
            if (!TryDeliver(order, pack))
            {
                ReleaseActive(order);
                RetryLater("Una compra necesita revisión. Puedes reintentar.");
                return;
            }
            DeferredProducts.Remove(pack.ProductId);
            confirming.Add(token);
            activeSince = Time.realtimeSinceStartup;
            UpdateStatus("Confirmando la compra con Google Play…");
            try { controller.ConfirmPurchase(order); }
            catch
            {
                confirming.Remove(token);
                ReleaseActive(order);
                RetryLater("Tu compra está guardada. Falta confirmarla con Google Play.");
            }
        }

        static void OnPurchaseConfirmed(Order order)
        {
            if (order?.Info?.Google?.PurchaseToken != null) confirming.Remove(order.Info.Google.PurchaseToken);
            ReleaseActive(order);
            if (order is FailedOrder)
                RetryLater("Tu compra está guardada. Reintentaremos la confirmación.");
            else
            {
                var pack = PackOf(order);
                if (pack != null) PendingProducts.Remove(pack.ProductId);
                UpdateStatus("Pago con Google Play");
            }
        }

        static void OnPurchaseFailed(FailedOrder order)
        {
            ReleaseActive(order);
            if (order.FailureReason == PurchaseFailureReason.UserCancelled)
            {
                message?.Invoke("Compra cancelada");
                UpdateStatus("Pago con Google Play");
            }
            else
            {
                message?.Invoke("La compra no se completó. Puedes revisar tus compras.");
                RetryLater("No se completó la compra. Puedes reintentar.");
            }
        }

        static void RememberDeferred(Order order)
        {
            var pack = PackOf(order);
            if (pack != null) DeferredProducts.Add(pack.ProductId);
        }

        static void OnPurchaseDeferred(DeferredOrder order)
        {
            RememberDeferred(order);
            ReleaseActive(order);
            message?.Invoke("El pago está pendiente. Recibirás el pack cuando Google Play lo apruebe.");
            RetryLater("Compra pendiente de aprobación o pago");
        }

        public static void Tick()
        {
            if ((ActiveProduct != null || confirming.Count > 0) && Time.realtimeSinceStartup - activeSince > 120f)
            {
                ActiveProduct = null;
                confirming.Clear();
                RetryLater("Revisando el estado de tu compra…");
            }
            if ((needsRetry || DeferredProducts.Count > 0) && Time.realtimeSinceStartup >= nextRetry &&
                Application.internetReachability != NetworkReachability.NotReachable)
            {
                nextRetry = Time.realtimeSinceStartup + 20f;
                Refresh();
            }
        }

        static void ReleaseActive(Order order)
        {
            if (PackOf(order)?.ProductId == ActiveProduct) ActiveProduct = null;
        }
    }
#endif
}
