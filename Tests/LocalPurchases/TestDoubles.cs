using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public static class Application
    {
        public static string persistentDataPath;
        public static string identifier = "com.glorpfarm.app";
        public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
    }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Mathf { public static int CeilToInt(float value) => (int)Math.Ceiling(value); }
    public static class Debug
    {
        public static void LogWarning(object value) { }
        public static void LogError(object value) { }
    }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(object value) => JsonSerializer.Serialize(value, options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, options);
    }
    public static class PlayerPrefs
    {
        static Dictionary<string, object> values = new Dictionary<string, object>();
        static Dictionary<string, object> saved = new Dictionary<string, object>();
        public static bool FailSave;
        public static int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out var value) ? (int)value : fallback;
        public static string GetString(string key, string fallback = "") => values.TryGetValue(key, out var value) ? (string)value : fallback;
        public static void SetInt(string key, int value) => values[key] = value;
        public static void SetString(string key, string value) => values[key] = value;
        public static float GetFloat(string key, float fallback = 0f) => values.TryGetValue(key, out var value) ? (float)value : fallback;
        public static void SetFloat(string key, float value) => values[key] = value;
        public static bool HasKey(string key) => values.ContainsKey(key);
        public static void DeleteKey(string key) => values.Remove(key);
        public static void Save()
        {
            if (FailSave) throw new IOException("Injected storage failure");
            saved = new Dictionary<string, object>(values);
        }
        public static void Crash() { FailSave = false; values = new Dictionary<string, object>(saved); }
        public static void Clear() { values.Clear(); saved.Clear(); FailSave = false; }
    }
}

namespace Purchasing.Utilities { public static class StoreKitSelector { public static bool UseStoreKit1() => false; } }
namespace UnityEngine.Purchasing.Extension { public sealed class UnusedNamespaceMarker { } }

public static class GooglePlayLicenseKey
{
    public static readonly bool IsConfigured = true;
    public static byte[] Key;
    public static byte[] Data() => Key;
}
public static class PurchaseRecoveryDriver { public static void EnsureExists() { } }

// Game state is a boundary double for the store flow tests. The real delivery
// journal is tested separately, including simulated failures and hard restarts.
public sealed class FarmGameState
{
    public bool StarterPackBought;
    public int Delivered;
    readonly HashSet<string> tokens = new HashSet<string>();
    public bool ThrowOnDelivery;
    public bool GrantPack(GemPack pack, string token)
    {
        if (ThrowOnDelivery) throw new IOException("Injected failed save");
        if (!tokens.Add(token)) return false;
        Delivered++;
        if (pack.OncePerPlayer) StarterPackBought = true;
        return true;
    }
}

namespace UnityEngine.Purchasing
{
    public enum ProductType { Consumable, NonConsumable }
    public enum PurchaseFailureReason { UserCancelled, Unknown }
    public sealed class ProductDefinition
    {
        public string id;
        public ProductDefinition(string id, ProductType type) { this.id = id; }
    }
    public sealed class ProductMetadata { public string localizedPriceString = "$200"; }
    public sealed class Product
    {
        public ProductDefinition definition;
        public bool availableToPurchase = true;
        public ProductMetadata metadata = new ProductMetadata();
    }
    public sealed class CartItem { public int Quantity = 1; public Product Product; }
    public sealed class Cart
    {
        public List<CartItem> Entries = new List<CartItem>();
        public List<CartItem> Items() => Entries;
    }
    public sealed class GoogleInfo { public string PurchaseToken; }
    public sealed class OrderInfo { public string Receipt; public GoogleInfo Google; }
    public class Order { public Cart CartOrdered; public OrderInfo Info; }
    public sealed class PendingOrder : Order { }
    public sealed class DeferredOrder : Order { }
    public sealed class ConfirmedOrder : Order { }
    public sealed class FailedOrder : Order { public PurchaseFailureReason FailureReason; }
    public sealed class Orders
    {
        public List<PendingOrder> PendingOrders = new List<PendingOrder>();
        public List<ConfirmedOrder> ConfirmedOrders = new List<ConfirmedOrder>();
        public List<DeferredOrder> DeferredOrders = new List<DeferredOrder>();
    }
    public sealed class StoreConnectionFailureDescription { }
    public sealed class ProductFetchFailed { }
    public sealed class PurchasesFetchFailureDescription { }
    public static class UnityIAPServices { public static StoreController StoreController() => Purchasing.StoreController.Instance; }
    public sealed class StoreController
    {
        public static StoreController Instance = new StoreController();
        public event Action OnStoreConnected;
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed;
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<DeferredOrder> OnPurchaseDeferred;
        public List<Product> Products = new List<Product>();
        public Orders History = new Orders();
        public int BuyCalls, ConfirmCalls;
        public bool FailProducts, FailFetch;
        public Action BeforeConfirm;
        public Task Connect() { OnStoreConnected?.Invoke(); return Task.CompletedTask; }
        public void ProcessPendingOrdersOnPurchasesFetched(bool value) { }
        public List<Product> GetProducts() => Products;
        public void FetchProducts(List<ProductDefinition> definitions)
        {
            if (FailProducts) { OnProductsFetchFailed?.Invoke(new ProductFetchFailed()); return; }
            Products = definitions.Select(d => new Product { definition = d }).ToList();
            OnProductsFetched?.Invoke(Products);
        }
        public void FetchPurchases()
        {
            if (FailFetch) OnPurchasesFetchFailed?.Invoke(new PurchasesFetchFailureDescription());
            else OnPurchasesFetched?.Invoke(History);
        }
        public void PurchaseProduct(Product product) { BuyCalls++; }
        public void ConfirmPurchase(PendingOrder order) { BeforeConfirm?.Invoke(); ConfirmCalls++; }
        public void Pending(PendingOrder order) => OnPurchasePending?.Invoke(order);
        public void Confirmed(Order order) => OnPurchaseConfirmed?.Invoke(order);
        public void Failed(FailedOrder order) => OnPurchaseFailed?.Invoke(order);
        public void Deferred(DeferredOrder order) => OnPurchaseDeferred?.Invoke(order);
        public void Disconnect() => OnStoreDisconnected?.Invoke(new StoreConnectionFailureDescription());
    }
}
