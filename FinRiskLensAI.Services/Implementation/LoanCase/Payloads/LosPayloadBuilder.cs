using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// Loan Origination System — a credit appraisal case. This is the bank's own
    /// system, so the shape is ours: everything a credit officer's memorandum
    /// carries, in the order they read it. Identity, the decision headline, the six
    /// dimensions, the ratio table, deep-dive signals, and provenance.
    /// </summary>
    public class LosPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.LOS;

        public object Build(BankCustomerRow c, RiskAnalysisResult r, LoanCaseContext ctx)
        {
            var lending = r.Lending;
            var top = r.Recommendations.FirstOrDefault();

            return new
            {
                schema = "finrisklens.los.case.v1",
                caseReference = ctx.CaseReference,
                submittedAt = ctx.Timestamp,
                submittedBy = new { userId = ctx.RequestedByUserId, name = ctx.RequestedByName, branchIfsc = ctx.BranchIfsc, branch = ctx.BranchName },
                source = new { system = "FinRiskLensAI", modelVersion = r.ModelVersion, scoreComputedAt = r.ComputedAt },

                applicant = new
                {
                    udyamRegistrationNumber = c.Uan,
                    enterpriseName = c.EnterpriseName,
                    constitution = c.OrganizationType,
                    pan = c.PanNumber,
                    gstin = c.GstinNumber,
                    mobile = c.MobileNumber,
                    email = c.Email,
                    dateOfIncorporation = c.DateOfIncorporation?.ToString("yyyy-MM-dd"),
                    majorActivity = c.MajorActivity,
                    sector = r.Industry == null ? null : new { nic2Digit = r.Industry.Nic2Digit, name = r.Industry.SectorName, outlook = r.Industry.Outlook },
                    city = c.City,
                    state = c.State
                },

                decision = new
                {
                    financialHealthScore = Math.Round(r.OverallScore),
                    scoreOutOf = 1000,
                    band = r.ScoreBand.ToString(),
                    rulesEngineScore = Math.Round(r.HeuristicScore),
                    mlCalibratedScore = Math.Round(r.MlCalibratedScore),
                    recommendedProduct = top == null ? null : new
                    {
                        name = top.ProductName,
                        scheme = top.SchemeCode,
                        indicativeAmountMin = top.IndicativeAmountMin,
                        indicativeAmountMax = top.IndicativeAmountMax,
                        reason = top.RecommendationReason
                    },
                    dataIntegrityFlag = r.Anomaly.IsAnomalous,
                    dataIntegrityNote = r.Anomaly.Note
                },

                eligibility = lending == null ? null : new
                {
                    workingCapitalLimit = Rupees(lending.WorkingCapitalLimit),
                    termLoanCapacity = Rupees(lending.TermLoanCapacity),
                    totalIndicative = Rupees(lending.TotalIndicativeEligibility),
                    marginMoneyRequired = Rupees(lending.MarginMoneyRequired),
                    affordableMonthlyEmi = Rupees(lending.AffordableMonthlyEmi),
                    bandAdjustmentFactor = lending.BandAdjustmentFactor,
                    basis = "Working capital via turnover (Nayak) method; term loan via EMI-headroom annuity, 5y @ 11%"
                },

                financials = lending == null ? null : new
                {
                    annualTurnover = Rupees(lending.AnnualTurnover),
                    monthlySales = Rupees(lending.MonthlySales),
                    monthlyPurchases = Rupees(lending.MonthlyPurchases),
                    monthlyBankInflow = Rupees(lending.MonthlyBankInflow),
                    monthlyBankOutflow = Rupees(lending.MonthlyBankOutflow),
                    monthlySurplus = Rupees(lending.MonthlySurplus),
                    existingMonthlyEmi = Rupees(lending.ExistingMonthlyEmi),
                    cashflowTrendSlope = r.CashflowTrendSlope
                },

                dimensions = r.Dimensions.Select(d => new
                {
                    name = d.Dimension,
                    score = Math.Round(d.Score, 1),
                    maxPoints = d.MaxPoints,
                    weight = d.EffectiveWeight,
                    dataAvailable = d.DataAvailable,
                    neutralDefaultUsed = d.UsedNeutralDefault
                }),

                appraisalRatios = (lending?.Ratios ?? new List<LendingRatio>()).Select(x => new
                {
                    name = x.Name,
                    value = x.Value,
                    display = x.Display,
                    benchmark = x.Benchmark,
                    status = x.Status.ToString()
                }),

                strengths = r.Explanations.Where(e => e.Impact == ImpactDirection.Positive).Select(e => e.ExplanationText),
                risks = r.Explanations.Where(e => e.Impact == ImpactDirection.Negative).Select(e => e.ExplanationText),

                bankConduct = r.BankAnalysis == null ? null : new
                {
                    averageMonthlyBalance = Rupees(r.BankAnalysis.AverageMonthlyBalance),
                    peakBalance = Rupees(r.BankAnalysis.PeakBalance),
                    bounces = r.BankAnalysis.BounceCount,
                    chequeReturns = r.BankAnalysis.ChequeReturnCount,
                    ecsNachReturns = r.BankAnalysis.EcsNachReturnCount,
                    overdrawnTransactions = r.BankAnalysis.OverdrawnTxnCount,
                    minBalanceBreaches = r.BankAnalysis.MinBalanceBreachCount
                },

                gstConduct = r.GstAnalysis == null ? null : new
                {
                    gstr1Vs3bConsistency = r.GstAnalysis.R1Vs3bConsistency,
                    topCustomerShare = r.GstAnalysis.TopCustomerShare,
                    topVendorShare = r.GstAnalysis.TopVendorShare,
                    supplierFilingRate = r.GstAnalysis.SupplierFilingRate,
                    itcMonthlyAverage = Rupees(r.GstAnalysis.ItcMonthlyAvg)
                },

                itr = r.Financials == null || !r.Financials.HasFinancials ? null : new
                {
                    formType = r.Financials.FormType,
                    filingSection = r.Financials.FilingSection,
                    assessmentYear = r.Financials.SourceYear,
                    declaredTurnover = Rupees(r.Financials.BusinessTurnover),
                    pbtMargin = r.Financials.PbtMargin,
                    netProfitMargin = r.Financials.NetProfitMargin,
                    netWorth = Rupees(r.Financials.NetWorth),
                    debtToEquity = r.Financials.DebtToEquity,
                    eVerified = r.Financials.EVerified,
                    auditCompleted = r.Financials.AuditCompleted,
                    taxDemandOutstanding = r.Financials.HasTaxDemand,
                    gstTurnoverVariancePct = r.Financials.GstTurnoverVariancePct
                },

                provenance = new
                {
                    dataSources = ctx.DataSources,
                    dimensionsExcluded = r.ExcludedDimensions,
                    consent = "Udyam / GST / ITR / Account Aggregator data obtained under customer consent recorded on the FinRiskLensAI platform"
                }
            };
        }

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
