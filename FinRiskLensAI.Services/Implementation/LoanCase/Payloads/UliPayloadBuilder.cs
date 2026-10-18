using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Facts;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// RBI Unified Lending Interface — OCEN lineage. ULI's model is that the
    /// <i>lender</i> decides; a service provider supplies evidence. So what the
    /// verification sources establish travels as a derived data packet alongside the
    /// source packets and consent artefacts, and there is no "decision" block.
    /// </summary>
    public class UliPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.ULI;

        public object Build(BankCustomerRow c, SourceFacts facts, LoanCaseContext ctx)
        {
            var f = facts.Features;

            var sourcePackets = ctx.DataSources.Select(s => new
            {
                packetType = MapSource(s),
                provider = ProviderFor(s),
                obtainedVia = s.StartsWith("aa", StringComparison.OrdinalIgnoreCase) ? "ACCOUNT_AGGREGATOR" : "CONSENTED_API",
                reference = s
            });

            return new
            {
                specVersion = "ULI/OCEN-4.0",
                messageType = "loanApplicationRequest",
                transactionId = ctx.TransactionId,
                timestamp = ctx.Timestamp,

                lender = new { id = ctx.LenderId, name = ctx.LenderName, branchIfsc = ctx.BranchIfsc },
                serviceProvider = new { id = "FINRISKLENS", role = "DERIVED_DATA_PROVIDER", factsVersion = facts.Version },

                loanApplication = new
                {
                    applicationId = ctx.CaseReference,
                    productType = "WORKING_CAPITAL",
                    currency = "INR",
                    purpose = "MSME_BUSINESS"
                },

                borrower = new
                {
                    type = "MSME",
                    udyamRegistrationNumber = c.Uan,
                    legalName = c.EnterpriseName,
                    constitution = c.OrganizationType,
                    identifiers = new { pan = c.PanNumber, gstin = c.GstinNumber, mobile = c.MobileNumber, email = c.Email },
                    address = new { city = c.City, state = c.State, country = "IN" },
                    dateOfIncorporation = c.DateOfIncorporation?.ToString("yyyy-MM-dd"),
                    industry = facts.Industry == null ? null : new { nicCode = facts.Industry.Nic2Digit, description = facts.Industry.SectorName }
                },

                consentArtefacts = ctx.DataSources.Select(s => new
                {
                    dataType = MapSource(s),
                    consentMode = s.StartsWith("aa", StringComparison.OrdinalIgnoreCase) ? "AA_CONSENT" : "PLATFORM_CONSENT",
                    purposeCode = "101",   // ReBIT purpose: loan underwriting
                    status = "ACTIVE"
                }),

                dataPackets = new
                {
                    source = sourcePackets,
                    derived = new[]
                    {
                        new
                        {
                            packetType = "VERIFIED_FACTS",
                            provider = "FINRISKLENS",
                            computedAt = facts.ComputedAt,
                            content = new
                            {
                                crossSourceTurnover = new
                                {
                                    gstMonthly = Rupees(f.GstMonthlyAvgTurnover),
                                    itrMonthly = f.ItrBusinessTurnover > 0 ? Rupees(f.ItrBusinessTurnover / 12) : (decimal?)null,
                                    bankCreditsMonthly = Rupees(f.AaMonthlyAvgBankCredit)
                                },
                                bankConduct = facts.BankAnalysis == null ? null : new
                                {
                                    averageMonthlyBalance = Rupees(facts.BankAnalysis.AverageMonthlyBalance),
                                    existingMonthlyEmi = Rupees(f.AaEmiMonthlyOutflow),
                                    bounces = facts.BankAnalysis.BounceCount,
                                    ecsNachReturns = facts.BankAnalysis.EcsNachReturnCount
                                },
                                gstConduct = facts.GstAnalysis == null ? null : new
                                {
                                    annualisedTurnover = Rupees(f.GstAnnualisedTurnover),
                                    gstr1Vs3bConsistency = facts.GstAnalysis.R1Vs3bConsistency,
                                    supplierFilingRate = facts.GstAnalysis.SupplierFilingRate
                                },
                                itr = facts.Financials == null || !facts.Financials.HasFinancials ? null : new
                                {
                                    assessmentYear = facts.Financials.SourceYear,
                                    declaredTurnover = Rupees(facts.Financials.BusinessTurnover),
                                    netWorth = Rupees(facts.Financials.NetWorth),
                                    taxDemandOutstanding = facts.Financials.HasTaxDemand
                                },
                                riskAssessment = ctx.RiskAssessment == null ? null : new
                                {
                                    verdict = ctx.RiskAssessment.Verdict.ToString(),
                                    assessedAt = ctx.RiskAssessment.AssessedAt,
                                    reviewedFindings = ctx.RiskAssessment.ReviewedFindings.Select(f => new
                                    {
                                        type = f.Type,
                                        severity = f.Severity.ToString(),
                                        reviewStatus = f.ReviewStatus.ToString()
                                    })
                                },
                                creditDecision = ctx.CreditDecision == null ? null : new
                                {
                                    decision = ctx.CreditDecision.Decision,
                                    product = ctx.CreditDecision.Product,
                                    amount = ctx.CreditDecision.Amount,
                                    tenure = ctx.CreditDecision.TenureMonths,
                                    emi = ctx.CreditDecision.Emi,
                                    foir = ctx.CreditDecision.Foir,
                                    reasons = ctx.CreditDecision.Reasons,
                                    conditions = ctx.CreditDecision.Conditions,
                                    model = ctx.CreditDecision.Model,
                                    acceptedBy = ctx.CreditDecision.AcceptedBy
                                }
                            }
                        }
                    }
                },

                requestedBy = new { userId = ctx.RequestedByUserId, name = ctx.RequestedByName }
            };
        }

        private static string MapSource(string file)
        {
            var f = file.ToLowerInvariant();
            if (f.StartsWith("udyam")) return "UDYAM_REGISTRATION";
            if (f.StartsWith("gst")) return "GST_RETURNS";
            if (f.StartsWith("itr")) return "INCOME_TAX_RETURN";
            if (f.StartsWith("aa")) return "BANK_STATEMENT";
            if (f.StartsWith("mca")) return "MCA_COMPANY_RECORD";
            if (f.StartsWith("din")) return "MCA_DIRECTOR_RECORD";
            return "OTHER";
        }

        private static string ProviderFor(string file)
        {
            var f = file.ToLowerInvariant();
            if (f.StartsWith("udyam")) return "MINISTRY_OF_MSME";
            if (f.StartsWith("gst")) return "GSTN";
            if (f.StartsWith("itr")) return "INCOME_TAX_DEPARTMENT";
            if (f.StartsWith("aa")) return "FIP_VIA_AA";
            if (f.StartsWith("mca") || f.StartsWith("din")) return "MCA21";
            return "PLATFORM";
        }

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
