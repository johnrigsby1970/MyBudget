using System.ComponentModel.DataAnnotations;
using System.IO;

namespace StayOnTarget.DataAnnotation;

public class DatabaseNameValidationAttribute : ValidationAttribute {
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        if (value is not string dbName || string.IsNullOrWhiteSpace(dbName)) {
            return new ValidationResult("Database name is required.");
        }

        // 1. Enforce .db extension (case-insensitive)
        if (!dbName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) {
            return new ValidationResult("Database name must end with '.db'.");
        }

        // 2. Extract the file name without .db extension
        string nameWithoutExtension = dbName[..^3];

        if (string.IsNullOrWhiteSpace(nameWithoutExtension)) {
            return new ValidationResult("Database name cannot be just '.db'.");
        }

        // 3. Check for invalid Windows filename characters (< > : " / \ | ? *)
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (nameWithoutExtension.Any(ch => invalidChars.Contains(ch))) {
            return new ValidationResult("Database name contains invalid Windows filename characters.");
        }

        // 4. Windows filenames cannot end with a period or space before the extension
        if (nameWithoutExtension.EndsWith(" ") || nameWithoutExtension.EndsWith(".")) {
            return new ValidationResult("Database name cannot end with a space or period.");
        }

        // 5. Reserved Windows names check (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
        string[] reservedNames = {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4",
            "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2",
            "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        if (reservedNames.Contains(nameWithoutExtension.ToUpperInvariant())) {
            return new ValidationResult($"'{nameWithoutExtension}' is a reserved Windows system name.");
        }

        var test = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StayOnTarget",
            dbName
        );

        if (File.Exists(test)) {
            return new ValidationResult(
                $"Database found at {test}. Please enter a different name to create a new database.");
        }

        return ValidationResult.Success;
    }
}