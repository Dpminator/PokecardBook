using System;

namespace Pokebook
{
    public class PayCalculator
    {
        public static (double yearly, double monthly, double weekly) CalcualtePay(double yearlyWage, bool hecs)
        {
            var medicareTax = yearlyWage * 0.02;
            var incomeTax = 0.0;
            if (yearlyWage > 180000) incomeTax = 51667 + (0.45 * (yearlyWage - 180000));
            else if (yearlyWage > 120000) incomeTax = 29467 + (0.37 * (yearlyWage - 120000));
            else if (yearlyWage > 45000) incomeTax = 5092 + (0.325 * (yearlyWage - 45000));
            else if (yearlyWage > 18000) incomeTax = 0.19 * (yearlyWage - 18000);
            var yearlyHecs = 0.0;
            if (hecs) yearlyHecs = 0.075 * yearlyWage; // ToDo: fix this
            var yearlyTax = medicareTax + incomeTax + yearlyHecs;

            var monthlyPretaxIncome = Math.Ceiling(yearlyWage / 12);
            var weeeklyPretaxIncome = Math.Round(monthlyPretaxIncome / (13/3.0), 2);

            var monthyTax = yearlyTax / 12.0;
            var weeklyTax = yearlyTax / 52.0;
            
            var yearlyPay = Math.Round(yearlyWage - yearlyTax, 0);
            var monthlyPaycheck = Math.Round(monthlyPretaxIncome - monthyTax, 0);
            var roughWeeklyPay = Math.Round(weeeklyPretaxIncome - weeklyTax, 0);

            return (yearlyPay, monthlyPaycheck, roughWeeklyPay);
        }
    }
}
