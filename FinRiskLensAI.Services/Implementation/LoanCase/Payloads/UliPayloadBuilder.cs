using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// RBI Unified Lending Interface — OCEN lineage. ULI's model is that the
    /// <i>lender</i> decides; a service provider supplies evidence. So the score
    /// travels as a derived data packet alongside the source packets and consent
    /// artefacts, inside a loan application, and there is no "decision" block.
    /// </summary>
    public class UliPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.ULI;

        public object Build(BankCustomerRow c, RiskAnalysisResult r, LoanCaseContext ctx)
        {
            var lending = r.Lending;
            var top = r.Recommendations.FirstOrDefault();

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
                serviceProvider = new { id = "FINRISKLENSAI", role = "DERIVED_DATA_PROVIDER", modelVersion = r.ModelVersion },

                loanApplication = new
                {
                    applicationId = ctx.CaseReference,
                    productType = top?.SchemeCode ?? "WORKING_CAPITAL",
                    productName = top?.ProductName,
                    requestedAmount = Rupees(lending?.TotalIndicativeEligibility ?? 0),
                    currency = "INR",
                    purpose = "MSME_BUSINESS",
                    tenureMonths = lending != null && lending.TermLoanCapacity > 0 ? 60 : 12
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
                    industry = r.Industry == null ? null : new { nicCode = r.Industry.Nic2Digit, description = r.Industry.SectorName }
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
                            packetType = "CREDIT_ASSESSMENT",
                            provider = "FINRISKLENSAI",
                            computedAt = r.ComputedAt,
                            content = new
                            {
                                financialHealthScore = Math.Round(r.OverallScore),
                                scale = "0-1000",
                                band = r.ScoreBand.ToString(),
                                dimensions = r.Dimensions.ToDictionary(d => ToKey(d.Dimension), d => Math.Round(d.Score, 1)),
                                dataIntegrityFlag = r.Anomaly.IsAnomalous,
                                indicativeEligibility = lending == null ? null : new
                                {
                                    workingCapital = Rupees(lending.WorkingCapitalLimit),
                                    termLoan = Rupees(lending.TermLoanCapacity),
                                    total = Rupees(lending.TotalIndicativeEligibility)
                                },
                                keyRatios = (lending?.Ratios ?? new List<LendingRatio>())
                                    .ToDictionary(x => ToKey(x.Name), x => new { value = x.Value, status = x.Status.ToString() }),
                                financials = lending == null ? null : new
                                {
                                    annualTurnover = Rupees(lending.AnnualTurnover),
                                    monthlySurplus = Rupees(lending.MonthlySurplus),
                                    existingMonthlyEmi = Rupees(lending.ExistingMonthlyEmi)
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

        private static string ToKey(string name)
            => new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray())
                .Trim().Replace(' ', '_').ToUpperInvariant();

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
