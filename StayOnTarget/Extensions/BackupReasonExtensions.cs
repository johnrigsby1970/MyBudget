namespace StayOnTarget.Extensions;

public static class BackupReasonExtensions {
    /// <summary>
    /// Converts the BackupReason enum into a normalized lower_snake_case string for filenames.
    /// </summary>
    public static string ToReasonTag(this BackupReason reason) => reason switch {
        BackupReason.Startup => "startup",
        BackupReason.Auto => "auto",
        BackupReason.PreDeleteAccount => "pre_delete_account",
        BackupReason.PreDeletePaycheck => "pre_delete_paycheck",
        BackupReason.PreDeleteBill => "pre_delete_bill",
        BackupReason.PreDeleteBucket => "pre_delete_bucket",
        BackupReason.PreDeleteCategory => "pre_delete_category",
        BackupReason.PreDeleteSubCategory => "pre_delete_subcategory",
        BackupReason.PreDeleteTransaction => "pre_delete_transaction",
        BackupReason.PreImport => "pre_import",
        BackupReason.PreReconciliation => "pre_reconciliation",
        _ => "auto"
    };
}