using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// A write-ahead record couples the reward balances with a hashed purchase token.
/// It repairs an interrupted delivery before the game loads its saved balances.
/// The checksum detects damaged files; it is not protection against a modified client.
/// </summary>
public static class PurchaseDeliveryJournal
{
    const string MarkerPrefix = "GlorpFarm.iap.delivered.";
    static string JournalPath => Path.Combine(Application.persistentDataPath, "iap-delivery-v1.journal");
    public static bool Faulted { get; private set; }

    [Serializable]
    public sealed class Delivery
    {
        public int version = 1;
        public string tokenHash;
        public string productId;
        public int gems;
        public long flowerCoins;
        public double incomeUntil;
        public int starter;
        public int count;
    }

    public static bool WasDelivered(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        // Migrate deliveries from the old integration without granting them again.
        return PlayerPrefs.GetInt(MarkerPrefix + Hash(token), 0) == 1 ||
            PlayerPrefs.GetInt("GlorpFarm.iap.tx." + token, 0) == 1;
    }

    public static void Recover()
    {
        Faulted = false;
        try
        {
            if (!File.Exists(JournalPath)) return;
            if (new FileInfo(JournalPath).Length > 16384) throw new InvalidDataException("Purchase delivery journal is too large.");
            string stored = File.ReadAllText(JournalPath, Encoding.UTF8);
            int split = stored.IndexOf('\n');
            if (split != 64 || Hash(stored.Substring(split + 1)) != stored.Substring(0, split))
                throw new InvalidDataException("Purchase delivery journal is damaged.");
            var delivery = JsonUtility.FromJson<Delivery>(stored.Substring(split + 1));
            Check(delivery);
            // If the save completed but removing the journal failed, do not overwrite
            // balances the player has subsequently earned or spent.
            if (PlayerPrefs.GetInt(MarkerPrefix + delivery.tokenHash, 0) != 1)
                Apply(delivery);
            PlayerPrefs.Save();
            File.Delete(JournalPath);
        }
        catch
        {
            Faulted = true;
            Debug.LogError("No se pudo recuperar una entrega de compra. Se bloquearon las compras para proteger el guardado.");
        }
    }

    public static void Commit(Delivery delivery)
    {
        if (Faulted || File.Exists(JournalPath))
            throw new IOException("A purchase delivery needs recovery before continuing.");
        Check(delivery);
        string json = JsonUtility.ToJson(delivery);
        byte[] bytes = Encoding.UTF8.GetBytes(Hash(json) + "\n" + json);
        string temporary = JournalPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, JournalPath);
            Apply(delivery);
            PlayerPrefs.Save();
            // A leftover committed journal is harmless: Recover sees the marker.
            try { File.Delete(JournalPath); }
            catch (IOException) { Faulted = true; }
            catch (UnauthorizedAccessException) { Faulted = true; }
        }
        catch
        {
            Faulted = true;
            throw;
        }
    }

    public static Delivery Prepare(GemPack pack, string token, int gems, long flowerCoins, double incomeUntil, bool starter)
    {
        bool grant = !WasDelivered(token) && !(pack.OncePerPlayer && starter);
        double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        return new Delivery
        {
            tokenHash = Hash(token), productId = pack.ProductId,
            gems = checked(gems + (grant ? pack.Gems : 0)),
            flowerCoins = checked(flowerCoins + (grant ? pack.BonusFlowerCoins : 0)),
            incomeUntil = grant && pack.BoostMinutes > 0 ? Math.Max(now, incomeUntil) + pack.BoostMinutes * 60 : incomeUntil,
            starter = starter || (grant && pack.OncePerPlayer) ? 1 : 0,
            count = checked(PlayerPrefs.GetInt("GlorpFarm.iap.count", 0) + (grant ? 1 : 0))
        };
    }

    static void Check(Delivery delivery)
    {
        if (delivery == null || delivery.version != 1 || string.IsNullOrEmpty(delivery.tokenHash) ||
            delivery.tokenHash.Length != 64 || ShopCatalog.Pack(delivery.productId) == null ||
            delivery.gems < 0 || delivery.flowerCoins < 0 || delivery.count < 0 ||
            (delivery.starter != 0 && delivery.starter != 1) || delivery.incomeUntil < 0 ||
            double.IsNaN(delivery.incomeUntil) || double.IsInfinity(delivery.incomeUntil))
            throw new InvalidDataException("Invalid purchase delivery.");
        foreach (char digit in delivery.tokenHash)
            if (!(digit >= '0' && digit <= '9') && !(digit >= 'a' && digit <= 'f'))
                throw new InvalidDataException("Invalid token digest.");
    }

    static void Apply(Delivery delivery)
    {
        PlayerPrefs.SetInt("GlorpFarm.gems", delivery.gems);
        PlayerPrefs.SetString("GlorpFarm.garden.flowerCoins", delivery.flowerCoins.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString("GlorpFarm.boost.0", delivery.incomeUntil.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt("GlorpFarm.iap.starter", delivery.starter);
        PlayerPrefs.SetInt("GlorpFarm.iap.count", delivery.count);
        PlayerPrefs.SetInt(MarkerPrefix + delivery.tokenHash, 1);
    }

    public static string Hash(string value)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }
}
