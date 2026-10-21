using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

static class Program
{
    const string Package = "com.glorpfarm.app";
    const string Product = "plorp.gems.20";
    const string Token = "unique-play-purchase-token";
    static RSA signingKey;
    static int passed;
    static void Main()
    {
        signingKey = RSA.Create(2048);
        GooglePlayLicenseKey.Key = signingKey.ExportSubjectPublicKeyInfo();
        Application.persistentDataPath = Path.Combine(Path.GetTempPath(), "plorp-iap-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Application.persistentDataPath);
        try
        {
            Receipts();
            Journal();
            Flow();
            Console.WriteLine($"PASS: {passed} local purchase checks.");
        }
        finally { signingKey.Dispose(); Directory.Delete(Application.persistentDataPath, true); }
    }

    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS " + name);
    }
    static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch { rejected = true; }
        Check(rejected, name);
    }
    static string Receipt(string product = Product, string token = Token, string package = Package,
        object state = null, bool omitState = false, object quantity = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["productId"] = product, ["purchaseToken"] = token, ["packageName"] = package,
            ["orderId"] = "GPA.test", ["purchaseTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (!omitState) payload["purchaseState"] = state ?? 0L;
        if (quantity != null) payload["quantity"] = quantity;
        string json = MiniJson.JsonEncode(payload);
        string signature = Convert.ToBase64String(signingKey.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));
        return MiniJson.JsonEncode(new Dictionary<string, object>
        {
            ["Store"] = "GooglePlay",
            ["Payload"] = MiniJson.JsonEncode(new Dictionary<string, object> { ["json"] = json, ["signature"] = signature })
        });
    }
    static void Receipts()
    {
        GooglePlayPublicKeyFormat.Validate(GooglePlayLicenseKey.Key);
        Check(true, "setup accepts a valid RSA SubjectPublicKeyInfo key");
        Reject(() => GooglePlayPublicKeyFormat.Validate(signingKey.ExportPkcs8PrivateKey()), "setup rejects a private key");
        Reject(() => GooglePlayPublicKeyFormat.Validate(new byte[294]), "setup rejects invalid public key data");
        using (var weak = RSA.Create(1024))
            Reject(() => GooglePlayPublicKeyFormat.Validate(weak.ExportSubjectPublicKeyInfo()), "setup rejects weak public key");
        var validator = new GooglePlayReceiptValidation(GooglePlayLicenseKey.Key, Package);
        string valid = Receipt();
        Check(validator.Validate(valid, Product, Token) == Token, "real RSA-signed receipt accepted");
        Reject(() => validator.Validate(valid.Replace("GPA.test", "GPA.changed"), Product, Token), "tampered signature rejected");
        Reject(() => validator.Validate(valid, "plorp.gems.560", Token), "wrong catalog product rejected");
        Reject(() => validator.Validate(valid, Product, "different-token"), "wrong IAP token rejected");
        Reject(() => validator.Validate(Receipt(package: "another.app"), Product, Token), "another app rejected");
        Reject(() => validator.Validate(Receipt(package: ""), Product, Token), "missing package rejected");
        Reject(() => validator.Validate(Receipt(state: 1L), Product, Token), "cancelled receipt rejected");
        Reject(() => validator.Validate(Receipt(state: 2L), Product, Token), "refunded receipt rejected");
        Reject(() => validator.Validate(Receipt(state: 4L), Product, Token), "deferred receipt rejected");
        Reject(() => validator.Validate(Receipt(omitState: true), Product, Token), "missing signed state rejected");
        Reject(() => validator.Validate(Receipt(state: "0"), Product, Token), "non-numeric signed state rejected");
        Reject(() => validator.Validate(Receipt(quantity: 2L), Product, Token), "unsupported multiple units rejected");
        Check(validator.Validate(Receipt(quantity: 1L), Product, Token) == Token, "one signed unit accepted");
        Reject(() => validator.Validate(valid.Replace("GooglePlay", "FakeStore"), Product, Token), "fake store rejected");
        Reject(() => validator.Validate("{}", Product, Token), "empty envelope rejected");
        Reject(() => validator.Validate(valid, Product, ""), "missing purchase token rejected");
        using (var another = RSA.Create(2048))
        {
            var wrong = new GooglePlayReceiptValidation(another.ExportSubjectPublicKeyInfo(), Package);
            Reject(() => wrong.Validate(valid, Product, Token), "wrong public key rejected");
        }
        Reject(() => new GooglePlayReceiptValidation(null, Package), "missing key fails closed");
        Reject(() => new GooglePlayReceiptValidation(new byte[] { 1, 2, 3 }, Package), "invalid key fails closed");
    }
    static string JournalPath => Path.Combine(Application.persistentDataPath, "iap-delivery-v1.journal");
    static void ResetJournal()
    {
        foreach (var file in Directory.GetFiles(Application.persistentDataPath)) File.Delete(file);
        PlayerPrefs.Clear();
        PurchaseDeliveryJournal.Recover();
    }
    static void StoreJournal(PurchaseDeliveryJournal.Delivery delivery)
    {
        string json = JsonUtility.ToJson(delivery);
        File.WriteAllText(JournalPath, PurchaseDeliveryJournal.Hash(json) + "\n" + json);
    }
    static void Journal()
    {
        ResetJournal();
        var pack = ShopCatalog.Pack(Product);
        var delivery = PurchaseDeliveryJournal.Prepare(pack, Token, 13, 3, 0, false);
        PurchaseDeliveryJournal.Commit(delivery);
        PlayerPrefs.Crash();
        Check(PlayerPrefs.GetInt("GlorpFarm.gems") == 33 && PurchaseDeliveryJournal.WasDelivered(Token), "reward and token survive restart together");
        Check(PlayerPrefs.GetInt("GlorpFarm.iap.count") == 1 && !File.Exists(JournalPath), "successful delivery commits once and cleans journal");
        var duplicate = PurchaseDeliveryJournal.Prepare(pack, Token, 33, 3, 0, false);
        Check(duplicate.gems == 33 && duplicate.count == 1, "duplicate token adds no gems or count");
        ResetJournal();
        var starter = ShopCatalog.Pack("plorp.starter.150");
        var start = PurchaseDeliveryJournal.Prepare(starter, Token, 7, 3, 0, false);
        PurchaseDeliveryJournal.Commit(start);
        Check(PlayerPrefs.GetInt("GlorpFarm.gems") == 157 && PlayerPrefs.GetString("GlorpFarm.garden.flowerCoins") == "13" &&
            PlayerPrefs.GetInt("GlorpFarm.iap.starter") == 1 && start.incomeUntil > 0, "starter grants all benefits in one journal");
        var startAgain = PurchaseDeliveryJournal.Prepare(starter, "another-token", 157, 13, start.incomeUntil, true);
        Check(startAgain.gems == 157 && startAgain.incomeUntil == start.incomeUntil && startAgain.count == 1, "starter cannot grant twice under a new token");
        ResetJournal();
        StoreJournal(delivery);
        PurchaseDeliveryJournal.Recover();
        PlayerPrefs.Crash();
        Check(PlayerPrefs.GetInt("GlorpFarm.gems") == 33 && PurchaseDeliveryJournal.WasDelivered(Token), "crash after journal preparation recovers balances and token");
        PlayerPrefs.SetInt("GlorpFarm.gems", 8); PlayerPrefs.Save();
        StoreJournal(delivery); // Simulate failed cleanup after an already saved delivery.
        PurchaseDeliveryJournal.Recover();
        Check(PlayerPrefs.GetInt("GlorpFarm.gems") == 8, "leftover journal never restores already spent gems");
        ResetJournal();
        PlayerPrefs.FailSave = true;
        Reject(() => PurchaseDeliveryJournal.Commit(delivery), "failed durable save does not report success");
        Check(PurchaseDeliveryJournal.Faulted && File.Exists(JournalPath), "failed save retains recovery record and blocks purchases");
        PlayerPrefs.Crash();
        PurchaseDeliveryJournal.Recover();
        Check(!PurchaseDeliveryJournal.Faulted && PlayerPrefs.GetInt("GlorpFarm.gems") == 33, "failed save repaired after a hard restart");
        ResetJournal();
        File.WriteAllText(JournalPath, "damaged");
        PurchaseDeliveryJournal.Recover();
        Check(PurchaseDeliveryJournal.Faulted && !PurchaseDeliveryJournal.WasDelivered(Token) && File.Exists(JournalPath), "corrupt journal fails closed and preserves evidence");
        ResetJournal();
        PlayerPrefs.SetInt("GlorpFarm.iap.tx." + Token, 1);
        Check(PurchaseDeliveryJournal.WasDelivered(Token), "old integration deliveries are not regranted");
        Reject(() => PurchaseDeliveryJournal.Prepare(pack, "fresh-token", int.MaxValue, 0, 0, false), "balance overflow rejected");
        ResetJournal();
    }
    static PendingOrder Pending(string product = Product, string token = Token, string receipt = null)
    {
        return new PendingOrder
        {
            CartOrdered = new Cart { Entries = new List<CartItem> { new CartItem { Product = new Product { definition = new ProductDefinition(product, ProductType.Consumable) } } } },
            Info = new OrderInfo { Receipt = receipt ?? Receipt(product, token), Google = new GoogleInfo { PurchaseToken = token } }
        };
    }
    static void Flow()
    {
        var game = new FarmGameState();
        var store = StoreController.Instance;
        store.FailProducts = true;
        PurchaseService.Initialize(game, _ => { });
        Check(!PurchaseService.Ready && PurchaseService.PriceOf(ShopCatalog.Pack(Product)) == "No disponible", "failed product fetch never displays fallback as a real price");
        store.FailProducts = false;
        PurchaseService.Restore();
        Check(PurchaseService.Ready && PurchaseService.CanBuy(ShopCatalog.Pack(Product)), "manual retry recovers failed product loading");
        PurchaseService.Buy(ShopCatalog.Pack(Product));
        PurchaseService.Buy(ShopCatalog.Pack(Product));
        Check(store.BuyCalls == 1 && PurchaseService.Busy, "double tap launches only one purchase");
        store.BeforeConfirm = () => Check(game.Delivered == 1, "delivery finishes before Google confirmation");
        var order = Pending();
        store.Pending(order);
        store.Pending(order);
        Check(game.Delivered == 1 && store.ConfirmCalls == 1 && PurchaseService.Busy, "repeated pending callback does not double deliver or confirm");
        store.BeforeConfirm = null;
        store.Confirmed(new FailedOrder { Info = order.Info, CartOrdered = order.CartOrdered });
        Check(!PurchaseService.CanBuy(ShopCatalog.Pack(Product)), "unconfirmed pack stays unavailable for another checkout");
        store.Pending(order);
        Check(game.Delivered == 1 && store.ConfirmCalls == 2, "failed confirmation can retry without another reward");
        store.Confirmed(order);
        Check(!PurchaseService.Busy && PurchaseService.CanBuy(ShopCatalog.Pack(Product)), "confirmed consumable can be purchased again");
        int confirms = store.ConfirmCalls;
        store.Pending(Pending(token: "invalid-token", receipt: "{}"));
        Check(game.Delivered == 1 && store.ConfirmCalls == confirms, "invalid receipt grants nothing and is not consumed");
        game.ThrowOnDelivery = true;
        store.Pending(Pending(token: "storage-failure-token"));
        Check(store.ConfirmCalls == confirms, "failed save never confirms purchase");
        game.ThrowOnDelivery = false;
        // Refresh an empty store history to clear the failed synthetic test order.
        PurchaseService.Restore();
        PurchaseService.Buy(ShopCatalog.Pack(Product));
        store.Failed(new FailedOrder { CartOrdered = order.CartOrdered, FailureReason = PurchaseFailureReason.UserCancelled });
        Check(!PurchaseService.Busy && game.Delivered == 1, "cancel releases checkout without a reward");
        store.Deferred(new DeferredOrder { CartOrdered = order.CartOrdered, Info = order.Info });
        Check(!PurchaseService.CanBuy(ShopCatalog.Pack(Product)) && game.Delivered == 1, "deferred payment delivers nothing and blocks its pack");
        store.Disconnect();
        Check(!PurchaseService.Ready, "disconnect disables checkout");
        PurchaseService.Restore();
        Check(PurchaseService.Ready, "checkout recovers after reconnect");
        var starter = Pending("plorp.starter.150", "starter-restore-token");
        store.History.ConfirmedOrders.Add(new ConfirmedOrder { CartOrdered = starter.CartOrdered, Info = starter.Info });
        PurchaseService.Restore();
        PurchaseService.Restore();
        Check(game.Delivered == 2 && game.StarterPackBought, "signed starter restoration is delivered only once");
    }
}
