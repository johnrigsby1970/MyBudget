namespace StayOnTarget.ViewModels;

public class AmortizationItem {
    public int Month { get; set; }
    public DateTime Date { get; set; }
    public decimal Payment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal EscrowInsurance { get; set; }
    public decimal Balance { get; set; }
}