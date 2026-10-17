namespace FinRiskLensAI.Common
{
    /// <summary>
    /// The GST page's single sample dataset (no live GST API yet). The GST view
    /// (_GstAnalysisBody) and the assistant's assessment context both read it, so
    /// the chatbot can never quote a figure the page doesn't show. The same for
    /// every business. Amounts are ₹ lakh; the period is Jan–Jun of one financial year.
    /// </summary>
    public static class GstSampleData
    {
        public static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
        public static readonly double[] Sales = { 84, 90, 105, 112, 122, 130 };          // outward supplies (GSTR-3B)
        public static readonly double[] Purchases = { 62, 65, 78, 80, 85, 92 };          // inward supplies (GSTR-2B)
        public static readonly double[] Tax = { 4.2, 4.5, 5.2, 5.6, 6.1, 6.5 };          // tax liability (GSTR-3B), paid in full
        public const double PriorSales = 543, PriorPurchases = 412.5;                    // same months of the previous year
        public const double CashShare = 0.29;                                            // tax paid directly in cash
        public const double Rcm = 1.2;                                                   // reverse charge, paid in cash
        public const double ItcAvailable = 23.5;                                         // ITC available in GSTR-2B
        public const int ReturnsDue = 12, ReturnsFiled = 12, LateReturns = 0;            // GSTR-1 + GSTR-3B, 6 months
        public const int B2bCustomers = 126, ActiveSuppliers = 82;
        public const double R1Vs3bMatch = 0.992;
        public const double OutlookGrowth = 0.16;                                        // illustrative: June sales to next June
        public const int Score = 94;                                                     // illustrative GST score (no formula behind it)
        public static readonly (string Name, double Share)[] Suppliers =
        {
            ("Supplier A", 0.28), ("Supplier B", 0.18), ("Supplier C", 0.13), ("Supplier D", 0.06), ("Supplier E", 0.04)
        };
        public static readonly string[] OutlookMonths = { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

        // ── Derived (the page shows these) ──
        public static double SalesTotal => Sales.Sum();
        public static double PurchaseTotal => Purchases.Sum();
        public static double Annualised => SalesTotal / Sales.Length * 12;
        public static double SalesGrowth => SalesTotal / PriorSales - 1;
        public static double PurchaseGrowth => PurchaseTotal / PriorPurchases - 1;
        public static int MonthsUp => Sales.Zip(Sales.Skip(1), (a, b) => b > a).Count(up => up);
        public static double TaxTotal => Tax.Sum();
        public static double TaxCash => Math.Round(TaxTotal * CashShare, 1);
        public static double TaxItc => Math.Round(TaxTotal - TaxCash, 1);               // ITC used = ITC claimed
        public static double ItcUtil => TaxItc / ItcAvailable;
        public static double ItcUnclaimed => Math.Round(ItcAvailable - TaxItc, 1);
        public static double Filing => (double)ReturnsFiled / ReturnsDue;
        public static double Top5 => Suppliers.Sum(s => s.Share);
        public static double[] Outlook =>
            Enumerable.Range(1, 12).Select(k => Math.Round(Sales[^1] * (1 + OutlookGrowth * k / 12), 1)).ToArray();
        public static double[] OutlookUpper => Outlook.Select(v => Math.Round(v * 1.06, 1)).ToArray();
    }
}
