using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.LoanCase;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// Loan Origination System — a credit appraisal case. This is the bank's own
    /// system, so the shape is ours: identity, what each verification source
    /// establishes, the cross-source income comparison, and provenance. No score and
    /// no decision — the sanction is the bank's. The findings block joins here once the
    /// fraud engine produces findings.
    /// </summary>
    public class LosPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.LOS;

        public object Build(BankCustomerRow c, SourceFacts facts, LoanCaseContext ctx)
        {
            var f = facts.Features;

            return new
            {
                schema = "finrisklens.los.case.v2",
                caseReference = ctx.CaseReference,
                submittedAt = ctx.Timestamp,
                submittedBy = new { userId = ctx.RequestedByUserId, name = ctx.RequestedByName, branchIfsc = ctx.BranchIfsc, branch = ctx.BranchName },
                source = new { system = "FinRiskLens", factsVersion = facts.Version, factsComputedAt = facts.ComputedAt },

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
                    sector = facts.Industry == null ? null : new { nic2Digit = facts.Industry.Nic2Digit, name = facts.Industry.SectorName, outlook = facts.Industry.Outlook },
                    city = c.City,
                    state = c.State
                },

                // Three independent turnover signals, like for like. ITR's leg is declared
                // turnover (revenue from operations), NOT gross total income — income is
                // after expenses, so comparing it to GST turnover measures the profit
                // margin rather than a mismatch. Null when the return keeps no books.
                crossSourceTurnover = new
                {
                    gstMonthly = Rupees(f.GstMonthlyAvgTurnover),
                    itrMonthly = f.ItrBusinessTurnover > 0 ? Rupees(f.ItrBusinessTurnover / 12) : (decimal?)null,
                    bankCreditsMonthly = Rupees(f.AaMonthlyAvgBankCredit)
                },

                bankConduct = facts.BankAnalysis == null ? null : new
                {
                    averageMonthlyBalance = Rupees(facts.BankAnalysis.AverageMonthlyBalance),
                    peakBalance = Rupees(facts.BankAnalysis.PeakBalance),
                    averageMonthlyCredit = Rupees(facts.BankAnalysis.AverageMonthlyCredit),
                    existingMonthlyEmi = Rupees(f.AaEmiMonthlyOutflow),
                    bounces = facts.BankAnalysis.BounceCount,
                    chequeReturns = facts.BankAnalysis.ChequeReturnCount,
                    ecsNachReturns = facts.BankAnalysis.EcsNachReturnCount,
                    overdrawnTransactions = facts.BankAnalysis.OverdrawnTxnCount,
                    minBalanceBreaches = facts.BankAnalysis.MinBalanceBreachCount
                },

                gstConduct = facts.GstAnalysis == null ? null : new
                {
                    annualisedTurnover = Rupees(f.GstAnnualisedTurnover),
                    gstr1Vs3bConsistency = facts.GstAnalysis.R1Vs3bConsistency,
                    topCustomerShare = facts.GstAnalysis.TopCustomerShare,
                    topVendorShare = facts.GstAnalysis.TopVendorShare,
                    supplierFilingRate = facts.GstAnalysis.SupplierFilingRate,
                    itcMonthlyAverage = Rupees(facts.GstAnalysis.ItcMonthlyAvg)
                },

                itr = facts.Financials == null || !facts.Financials.HasFinancials ? null : new
                {
                    formType = facts.Financials.FormType,
                    filingSection = facts.Financials.FilingSection,
                    assessmentYear = facts.Financials.SourceYear,
                    declaredTurnover = Rupees(facts.Financials.BusinessTurnover),
                    pbtMargin = facts.Financials.PbtMargin,
                    netProfitMargin = facts.Financials.NetProfitMargin,
                    netWorth = Rupees(facts.Financials.NetWorth),
                    debtToEquity = facts.Financials.DebtToEquity,
                    eVerified = facts.Financials.EVerified,
                    auditCompleted = facts.Financials.AuditCompleted,
                    taxDemandOutstanding = facts.Financials.HasTaxDemand,
                    gstTurnoverVariancePct = facts.Financials.GstTurnoverVariancePct
                },

                provenance = new
                {
                    dataSources = ctx.DataSources,
                    consent = "Udyam / GST / ITR / Account Aggregator data obtained under customer consent recorded on the FinRiskLens platform"
                }
            };
        }

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
