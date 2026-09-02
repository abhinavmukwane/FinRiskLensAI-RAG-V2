using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.ML.Features;
using FinRiskLensAI.ML.MachineLearning;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// Orchestrates the full analysis: feature extraction from raw payloads → six-dimension
    /// scoring (with missing-source weight redistribution) → LightGBM calibration blend →
    /// anomaly check → explanations → product recommendations. Per Doc/03_SCORING_ENGINE.md.
    /// </summary>
    public class RiskScoringService : IRiskScoringService
    {
        public const string Version = "frl-scoring-v1.0";

        private static readonly (string Name, double Weight, Func<MsmeFeatureSet, bool> HasData)[] DimensionDefs =
        {
            ("Revenue Vitality",             0.25, f => f.HasGst || f.HasItr),
            ("Cash Flow Health",             0.20, f => f.HasAa),
            ("Transaction Trustworthiness",  0.15, f => f.HasAa),
            ("Compliance Quotient",          0.15, f => f.HasGst || f.HasItr),
            ("Business Stability",           0.15, f => f.HasUdyam || f.HasEpfo),
            ("Debt Serviceability",          0.10, f => true)   // neutral default for NTC, never zeroed
        };

        private readonly UdyamFeatureExtractor _udyam;
        private readonly ItrFeatureExtractor _itr;
        private readonly GstFeatureExtractor _gst;
        private readonly AaFeatureExtractor _aa;
        private readonly CashflowTrendAnalyzer _trend;
        private readonly IncomeAnomalyDetector _anomaly;
        private readonly ScoreCalibrationModel _calibration;

        public RiskScoringService(
            UdyamFeatureExtractor udyam, ItrFeatureExtractor itr,
            GstFeatureExtractor gst, AaFeatureExtractor aa,
            CashflowTrendAnalyzer trend, IncomeAnomalyDetector anomaly,
            ScoreCalibrationModel calibration)
        {
            _udyam = udyam; _itr = itr; _gst = gst; _aa = aa;
            _trend = trend; _anomaly = anomaly; _calibration = calibration;
        }

        public RiskAnalysisResult Analyze(RiskAnalysisRequest request)
        {
            // ── 1. Feature engineering
            var features = new MsmeFeatureSet();
            if (!string.IsNullOrWhiteSpace(request.UdyamJson)) _udyam.Extract(request.UdyamJson!, features);
            if (!string.IsNullOrWhiteSpace(request.ItrJson)) _itr.Extract(request.ItrJson!, features);
            if (!string.IsNullOrWhiteSpace(request.AaJson)) _aa.Extract(request.AaJson!, features);
            _gst.Extract(request.GstTaxpayerJson, request.Gstr3bJsons,
                         request.Gstr1SummaryJsons, request.Gstr1B2bJsons,
                         request.Gstr1CdnrJsons, request.Gstr1HsnJsons,
                         request.Gstr2aB2bJsons, request.Gstr2bJsons, features);

            features.AaCashflowTrendSlope = _trend.ComputeTrend(features.AaMonthlyCredits.Values.ToList());

            // ── 2. Six sub-scores on 0..1
            var raw = new Dictionary<string, double>
            {
                ["Revenue Vitality"] = RevenueVitality(features),
                ["Cash Flow Health"] = CashFlowHealth(features),
                ["Transaction Trustworthiness"] = TransactionTrustworthiness(features),
                ["Compliance Quotient"] = ComplianceQuotient(features),
                ["Business Stability"] = BusinessStability(features),
                ["Debt Serviceability"] = DebtServiceability(features, out var debtUsedNeutral)
            };

            // ── 3. Missing-dimension weight redistribution (never zero a missing source)
            var result = new RiskAnalysisResult { ComputedAt = DateTime.UtcNow, ModelVersion = Version };
            var availableWeight = DimensionDefs.Where(d => d.HasData(features)).Sum(d => d.Weight);
            double heuristic = 0;

            foreach (var (name, weight, hasData) in DimensionDefs)
            {
                var available = hasData(features);
                var effectiveWeight = available ? weight / availableWeight : 0;
                if (!available) result.ExcludedDimensions.Add(name);

                heuristic += effectiveWeight * raw[name];
                result.Dimensions.Add(new DimensionScore
                {
                    Dimension = name,
                    MaxPoints = Math.Round(weight * 1000),
                    Score = available ? Math.Round(raw[name] * weight * 1000, 1) : 0,
                    EffectiveWeight = Math.Round(effectiveWeight, 4),
                    DataAvailable = available,
                    UsedNeutralDefault = name == "Debt Serviceability" && debtUsedNeutral
                });
            }
            result.HeuristicScore = Math.Round(heuristic * 1000, 1);

            // ── 4. LightGBM calibration blend
            var vector = ScoreFeatureVector.FromFeatureSet(features);
            result.MlCalibratedScore = Math.Round(_calibration.PredictScore(vector), 1);
            result.OverallScore = Math.Round(0.6 * result.HeuristicScore + 0.4 * result.MlCalibratedScore);
            result.ScoreBand = Band(result.OverallScore);
            result.CashflowTrendSlope = Math.Round(features.AaCashflowTrendSlope, 3);

            // ── 5. Cross-source anomaly check (data-integrity signal, separate from the score)
            result.Anomaly = _anomaly.Check(features);

            // ── 6. Explanations: PFI-driven + rule templates
            result.Explanations = BuildExplanations(features, vector, debtUsedNeutral, result.ExcludedDimensions);

            // ── 7. Product recommendations by band
            result.Recommendations = Recommend(result.ScoreBand, features, result);

            // ── 8. Bank-decision ratios + indicative loan eligibility
            result.Lending = LendingCalculator.Compute(features, result.ScoreBand);

            // ── 9. Underwriting deep-dives for the Financial Health Card
            BuildDeepDives(features, result);
            return result;
        }

        private static void BuildDeepDives(MsmeFeatureSet f, RiskAnalysisResult result)
        {
            if (f.HasAa)
                result.BankAnalysis = new BankStatementAnalysis
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
                result.GstAnalysis = new GstDeepDive
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
                result.Financials = new FinancialRatios
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
                result.Industry = new IndustryRiskInfo
                {
                    SectorName = f.SectorName,
                    Nic2Digit = f.SectorNic2,
                    RiskWeight = f.SectorRiskWeight,
                    Outlook = f.SectorRiskWeight >= 0.75 ? "Favourable"
                            : f.SectorRiskWeight >= 0.60 ? "Moderate" : "Cautious"
                };
        }

        // ─────────────────────────── dimension calculators (0..1) ───────────────────────────

        private static double RevenueVitality(MsmeFeatureSet f)
        {
            if (!f.HasGst && !f.HasItr) return 0;
            double score = 0.5, weightUsed = 0;

            if (f.HasGst && f.GstMonthlyTurnover.Count > 0)
            {
                var gst = 0.5 + 0.5 * f.GstTurnoverTrendSlope;                       // growth
                gst = 0.7 * gst + 0.3 * (1 - Math.Min(1,
                    TrendMath.CoefficientOfVariation(f.GstMonthlyTurnover.Values.ToList())));  // consistency
                score = gst; weightUsed = 0.7;
            }
            if (f.HasItr && f.ItrYearlyIncome.Count > 0)
            {
                var itr = 0.5 + 0.5 * f.ItrIncomeTrendSlope;
                score = weightUsed > 0 ? weightUsed * score + (1 - weightUsed) * itr : itr;
            }
            // Heavy credit/debit-note reversals (CDNR) undercut headline turnover
            score -= 0.3 * f.GstCreditNoteRatio;
            return Math.Clamp(score, 0, 1);
        }

        private static double CashFlowHealth(MsmeFeatureSet f)
        {
            if (!f.HasAa) return 0;
            var cushion = Math.Min(1, f.AaDaysCashOnHand / 90.0);                    // 3 months cash = full marks
            var steadiness = 1 - Math.Min(1, f.AaInflowVolatility);
            var bouncePenalty = Math.Min(0.4, f.AaBounceCount * 0.1);
            var trendBonus = 0.5 + 0.5 * f.AaCashflowTrendSlope;
            return Math.Clamp(0.35 * cushion + 0.30 * steadiness + 0.20 * trendBonus - bouncePenalty + 0.15, 0, 1);
        }

        private static double TransactionTrustworthiness(MsmeFeatureSet f)
        {
            if (!f.HasAa || f.AaTransactionCount == 0) return 0;
            var velocity = Math.Min(1, f.AaTransactionCount / (double)Math.Max(1, f.AaMonthsCovered) / 30.0);
            var diversity = Math.Min(1, f.AaUpiCounterpartyCount / 10.0);
            var repeat = f.AaRepeatPayerRatio;
            var upiAdoption = f.AaUpiTxnShare;
            return Math.Clamp(0.25 * velocity + 0.30 * diversity + 0.25 * repeat + 0.20 * upiAdoption, 0, 1);
        }

        private static double ComplianceQuotient(MsmeFeatureSet f)
        {
            if (!f.HasGst && !f.HasItr) return 0;
            double total = 0, weight = 0;
            if (f.HasGst)
            {
                total += 0.55 * f.GstFilingRegularity + 0.10 * (f.GstRegistrationActive ? 1 : 0);
                weight += 0.65;
            }
            if (f.HasItr)
            {
                total += 0.35 * ItrCompliance(f);
                weight += 0.35;
            }
            return Math.Clamp(weight > 0 ? total / weight : 0, 0, 1);
        }

        /// <summary>
        /// ITR filing quality, 0..1. The vendor response covers a single assessment
        /// year, so this reads how well <i>that</i> return was handled rather than
        /// counting years filed — a count that can only ever be 1 and used to cost a
        /// fully compliant filer two-thirds of the sub-score.
        /// </summary>
        private static double ItrCompliance(MsmeFeatureSet f)
        {
            var score = 0.50 * f.ItrFilingTimeliness             // filed by the due date
                      + 0.15 * (f.ItrEVerified ? 1 : 0)          // e-verified — an unverified return is invalid
                      + 0.15 * (f.ItrReturnProcessed ? 1 : 0)    // accepted and processed by CPC
                      + 0.20 * f.ItrTaxPaidRatio;                // liability actually discharged

            // A required tax audit that wasn't filed is a compliance failure in itself.
            if (f.ItrAuditApplicable && !f.ItrAuditCompleted) score -= 0.15;

            // An open demand means the self-assessment fell short.
            if (f.ItrHasTaxDemand) score -= 0.10;

            // Advance-tax interest is a cash-planning signal, not evasion — a light touch.
            if (f.ItrAdvanceTaxInterest > 0) score -= 0.05;

            return Math.Clamp(score, 0, 1);
        }

        private static double BusinessStability(MsmeFeatureSet f)
        {
            if (!f.HasUdyam && !f.HasEpfo) return 0;
            var vintage = Math.Min(1, f.BusinessVintageMonths / 120.0);              // 10 years = full marks
            var sizeClass = f.EnterpriseType.ToLowerInvariant() switch
            {
                "medium" => 1.0, "small" => 0.75, "micro" => 0.5, _ => 0.5
            };
            // Footprint: locations + activity codes + product/service mix (HSN)
            var footprint = Math.Min(1, (f.PlantLocationCount + f.NicCodeCount + f.GstHsnProductCount / 5.0) / 8.0);

            // Purchase-to-sales in a business-normal band (≈0.4–1.1) signals a real
            // operating trade cycle; only applies when GSTR-2A data exists
            var tradeCycle = f.GstPurchaseToSalesRatio > 0
                ? 1 - Math.Min(1, Math.Abs(f.GstPurchaseToSalesRatio - 0.75) / 0.75)
                : 0.5;

            // EPFO headcount trend joins here when the source arrives
            return Math.Clamp(0.40 * vintage + 0.20 * sizeClass + 0.15 * footprint
                            + 0.15 * tradeCycle + 0.10 * f.SectorRiskWeight, 0, 1);
        }

        private static double DebtServiceability(MsmeFeatureSet f, out bool usedNeutral)
        {
            if (!f.HasBureau && !f.HasAa)
            {
                usedNeutral = true;
                return 0.5;   // NTC neutral midpoint — absence of credit history is not bad credit
            }
            usedNeutral = !f.HasBureau;
            var fromAa = 1 - f.AaEmiToInflowRatio;                                   // low obligations = high score
            // Without bureau data, blend the AA read with the neutral midpoint
            return Math.Clamp(f.HasBureau ? fromAa : 0.6 * fromAa + 0.4 * 0.5, 0, 1);
        }

        private static ScoreBandType Band(double score) => score switch
        {
            >= 800 => ScoreBandType.Excellent,
            >= 650 => ScoreBandType.Good,
            >= 500 => ScoreBandType.Fair,
            >= 350 => ScoreBandType.AtRisk,
            _ => ScoreBandType.HighRisk
        };

        // ─────────────────────────── explanations ───────────────────────────

        private static readonly Dictionary<string, (string Dimension, string Positive, string Negative)> Templates = new()
        {
            ["GstTurnoverTrend"] = ("Revenue Vitality", "GST turnover is growing month over month", "GST turnover is declining month over month"),
            ["ItrIncomeTrend"] = ("Revenue Vitality", "ITR-declared income is rising across assessment years", "ITR-declared income is falling across assessment years"),
            ["GstFilingRegularity"] = ("Compliance Quotient", "GST returns filed for every expected period", "Gaps found in GST return filing history"),
            ["ItrFilingTimeliness"] = ("Compliance Quotient", "ITR consistently filed before the due date", "One or more ITR filings were made after the due date"),
            ["InflowVolatility"] = ("Cash Flow Health", "Monthly bank inflows are steady and predictable", "Monthly bank inflows are highly volatile"),
            ["DaysCashOnHand"] = ("Cash Flow Health", "Healthy cash cushion relative to monthly outflows", "Thin cash cushion relative to monthly outflows"),
            ["BounceRate"] = ("Cash Flow Health", "No payment bounces detected in the statement window", "Payment bounces detected in bank statements"),
            ["UpiShare"] = ("Transaction Trustworthiness", "Strong digital (UPI) payment adoption", "Low digital payment adoption"),
            ["RepeatPayerRatio"] = ("Transaction Trustworthiness", "Recurring customers keep paying — sticky counterparty base", "Few repeat payers — customer base may be transient"),
            ["GstB2bShare"] = ("Business Stability", "Meaningful B2B trade share indicates established buyers", "Sales are almost entirely B2C/unregistered"),
            ["BusinessVintage"] = ("Business Stability", "Established business vintage per Udyam registration", "Young business with limited operating history"),
            ["EmiToInflowRatio"] = ("Debt Serviceability", "Existing EMI obligations are small relative to inflows", "Existing EMI obligations consume a large share of inflows"),
            ["CreditNoteRatio"] = ("Revenue Vitality", "Minimal credit-note reversals — invoiced revenue holds up", "Significant share of invoiced revenue reversed via credit notes"),
            ["PurchaseCoverage"] = ("Business Stability", "Purchases (GSTR-2A) sit in a healthy band relative to sales — real trading cycle", "Purchases are out of proportion to declared sales"),
            ["HsnDiversity"] = ("Business Stability", "Diversified product/service mix across HSN codes", "Revenue concentrated in very few product lines")
        };

        private List<ScoreExplanationItem> BuildExplanations(
            MsmeFeatureSet features, ScoreFeatureVector vector,
            bool debtUsedNeutral, List<string> excluded)
        {
            var explanations = new List<ScoreExplanationItem>();

            // PFI: top movers for this specific MSME, translated via the template dictionary
            var impacts = _calibration.ExplainPrediction(vector)
                .OrderByDescending(i => Math.Abs(i.Impact))
                .Take(8)
                .ToList();
            var maxImpact = Math.Max(1e-6, impacts.Max(i => Math.Abs(i.Impact)));

            foreach (var (feature, impact) in impacts)
            {
                if (!Templates.TryGetValue(feature, out var t) || Math.Abs(impact) < 1) continue;
                explanations.Add(new ScoreExplanationItem
                {
                    Dimension = t.Dimension,
                    ExplanationText = impact >= 0 ? t.Positive : t.Negative,
                    Impact = impact >= 0 ? ImpactDirection.Positive : ImpactDirection.Negative,
                    RelativeWeight = Math.Round(Math.Abs(impact) / maxImpact, 3)
                });
            }

            // Mandatory transparency notes from the scoring doc
            if (debtUsedNeutral)
                explanations.Add(new ScoreExplanationItem
                {
                    Dimension = "Debt Serviceability",
                    ExplanationText = "No credit bureau record found (New-to-Credit) — this dimension used a neutral default rather than penalizing missing history",
                    Impact = ImpactDirection.Positive,
                    RelativeWeight = 0.1
                });

            if (excluded.Count > 0)
                explanations.Add(new ScoreExplanationItem
                {
                    Dimension = "Overall",
                    ExplanationText = $"Score computed without: {string.Join(", ", excluded)} — weights redistributed across available sources",
                    Impact = ImpactDirection.Negative,
                    RelativeWeight = 0.1
                });

            return explanations;
        }

        // ─────────────────────────── recommendations ───────────────────────────

        private static List<ProductRecommendationItem> Recommend(
            ScoreBandType band, MsmeFeatureSet features, RiskAnalysisResult result)
        {
            // Indicative ticket anchored to observed scale (annualized turnover or bank inflows)
            var scale = (decimal)Math.Max(features.GstAnnualisedTurnover, features.AaAvgMonthlyCredit * 12);
            if (scale <= 0) scale = 500_000m;

            ProductRecommendationItem Make(string name, string scheme, decimal minFactor, decimal maxFactor, string reason) => new()
            {
                ProductName = name,
                SchemeCode = scheme,
                IndicativeAmountMin = Math.Round(scale * minFactor / 10_000m) * 10_000m,
                IndicativeAmountMax = Math.Round(scale * maxFactor / 10_000m) * 10_000m,
                RecommendationReason = reason
            };

            return band switch
            {
                ScoreBandType.Excellent or ScoreBandType.Good => new List<ProductRecommendationItem>
                {
                    Make("Standard Working Capital Loan", "Standard", 0.15m, 0.30m,
                        $"{band} financial health score supports a standard working capital facility"),
                    Make("Business Term Loan", "Standard", 0.20m, 0.40m,
                        "Consistent revenue and compliance history support term lending")
                },
                ScoreBandType.Fair => new List<ProductRecommendationItem>
                {
                    Make("CGTMSE-backed Working Capital", "CGTMSE", 0.10m, 0.20m,
                        "Moderate risk profile — credit guarantee cover reduces lender exposure")
                },
                ScoreBandType.AtRisk => new List<ProductRecommendationItem>
                {
                    Make("MUDRA Loan (Shishu/Kishor)", "MUDRA", 0.02m, 0.10m,
                        "Entry-tier MUDRA product recommended while financial health improves")
                },
                _ => new List<ProductRecommendationItem>()   // High Risk: surface weak dimensions instead
            };
        }
    }
}
