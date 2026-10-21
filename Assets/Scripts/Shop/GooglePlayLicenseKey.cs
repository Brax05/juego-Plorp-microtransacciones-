// Generated public Google Play license key. Do not replace with an upload signing key.
public static class GooglePlayLicenseKey
{
    public static readonly bool IsConfigured = true;
    public static byte[] Data()
    {
#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.Purchasing.Security.Obfuscator.DeObfuscate(
            System.Convert.FromBase64String("2N/zvTdOrYYidcP5Kzm5ZzbTyINFerwyjztv5lSXqMo9wTcUUZ+8DD6MDyw+AwgHJIhGiPkDDw8PCw4NLp+KzyLeQ4UyuiSUJ8E5DPgkR2pn7O99a4XzJDDBtdTlQfrwhKUvekTqBVMqp9sujAkJe2ffHfZtpUBWqH28pIqxDOp9F9nnngL/Q5bgKWBCYHMcHsbzdv1A1JI4DhJaI/+5RQu0ivdajRL13WMfl8hTBkstog9YLfCGbF8w0iP55W1M98YU/QhxxFeSlk1r2Oo1oYmiVKali5V2M5oDSQHdIqo3zNo8l/XdwiVOgtCu2YTaHHyZN3MkwRvaHX+AoxX4cGWBIEqMDwEOPowPBAyMDw8OtUxCz9IeModxEhIeLhNnoQwNDw4P"),
            new int[] { 8, 3, 8, 12, 9, 9, 7, 10, 13, 9, 11, 11, 13, 13, 14 }, 14);
#else
        return null;
#endif
    }
}
