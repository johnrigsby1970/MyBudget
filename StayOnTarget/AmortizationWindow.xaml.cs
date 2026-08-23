using System.Windows;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;

namespace StayOnTarget
{
    public partial class AmortizationWindow : Window
    {
        public AmortizationWindow(Account account)
        {
            try
            {
                InitializeComponent();
                HeaderLabel.Text = $"Amortization for {account.Name}";
                CalculateSchedule(account);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error initializing AmortizationWindow for account {AccountName}.", account?.Name);
                
                MessageBox.Show($"Failed to load amortization schedule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateSchedule(Account account)
        {
            try
            {
                if (account.MortgageDetails == null) return;

                var schedule = new List<AmortizationItem>();
                decimal balance = account.Balance;
                decimal monthlyInterestRate = account.MortgageDetails.InterestRate / 100 / 12;
                decimal escrowInsurance = account.MortgageDetails.Escrow + account.MortgageDetails.MortgageInsurance;
                decimal totalPayment = account.MortgageDetails.LoanPayment;
                DateTime paymentDate = account.MortgageDetails.PaymentDate;

                int month = 1;
                balance = Math.Abs(balance);
                while (balance > 0 && month <= 600) // Limit to 50 years to prevent infinite loop
                {
                    decimal interest = balance * monthlyInterestRate;
                    decimal principal = totalPayment - interest - escrowInsurance;

                    if (principal <= 0 && balance > 0)
                    {
                        // Payment is too low to cover interest and escrow
                        break;
                    }

                    if (balance < principal)
                    {
                        principal = balance;
                    }

                    balance -= principal;

                    schedule.Add(new AmortizationItem
                    {
                        Month = month++,
                        Date = paymentDate,
                        Payment = totalPayment,
                        Interest = interest,
                        Principal = principal,
                        EscrowInsurance = escrowInsurance,
                        Balance = balance
                    });

                    paymentDate = paymentDate.AddMonths(1);
                }

                ScheduleGrid.ItemsSource = schedule;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating amortization schedule for account {AccountName}.", account?.Name);
                
                MessageBox.Show($"Error calculating amortization schedule: {ex.Message}", "Calculation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}