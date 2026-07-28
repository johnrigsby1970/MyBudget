using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;
using StayOnTarget.Models;

namespace StayOnTarget.Converters;

public class AccountTypeToIconConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is AccountType type) {
            return type switch {
                AccountType.Auto => PackIconFontAwesomeKind.CarSolid, // 
                AccountType.Cash => PackIconFontAwesomeKind.DollarSignSolid, // 
                AccountType.CollegeFund => PackIconFontAwesomeKind.GraduationCapSolid, // 
                AccountType.Checking => PackIconFontAwesomeKind.MoneyCheckDollarSolid, // Bank building
                AccountType.Savings => PackIconFontAwesomeKind.SackDollarSolid, // Piggy bank
                AccountType.Investment => PackIconFontAwesomeKind.MoneyBillTrendUpSolid, // Growth chart
                AccountType.CD => PackIconFontAwesomeKind.CertificateSolid, // Financial certificate
                AccountType.Retirement401k => PackIconFontAwesomeKind.HourglassHalfSolid, // Long term time accumulation
                AccountType.Brokerage => PackIconFontAwesomeKind.BriefcaseSolid, // Portfolio briefcase
                AccountType.Mortgage => PackIconFontAwesomeKind.FileInvoiceDollarSolid, // Legal property agreement
                AccountType.PersonalLoan => PackIconFontAwesomeKind.HandHoldingDollarSolid, // Loan/funds received
                AccountType.CreditCard => PackIconFontAwesomeKind.CreditCardRegular, // Physical credit card
                AccountType.RealEstate => PackIconFontAwesomeKind.HouseChimneySolid, // Residential house
                AccountType.AppreciatingAsset => PackIconFontAwesomeKind.GemRegular, // High value store of wealth
                _ => PackIconFontAwesomeKind.CircleQuestionSolid
            };
        }

        return PackIconIoniconsKind.HelpMD;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
