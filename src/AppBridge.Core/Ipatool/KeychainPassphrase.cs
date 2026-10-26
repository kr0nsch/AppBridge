using System.Security.Cryptography;
using System.Text;

namespace AppBridge.Core.Ipatool;

/// <summary>
/// ipatool encrypts its saved session with a passphrase. AppBridge generates a random one per
/// Windows user and keeps it encrypted with DPAPI, so people never have to invent or type it.
/// </summary>
internal static class KeychainPassphrase
{
    private const string KeyFileName = "keychain.key";
    private static readonly byte[] Entropy = "AppBridge.ipatool.keychain.v1"u8.ToArray();

    public static string GetOrCreate(string ipatoolHome)
    {
        var keyFile = Path.Combine(ipatoolHome, KeyFileName);
        if (File.Exists(keyFile))
        {
            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(keyFile), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException ex)
            {
                // Typical for a portable copy moved to another PC or Windows account.
                AppLog.Warn("The saved Apple Account session belongs to another Windows user or PC; a new sign-in is required.", ex);
            }
        }

        // A session encrypted with an unknown passphrase can never be opened again.
        DeleteSavedAccount(ipatoolHome);

        var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(ipatoolHome);
        File.WriteAllBytes(keyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(passphrase), Entropy, DataProtectionScope.CurrentUser));
        return passphrase;
    }

    private static void DeleteSavedAccount(string ipatoolHome)
    {
        var account = Path.Combine(ipatoolHome, ".ipatool", "account");
        try
        {
            if (File.Exists(account))
                File.Delete(account);
        }
        catch (IOException ex)
        {
            AppLog.Warn("Could not remove the old ipatool session.", ex);
        }
    }
}
