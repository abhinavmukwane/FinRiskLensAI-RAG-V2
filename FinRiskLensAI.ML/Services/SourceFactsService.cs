using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.ML.Features;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// Raw source payloads → <see cref="SourceFacts"/>. Two steps, both deterministic:
    /// the feature extractors parse each source, then the deep-dives derive the
    /// presentation-ready subsets. No scoring, no model — the fraud checks judge the
    /// facts, this only establishes them.
    /// <para>
    /// Extraction and deep-dive code is carried over unchanged from the V1 scorer; the
    /// golden test in FinRiskLensAI.Tests pins the output to what that scorer produced.
    /// </para>
    /// </summary>
    public class SourceFactsService : ISourceFactsService
    {
        public const string Version = "frl-facts-v1.0";

        private readonly UdyamFeatureExtractor _udyam;
        private readonly ItrFeatureExtractor _itr;
        private readonly GstFeatureExtractor _gst;
        private readonly AaFeatureExtractor _aa;

        public SourceFactsService(UdyamFeatureExtractor udyam, ItrFeatureExtractor itr,
            GstFeatureExtractor gst, AaFeatureExtractor aa)
        {
            _udyam = udyam; _itr = itr; _gst = gst; _aa = aa;
        }

        public SourceFacts Build(SourceFactsRequest request)
        {
            var features = new MsmeFeatureSet();
            if (!string.IsNullOrWhiteSpace(request.UdyamJson)) _udyam.Extract(request.UdyamJson!, features);
            if (!string.IsNullOrWhiteSpace(request.ItrJson)) _itr.Extract(request.ItrJson!, features);
            if (!string.IsNullOrWhiteSpace(request.AaJson)) _aa.Extract(request.AaJson!, features);
            _gst.Extract(request.GstTaxpayerJson, request.Gstr3bJsons,
                         request.Gstr1SummaryJsons, request.Gstr1B2bJsons,
                         request.Gstr1CdnrJsons, request.Gstr1HsnJsons,
                         request.Gstr2aB2bJsons, request.Gstr2bJsons, features);

            var facts = new SourceFacts { Features = features, Version = Version, ComputedAt = DateTime.UtcNow };
            BuildDeepDives(features, facts);
            return facts;
        }

        private static void BuildDeepDives(MsmeFeatureSet f, SourceFacts facts)
        {
            if (f.HasAa)
                facts.BankAnalysis = new BankStatementAnalysis
                {
                    AverageMonthlyBalance = f.AaAvgMonthlyBalance,
                    PeakBalance = f.AaPeakBalance,
                    AverageMonthlyCredit = Math.Round(f.AaAvgMonthlyCredit),
                    CashDepositsMonthly = f.AaCashDepositsMonthly,
                    SalaryCreditsMonthly = f.AaSalaryCreditsMonthly,
                    CustomerReceiptsMonthly = f.AaCustomerReceiptsMonthly,
                    SupplierPaymentsMonthly = f.AaSupplierPaymentsMonthly,
                    BounceCount = f.AaBounceCount,
                    ChequeReturnCount = f.AaChequeReturnCount,
                    EcsNachReturnCount = f.AaEcsNachReturnCount,
                    OdLimitTotal = f.AaOdLimitTotal,
                    OverdrawnTxnCount = f.AaOverdrawnTxnCount,
                    MinBalanceBreachCount = f.AaMinBalanceBreachCount,
                    MonthlyAvgBalance = new(f.AaMonthlyAvgBalance),
                    ModeSplitAmount = new(f.AaModeSplitAmount),
                    MonthlyCredits = new(f.AaMonthlyCredits),
                    MonthlyDebits = new(f.AaMonthlyDebits)
                };

            if (f.HasGst)
                facts.GstAnalysis = new GstDeepDive
                {
                    R1Vs3bConsistency = Math.Round(f.GstR1Vs3bConsistency, 4),
                    HasTaxPaymentData = f.GstHasTaxPaymentData,
                    CashTaxShare = Math.Round(f.GstCashTaxShare, 4),
                    ItcMonthlyAvg = Math.Round(f.GstItcMonthlyAvg),
                    TopCustomerShare = f.GstTopCustomerShare,
                    TopVendorShare = f.GstTopVendorShare,
                    TopCustomers = new(f.GstTopCustomers),
                    TopVendors = new(f.GstTopVendors),
                    MonthlySales = new(f.GstMonthlyTurnover),
                    Has2bData = f.GstHas2bData,
                    ItcAvailableMonthly = f.GstItcAvailableMonthly,
                    ItcClaimVsAvailable = f.GstItcClaimVsAvailable,
                    ItcUnavailableShare = f.GstItcUnavailableShare,
                    SupplierFilingRate = f.GstSupplierFilingRate
                };

            if (f.HasItr)
                facts.Financials = new FinancialRatios
                {
                    HasFinancials = f.ItrFinancialsYear != null,
                    SourceYear = f.ItrFinancialsYear,
                    BusinessTurnover = f.ItrBusinessTurnover,
                    EbitdaMargin = f.ItrEbitdaMargin,
                    PbtMargin = f.ItrPbtMargin,
                    NetProfitMargin = f.ItrNetProfitMargin,
                    DebtorDays = f.ItrDebtorDays,
                    AssetTurnover = f.ItrAssetTurnover,
                    TotalAssets = f.ItrTotalAssets,
                    NetWorth = f.ItrNetWorth,
                    DebtToEquity = f.ItrDebtToEquity,
                    FormType = f.ItrFormType,
                    FilingSection = f.ItrFilingSection,
                    IsPresumptive = f.ItrIsPresumptive,
                    EVerified = f.ItrEVerified,
                    ReturnProcessed = f.ItrReturnProcessed,
                    AuditApplicable = f.ItrAuditApplicable,
                    AuditCompleted = f.ItrAuditCompleted,
                    HasTaxDemand = f.ItrHasTaxDemand,
                    GstTurnoverVariancePct = f.ItrGstTurnoverVariancePct
                };

            if (f.HasUdyam)
                facts.Industry = new IndustryRiskInfo
                {
                    SectorName = f.SectorName,
                    Nic2Digit = f.SectorNic2,
                    RiskWeight = f.SectorRiskWeight,
                    Outlook = f.SectorRiskWeight >= 0.75 ? "Favourable"
                            : f.SectorRiskWeight >= 0.60 ? "Moderate" : "Cautious"
                };
        }
    }
}
