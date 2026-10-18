using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.ML.Features;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// Six-dimension credit health scoring engine (0-1000) per Doc/03_SCORING_ENGINE.md.
    /// Evaluates Revenue Vitality, Cash Flow Health, Transaction Trustworthiness,
    /// Compliance Quotient, Business Stability, and Debt Serviceability with
    /// missing-source weight redistribution and New-To-Credit neutral handling.
    /// </summary>
    public class RiskScoringService : IRiskScoringService
    {
        public const string Version = "frl-scoring-v2.0";

        private static readonly (string Name, double Weight, Func<MsmeFeatureSet, bool> HasData)[] DimensionDefs =
        {
            ("Revenue Vitality",             0.25, f => f.HasGst || f.HasItr),
            ("Cash Flow Health",             0.20, f => f.HasAa),
            ("Transaction Trustworthiness",  0.15, f => f.HasAa),
            ("Compliance Quotient",          0.15, f => f.HasGst || f.HasItr),
            ("Business Stability",           0.15, f => f.HasUdyam || f.HasEpfo),
            ("Debt Serviceability",          0.10, f => true)   // neutral default for NTC, never zeroed
        };

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

        public RiskAnalysisResult Analyze(SourceFacts facts)
        {
            return Analyze(facts.Features, facts);
        }

        public RiskAnalysisResult Analyze(MsmeFeatureSet features, SourceFacts? facts = null)
        {
            if (features.HasAa && features.AaMonthlyCredits.Count > 1 && features.AaCashflowTrendSlope == 0)
            {
                features.AaCashflowTrendSlope = TrendMath.NormalizedSlope(features.AaMonthlyCredits.Values.ToList());
            }

            // ── 1. Six sub-scores on 0..1
            var raw = new Dictionary<string, double>
            {
                ["Revenue Vitality"] = RevenueVitality(features),
                ["Cash Flow Health"] = CashFlowHealth(features),
                ["Transaction Trustworthiness"] = TransactionTrustworthiness(features),
                ["Compliance Quotient"] = ComplianceQuotient(features),
                ["Business Stability"] = BusinessStability(features),
                ["Debt Serviceability"] = DebtServiceability(features, out var debtUsedNeutral)
            };

            // ── 2. Missing-dimension weight redistribution
            var result = new RiskAnalysisResult { ComputedAt = DateTime.UtcNow, ModelVersion = Version };
            var availableWeight = DimensionDefs.Where(d => d.HasData(features)).Sum(d => d.Weight);
            if (availableWeight <= 0) availableWeight = 1.0;
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
            result.MlCalibratedScore = result.HeuristicScore;
            result.OverallScore = result.HeuristicScore;
            result.ScoreBand = Band(result.OverallScore);
            result.CashflowTrendSlope = Math.Round(features.AaCashflowTrendSlope, 3);

            // ── 3. Cross-source anomaly check
            result.Anomaly = CheckAnomaly(features);

            // ── 4. Explanations
            result.Explanations = BuildExplanations(features, debtUsedNeutral, result.ExcludedDimensions);

            // ── 5. Product recommendations
            result.Recommendations = Recommend(result.ScoreBand, features);

            // ── 6. Bank-decision ratios + indicative loan eligibility
            result.Lending = LendingCalculator.Compute(features, result.ScoreBand);

            // ── 7. Underwriting deep-dives
            if (facts != null)
            {
                result.BankAnalysis = facts.BankAnalysis;
                result.GstAnalysis = facts.GstAnalysis;
                result.Financials = facts.Financials;
                result.Industry = facts.Industry;
            }

            return result;
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

        private static double ItrCompliance(MsmeFeatureSet f)
        {
            var score = 0.50 * f.ItrFilingTimeliness             // filed by the due date
                      + 0.15 * (f.ItrEVerified ? 1 : 0)          // e-verified
                      + 0.15 * (f.ItrReturnProcessed ? 1 : 0)    // accepted and processed by CPC
                      + 0.20 * f.ItrTaxPaidRatio;                // liability actually discharged

            if (f.ItrAuditApplicable && !f.ItrAuditCompleted) score -= 0.15;
            if (f.ItrHasTaxDemand) score -= 0.10;
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
            var footprint = Math.Min(1, (f.PlantLocationCount + f.NicCodeCount + f.GstHsnProductCount / 5.0) / 8.0);

            var tradeCycle = f.GstPurchaseToSalesRatio > 0
                ? 1 - Math.Min(1, Math.Abs(f.GstPurchaseToSalesRatio - 0.75) / 0.75)
                : 0.5;

            return Math.Clamp(0.40 * vintage + 0.20 * sizeClass + 0.15 * footprint
                            + 0.15 * tradeCycle + 0.10 * f.SectorRiskWeight, 0, 1);
        }

        private static double DebtServiceability(MsmeFeatureSet f, out bool usedNeutral)
        {
            if (!f.HasBureau && !f.HasAa)
            {
                usedNeutral = true;
                return 0.5;   // NTC neutral midpoint
            }
            usedNeutral = !f.HasBureau;
            var fromAa = 1 - f.AaEmiToInflowRatio;
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

        // ─────────────────────────── anomaly check ───────────────────────────

        private static AnomalyCheckResult CheckAnomaly(MsmeFeatureSet f)
        {
            var res = new AnomalyCheckResult
            {
                GstMonthlyTurnover = f.GstMonthlyAvgTurnover,
                ItrMonthlyIncome = f.ItrBusinessTurnover / 12.0,
                BankMonthlyCredits = f.AaAvgMonthlyCredit
            };

            var signals = new List<double>();
            if (f.HasGst && res.GstMonthlyTurnover > 0 && f.HasAa && res.BankMonthlyCredits > 0)
            {
                var ratio = res.BankMonthlyCredits / res.GstMonthlyTurnover;
                if (ratio < 0.25 || ratio > 4.0) signals.Add(Math.Min(1.0, Math.Abs(Math.Log2(ratio)) / 3.0));
            }

            if (f.HasGst && res.GstMonthlyTurnover > 0 && f.HasItr && res.ItrMonthlyIncome > 0)
            {
                var ratio = res.ItrMonthlyIncome / res.GstMonthlyTurnover;
                if (ratio < 0.25 || ratio > 4.0) signals.Add(Math.Min(1.0, Math.Abs(Math.Log2(ratio)) / 3.0));
            }

            if (signals.Count > 0)
            {
                res.AnomalyScore = Math.Round(signals.Average(), 3);
                res.IsAnomalous = res.AnomalyScore >= 0.5;
                if (res.IsAnomalous)
                    res.Note = "Cross-source turnover variance exceeds tolerance between declared and banking amounts.";
            }

            return res;
        }

        // ─────────────────────────── explanations ───────────────────────────

        private static List<ScoreExplanationItem> BuildExplanations(
            MsmeFeatureSet f, bool debtUsedNeutral, List<string> excluded)
        {
            var explanations = new List<ScoreExplanationItem>();

            void Check(string key, bool isPositive, double weight = 0.8)
            {
                if (Templates.TryGetValue(key, out var t))
                {
                    explanations.Add(new ScoreExplanationItem
                    {
                        Dimension = t.Dimension,
                        ExplanationText = isPositive ? t.Positive : t.Negative,
                        Impact = isPositive ? ImpactDirection.Positive : ImpactDirection.Negative,
                        RelativeWeight = weight
                    });
                }
            }

            if (f.HasGst)
            {
                if (f.GstMonthlyTurnover.Count > 1)
                    Check("GstTurnoverTrend", f.GstTurnoverTrendSlope >= 0, 0.9);
                Check("GstFilingRegularity", f.GstFilingRegularity >= 0.9, 0.85);
                if (f.GstCreditNoteRatio > 0.05)
                    Check("CreditNoteRatio", false, 0.7);
            }

            if (f.HasAa)
            {
                Check("InflowVolatility", f.AaInflowVolatility <= 0.35, 0.8);
                Check("DaysCashOnHand", f.AaDaysCashOnHand >= 30, 0.75);
                Check("BounceRate", f.AaBounceCount == 0, 0.95);
                if (f.AaUpiTxnShare > 0.3)
                    Check("UpiShare", true, 0.6);
                if (f.AaRepeatPayerRatio > 0.4)
                    Check("RepeatPayerRatio", true, 0.65);
                Check("EmiToInflowRatio", f.AaEmiToInflowRatio <= 0.4, 0.75);
            }

            if (f.HasUdyam)
            {
                Check("BusinessVintage", f.BusinessVintageMonths >= 24, 0.7);
            }

            if (debtUsedNeutral)
            {
                explanations.Add(new ScoreExplanationItem
                {
                    Dimension = "Debt Serviceability",
                    ExplanationText = "No credit bureau record found (New-to-Credit) — this dimension used a neutral default rather than penalizing missing history",
                    Impact = ImpactDirection.Positive,
                    RelativeWeight = 0.5
                });
            }

            if (excluded.Count > 0)
            {
                explanations.Add(new ScoreExplanationItem
                {
                    Dimension = "Overall",
                    ExplanationText = $"Score computed without: {string.Join(", ", excluded)} — weights redistributed across available sources",
                    Impact = ImpactDirection.Negative,
                    RelativeWeight = 0.4
                });
            }

            return explanations.OrderByDescending(x => x.RelativeWeight).Take(8).ToList();
        }

        // ─────────────────────────── recommendations ───────────────────────────

        private static List<ProductRecommendationItem> Recommend(ScoreBandType band, MsmeFeatureSet features)
        {
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
                _ => new List<ProductRecommendationItem>()
            };
        }
    }
}
