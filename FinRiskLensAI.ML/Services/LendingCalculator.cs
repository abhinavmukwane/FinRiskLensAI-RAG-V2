using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.ML.Features;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// Computes the bank-decision view: standard credit-appraisal ratios (DSCR, FOIR,
    /// banking penetration, gross margin, liquidity) and indicative loan eligibility —
    /// working capital via the turnover (Nayak committee) method (WC requirement 25%
    /// of annual turnover; bank finances 20%, borrower margins 5%) and term capacity
    /// from EMI headroom annuitized over 5 years @ 11% p.a. Everything is indicative,
    /// scaled by the score band — not a sanctioned limit.
    /// </summary>
    public static class LendingCalculator
    {
        private const double AnnualRate = 0.11;
        private const int TenureMonths = 60;
        private const double FoirCap = 0.50;             // max share of inflows serviceable as EMI

        public static LendingAssessment Compute(MsmeFeatureSet f, ScoreBandType band)
        {
            var a = new LendingAssessment
            {
                AnnualTurnover = f.GstAnnualisedTurnover,
                MonthlySales = f.GstMonthlyAvgTurnover,
                MonthlyPurchases = f.GstMonthlyAvgPurchases,
                MonthlyBankInflow = f.AaAvgMonthlyCredit,
                MonthlyBankOutflow = f.AaMonthlyDebits.Count > 0 ? f.AaMonthlyDebits.Values.Average() : 0,
                ExistingMonthlyEmi = f.AaEmiMonthlyOutflow,
                BandAdjustmentFactor = band switch
                {
                    ScoreBandType.Excellent => 1.00,
                    ScoreBandType.Good => 0.85,
                    ScoreBandType.Fair => 0.60,
                    ScoreBandType.AtRisk => 0.35,
                    _ => 0
                }
            };
            a.MonthlySurplus = a.MonthlyBankInflow - a.MonthlyBankOutflow;

            // Fall back to bank inflows when GST is unavailable (thin-file case)
            if (a.AnnualTurnover <= 0 && f.HasAa)
                a.AnnualTurnover = a.MonthlyBankInflow * 12;

            // ── Eligibility: turnover method (working capital)
            a.WorkingCapitalLimit = Round10k(0.20 * a.AnnualTurnover * a.BandAdjustmentFactor);
            a.MarginMoneyRequired = Round10k(0.05 * a.AnnualTurnover);

            // ── Eligibility: EMI capacity → term loan (annuity PV)
            var foirHeadroom = Math.Max(0, FoirCap * a.MonthlyBankInflow - a.ExistingMonthlyEmi);
            var surplusHeadroom = Math.Max(0, a.MonthlySurplus * 0.8);
            a.AffordableMonthlyEmi = Math.Round(Math.Min(foirHeadroom, surplusHeadroom));

            var i = AnnualRate / 12;
            var annuityFactor = (1 - Math.Pow(1 + i, -TenureMonths)) / i;   // ≈ 45.99 for 60m @ 11%
            a.TermLoanCapacity = Round10k(a.AffordableMonthlyEmi * annuityFactor * a.BandAdjustmentFactor);

            a.TotalIndicativeEligibility = a.WorkingCapitalLimit + a.TermLoanCapacity;

            // ── Ratios with banking benchmarks
            var dscr = a.ExistingMonthlyEmi > 0
                ? Math.Min(10, (a.MonthlySurplus + a.ExistingMonthlyEmi) / a.ExistingMonthlyEmi)
                : (a.MonthlySurplus > 0 ? 10 : 0);
            var foir = f.AaEmiToInflowRatio;
            var penetration = a.MonthlySales > 0 && f.HasAa ? a.MonthlyBankInflow / a.MonthlySales : 0;
            var grossMargin = a.MonthlySales > 0 && a.MonthlyPurchases > 0
                ? (a.MonthlySales - a.MonthlyPurchases) / a.MonthlySales : 0;

            void Add(string name, double value, string display, string benchmark,
                     RatioStatus status, string description)
                => a.Ratios.Add(new LendingRatio
                {
                    Name = name, Value = Math.Round(value, 3), Display = display,
                    Benchmark = benchmark, Status = status, Description = description
                });

            Add("DSCR (Debt Service Coverage)", dscr,
                f.HasAa ? $"{dscr:0.00}x" : "N/A", "≥ 1.50x",
                !f.HasAa ? RatioStatus.NotAvailable : dscr >= 1.5 ? RatioStatus.Strong : dscr >= 1.25 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Cash available for debt service vs existing obligations — the primary repayment-capacity test");

            Add("FOIR (Obligations / Inflows)", foir,
                f.HasAa ? $"{foir:P0}" : "N/A", "≤ 40%",
                !f.HasAa ? RatioStatus.NotAvailable : foir <= 0.40 ? RatioStatus.Strong : foir <= 0.55 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Share of monthly inflows already committed to EMIs — headroom for a new loan");

            Add("Banking Penetration", penetration,
                penetration > 0 ? $"{penetration:P0}" : "N/A", "≥ 60%",
                penetration <= 0 ? RatioStatus.NotAvailable : penetration >= 0.60 ? RatioStatus.Strong : penetration >= 0.30 ? RatioStatus.Adequate : RatioStatus.Weak,
                "GST-declared sales actually routed through the bank account — cash-sales businesses score low");

            Add("Gross Margin (GST sales vs purchases)", grossMargin,
                grossMargin != 0 ? $"{grossMargin:P0}" : "N/A", "≥ 15%",
                a.MonthlyPurchases <= 0 ? RatioStatus.NotAvailable : grossMargin >= 0.15 ? RatioStatus.Strong : grossMargin >= 0.05 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Trading margin implied by GSTR-1 outward vs GSTR-2A inward values");

            Add("Days Cash on Hand", f.AaDaysCashOnHand,
                f.HasAa ? $"{Math.Min(365, f.AaDaysCashOnHand):0} days" : "N/A", "≥ 60 days",
                !f.HasAa ? RatioStatus.NotAvailable : f.AaDaysCashOnHand >= 60 ? RatioStatus.Strong : f.AaDaysCashOnHand >= 30 ? RatioStatus.Adequate : RatioStatus.Weak,
                "How long current balances cover average outflows if inflows stopped");

            Add("Inflow Volatility (CV)", f.AaInflowVolatility,
                f.HasAa ? $"{f.AaInflowVolatility:0.00}" : "N/A", "≤ 0.30",
                !f.HasAa ? RatioStatus.NotAvailable : f.AaInflowVolatility <= 0.30 ? RatioStatus.Strong : f.AaInflowVolatility <= 0.60 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Month-to-month steadiness of bank inflows — volatile inflows need conservative limits");

            Add("Credit Note Ratio", f.GstCreditNoteRatio,
                f.HasGst ? $"{f.GstCreditNoteRatio:P1}" : "N/A", "≤ 5%",
                !f.HasGst ? RatioStatus.NotAvailable : f.GstCreditNoteRatio <= 0.05 ? RatioStatus.Strong : f.GstCreditNoteRatio <= 0.15 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Share of invoiced revenue reversed via credit notes — high values inflate headline turnover");

            var hasConsistency = f.HasGst && f.GstR1Vs3bConsistency > 0;
            Add("GSTR-1 vs GSTR-3B Consistency", f.GstR1Vs3bConsistency,
                hasConsistency ? $"{f.GstR1Vs3bConsistency:P0}" : "N/A", "≥ 90%",
                !hasConsistency ? RatioStatus.NotAvailable : f.GstR1Vs3bConsistency >= 0.90 ? RatioStatus.Strong : f.GstR1Vs3bConsistency >= 0.75 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Agreement between invoice-level (GSTR-1) and summary (GSTR-3B) declared sales — persistent gaps signal misdeclaration");

            var hasItcMatch = f.GstHas2bData && f.GstItcClaimVsAvailable > 0;
            Add("ITC Claimed vs 2B Available", f.GstItcClaimVsAvailable,
                hasItcMatch ? $"{f.GstItcClaimVsAvailable:P0}" : "N/A", "≤ 100%",
                !hasItcMatch ? RatioStatus.NotAvailable : f.GstItcClaimVsAvailable <= 1.00 ? RatioStatus.Strong : f.GstItcClaimVsAvailable <= 1.10 ? RatioStatus.Adequate : RatioStatus.Weak,
                "ITC claimed in GSTR-3B vs auto-drafted availability in GSTR-2B — claiming beyond 2B is an over-claim red flag");

            Add("Supplier Filing Discipline (2B)", f.GstSupplierFilingRate,
                f.GstHas2bData ? $"{f.GstSupplierFilingRate:P0}" : "N/A", "≥ 90%",
                !f.GstHas2bData ? RatioStatus.NotAvailable : f.GstSupplierFilingRate >= 0.90 ? RatioStatus.Strong : f.GstSupplierFilingRate >= 0.75 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Share of B2B suppliers whose returns are filed per GSTR-2B — non-filing vendors put the MSME's ITC at risk");

            // ── Financial-statement ratios from ITR (business filers with books only)
            // PBT, not EBITDA: the ITR response reports no PBIDTA or depreciation line,
            // so an "EBITDA margin" here would be a relabelled PBT margin. Same threshold
            // band would flatter the borrower, so the bar is set for a post-interest figure.
            Add("Profit Before Tax Margin (ITR)", f.ItrPbtMargin ?? 0,
                f.ItrPbtMargin.HasValue ? $"{f.ItrPbtMargin:P1}" : "N/A", "≥ 8%",
                !f.ItrPbtMargin.HasValue ? RatioStatus.NotAvailable : f.ItrPbtMargin >= 0.08 ? RatioStatus.Strong : f.ItrPbtMargin >= 0.04 ? RatioStatus.Adequate : RatioStatus.Weak,
                $"Operating profitability from the ITR P&L{(f.ItrFinancialsYear != null ? $" (AY {f.ItrFinancialsYear})" : "")} — profit before tax over turnover");

            Add("Debt to Equity (ITR)", f.ItrDebtToEquity ?? 0,
                f.ItrDebtToEquity.HasValue ? $"{f.ItrDebtToEquity:0.00}x" : "N/A", "≤ 2.00x",
                !f.ItrDebtToEquity.HasValue ? RatioStatus.NotAvailable : f.ItrDebtToEquity <= 2.0 ? RatioStatus.Strong : f.ItrDebtToEquity <= 3.0 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Outside liabilities over partners'/members' capital from the ITR balance sheet — how much of the business is funded by borrowing");

            Add("Net Profit Margin (ITR)", f.ItrNetProfitMargin ?? 0,
                f.ItrNetProfitMargin.HasValue ? $"{f.ItrNetProfitMargin:P1}" : "N/A", "≥ 5%",
                !f.ItrNetProfitMargin.HasValue ? RatioStatus.NotAvailable : f.ItrNetProfitMargin >= 0.05 ? RatioStatus.Strong : f.ItrNetProfitMargin >= 0.02 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Bottom-line profitability after all costs and tax, from the ITR P&L");

            Add("Debtor Days (ITR)", f.ItrDebtorDays ?? 0,
                f.ItrDebtorDays.HasValue ? $"{f.ItrDebtorDays:0} days" : "N/A", "≤ 60 days",
                !f.ItrDebtorDays.HasValue ? RatioStatus.NotAvailable : f.ItrDebtorDays <= 60 ? RatioStatus.Strong : f.ItrDebtorDays <= 90 ? RatioStatus.Adequate : RatioStatus.Weak,
                "How long customers take to pay — sundry debtors over turnover × 365 (needs ITR balance sheet)");

            Add("Asset Turnover (ITR)", f.ItrAssetTurnover ?? 0,
                f.ItrAssetTurnover.HasValue ? $"{f.ItrAssetTurnover:0.00}x" : "N/A", "≥ 1.5x",
                !f.ItrAssetTurnover.HasValue ? RatioStatus.NotAvailable : f.ItrAssetTurnover >= 1.5 ? RatioStatus.Strong : f.ItrAssetTurnover >= 0.8 ? RatioStatus.Adequate : RatioStatus.Weak,
                "Revenue generated per rupee of assets — turnover over total assets (needs ITR balance sheet)");

            // ── Transparency notes
            a.Notes.Add("Working capital via turnover method: requirement 25% of annual turnover, bank finance 20%, borrower margin 5% (Nayak norms for MSME limits up to ₹5 Cr).");
            a.Notes.Add($"Term capacity assumes {TenureMonths / 12} year tenure @ {AnnualRate:P0} p.a., EMI capped at {FoirCap:P0} FOIR and 80% of observed monthly surplus.");
            a.Notes.Add($"All amounts scaled by score-band factor {a.BandAdjustmentFactor:0.00} ({band}); indicative only — not a sanction.");
            if (f.HasAa && f.HasGst && penetration < 0.30 && penetration > 0)
                a.Notes.Add("Banking penetration is very low relative to GST sales — verify whether the consented account is the business's primary operating account.");
            if (!f.HasAa)
                a.Notes.Add("No AA bank data — cashflow-based ratios unavailable; eligibility derives from GST turnover only.");
            if (!string.IsNullOrEmpty(f.SectorName))
                a.Notes.Add($"Industry: {f.SectorName} (NIC {f.SectorNic2 ?? "n/a"}) — sector risk weight {f.SectorRiskWeight:0.00} feeds Business Stability; use as a pricing hint, not a sanction condition.");

            return a;
        }

        private static double Round10k(double v) => Math.Round(v / 10_000) * 10_000;
    }
}
