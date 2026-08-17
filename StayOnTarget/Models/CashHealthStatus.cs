using System.ComponentModel.DataAnnotations;

namespace StayOnTarget.Models;

public enum CashHealthStatus {
    Optimal,
    [Display(Name = "Transfer Recommended")]
    TransferRecommended,
    [Display(Name = "Global Deficit")]
    GlobalDeficit
}