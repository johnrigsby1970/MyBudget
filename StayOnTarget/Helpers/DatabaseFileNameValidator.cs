using System;
using System.IO;
using System.Linq;

namespace StayOnTarget.Helpers;

public static class DatabaseFileNameValidator
{
    // Characters that break Windows OS OR SQLite URI parsing
    private static readonly char[] InvalidDbNameChars = 
        Path.GetInvalidFileNameChars()
            .Concat(new[] { '#', '?', '%', ';' })
            .Distinct()
            .ToArray();

    public static bool IsValidFileName(string fileName, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            errorMessage = "File name cannot be empty.";
            return false;
        }

        // Trim whitespace for checking
        string trimmedStr = fileName.Trim();

        if (trimmedStr.Length > 128)
        {
            errorMessage = "File name must be 128 characters or fewer.";
            return false;
        }

        // Check for illegal OS / URI characters
        if (trimmedStr.IndexOfAny(InvalidDbNameChars) >= 0)
        {
            errorMessage = "File name contains invalid characters (avoid special symbols like #, ?, %, <, >, :, \", /, \\, |, *).";
            return false;
        }

        // Check Windows reserved names (CON, NUL, AUX, etc.)
        string nameWithoutExt = Path.GetFileNameWithoutExtension(trimmedStr).ToUpperInvariant();
        string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        
        if (reservedNames.Contains(nameWithoutExt))
        {
            errorMessage = $"'{nameWithoutExt}' is a reserved system name.";
            return false;
        }

        if (trimmedStr.EndsWith(".") || trimmedStr.EndsWith(" "))
        {
            errorMessage = "File name cannot end with a dot or space.";
            return false;
        }

        return true;
    }
}