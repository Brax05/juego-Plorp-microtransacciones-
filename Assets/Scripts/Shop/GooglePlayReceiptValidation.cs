#if PLORP_IAP
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

/// <summary>Checks the signed Play payload as well as the product/token delivered by IAP.</summary>
public sealed class GooglePlayReceiptValidation
{
    readonly CrossPlatformValidator validator;
    readonly string packageName;

    public GooglePlayReceiptValidation(byte[] publicKey, string packageName)
    {
        if (publicKey == null || publicKey.Length == 0 || string.IsNullOrEmpty(packageName))
            throw new ArgumentException("Missing Google Play license key or package name.");
        this.packageName = packageName;
        validator = new CrossPlatformValidator(publicKey, packageName);
    }

    public string Validate(string receipt, string expectedProduct, string expectedToken)
    {
        if (string.IsNullOrEmpty(receipt) || receipt.Length > 65536 ||
            string.IsNullOrEmpty(expectedToken) || string.IsNullOrEmpty(expectedProduct))
            throw new CryptographicException("Missing purchase receipt, product or token.");

        var receipts = validator.Validate(receipt);
        if (receipts.Length != 1 || !(receipts[0] is GooglePlayReceipt google) ||
            google.packageName != packageName || google.productID != expectedProduct ||
            google.purchaseState != GooglePurchaseState.Purchased ||
            string.IsNullOrEmpty(google.purchaseToken) || google.purchaseToken != expectedToken)
            throw new CryptographicException("Receipt does not match the paid order.");

        // CrossPlatformValidator verifies the signature but does not check quantity,
        // and its legacy parser treats a missing purchaseState as zero (purchased).
        // Require those signed fields explicitly before accepting the receipt.
        var envelope = MiniJson.JsonDecode(receipt) as Dictionary<string, object>;
        var payload = MiniJson.JsonDecode((string)envelope["Payload"]) as Dictionary<string, object>;
        var signed = MiniJson.JsonDecode((string)payload["json"]) as Dictionary<string, object>;
        if (signed == null || !signed.TryGetValue("purchaseState", out var purchaseState) ||
            !(purchaseState is long state) || state != 0 ||
            (signed.TryGetValue("quantity", out var quantity) && (!(quantity is long units) || units != 1)))
            throw new CryptographicException("Unsupported signed state or quantity.");
        return google.purchaseToken;
    }
}
#endif
