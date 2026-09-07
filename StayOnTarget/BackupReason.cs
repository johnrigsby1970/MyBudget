namespace StayOnTarget;

public enum BackupReason {
    Startup,
    Auto,
    PreDeleteAccount,
    PreDeletePaycheck,
    PreDeleteBill,
    PreDeleteBucket,
    PreDeleteCategory,
    PreDeleteSubCategory,
    PreDeleteTransaction,
    PreImport,
    PreReconciliation
}