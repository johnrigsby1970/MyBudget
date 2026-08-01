using Windows.Security.Credentials;

namespace StayOnTarget.Helpers;

public static class VaultManager
{
    private const string VaultResourceName = "StayOnTarget_DB_Vault";

    /// <summary>
    /// Saves or updates the password for a specific database file.
    /// </summary>
    public static void SaveDatabaseKey(string dbFileName, string password)
    {
        var vault = new PasswordVault();

        // Pass dbFileName as the unique UserName key for this specific database
        var credential = new PasswordCredential(VaultResourceName, dbFileName, password);

        vault.Add(credential);
    }

    /// <summary>
    /// Retrieves the password for a specific database file.
    /// </summary>
    public static string? GetDatabaseKey(string dbFileName)
    {
        try
        {
            var vault = new PasswordVault();
            // Retrieve password by resource and database file name
            var credential = vault.Retrieve(VaultResourceName, dbFileName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            // Throws an exception if credential is not found in the vault
            return null;
        }
    }

    /// <summary>
    /// Removes the password from the vault when a database file is deleted.
    /// </summary>
    public static void RemoveDatabaseKey(string dbFileName)
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(VaultResourceName, dbFileName);
            vault.Remove(credential);
        }
        catch
        {
            // Credential didn't exist or was already removed
        }
    }
}