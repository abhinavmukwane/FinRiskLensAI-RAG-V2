using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// ONDC financial services — beckn protocol, domain ONDC:FIS12 (credit). A beckn
    /// message is a <c>context</c> envelope (who, what action, which transaction)
    /// around a <c>message.order</c>: the borrower is the customer, the loan is the
    /// item, and our score rides along as tags on the order. Nothing like the other
    /// two shapes, which is the whole reason each channel has its own builder.
    /// </summary>
    public class OndcPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.ONDC;

        public object Build(BankCustomerRow c, RiskAnalysisResult r, LoanCaseContext ctx)
        {
            var lending = r.Lending;
            var top = r.Recommendations.FirstOrDefault();
            var isoTime = ctx.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

            return new
            {
                context = new
                {
                    domain = "ONDC:FIS12",
                    country = "IND",
                    city = "std:022",
                    action = "init",
                    core_version = "1.2.0",
                    bap_id = ctx.OndcSubscriberId,
                    bap_uri = ctx.OndcSubscriberUri,
                    bpp_id = $"{ctx.LenderId.ToLowerInvariant()}.lender.ondc",
                    transaction_id = ctx.TransactionId,
                    message_id = Guid.NewGuid().ToString(),
                    timestamp = isoTime,
                    ttl = "PT30S"
                },
                message = new
                {
                    order = new
                    {
                        id = ctx.CaseReference,
                        provider = new { id = ctx.LenderId, descriptor = new { name = ctx.LenderName } },
                        items = new[]
                        {
                            new
                            {
                                id = top?.SchemeCode ?? "MSME_WC",
                                descriptor = new { name = top?.ProductName ?? "MSME Working Capital", code = top?.SchemeCode ?? "MSME_WC" },
                                price = new { currency = "INR", value = Rupees(lending?.TotalIndicativeEligibility ?? 0).ToString("0.00") },
                                tags = new object[]
                                {
                                    Tag("LOAN_INFO", new Dictionary<string, string>
                                    {
                                        ["INTEREST_RATE"] = "11",
                                        ["TERM"] = lending != null && lending.TermLoanCapacity > 0 ? "P60M" : "P12M",
                                        ["INTEREST_RATE_TYPE"] = "FLOATING",
                                        ["APPLICATION_FEE"] = "0",
                                        ["WORKING_CAPITAL_LIMIT"] = Rupees(lending?.WorkingCapitalLimit ?? 0).ToString("0"),
                                        ["TERM_LOAN_CAPACITY"] = Rupees(lending?.TermLoanCapacity ?? 0).ToString("0")
                                    })
                                }
                            }
                        },
                        customer = new
                        {
                            person = new { name = c.EnterpriseName },
                            contact = new { phone = c.MobileNumber, email = c.Email }
                        },
                        fulfillments = new[]
                        {
                            new
                            {
                                id = "F1",
                                type = "LOAN",
                                customer = new { person = new { name = c.EnterpriseName } },
                                state = new { descriptor = new { code = "INITIATED" } }
                            }
                        },
                        tags = new object[]
                        {
                            Tag("BORROWER_IDENTITY", new Dictionary<string, string>
                            {
                                ["UDYAM"] = c.Uan,
                                ["PAN"] = c.PanNumber ?? "",
                                ["GSTIN"] = c.GstinNumber ?? "",
                                ["CONSTITUTION"] = c.OrganizationType ?? "",
                                ["STATE"] = c.State ?? "",
                                ["NIC_2_DIGIT"] = r.Industry?.Nic2Digit ?? ""
                            }),
                            Tag("CREDIT_ASSESSMENT", new Dictionary<string, string>
                            {
                                ["PROVIDER"] = "FINRISKLENSAI",
                                ["MODEL_VERSION"] = r.ModelVersion,
                                ["FINANCIAL_HEALTH_SCORE"] = Math.Round(r.OverallScore).ToString("0"),
                                ["SCALE"] = "0-1000",
                                ["BAND"] = r.ScoreBand.ToString(),
                                ["COMPUTED_AT"] = r.ComputedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                                ["DATA_INTEGRITY_FLAG"] = r.Anomaly.IsAnomalous ? "Y" : "N",
                                ["ANNUAL_TURNOVER"] = Rupees(lending?.AnnualTurnover ?? 0).ToString("0"),
                                ["MONTHLY_SURPLUS"] = Rupees(lending?.MonthlySurplus ?? 0).ToString("0")
                            }),
                            Tag("SCORE_DIMENSIONS", r.Dimensions.ToDictionary(
                                d => ToKey(d.Dimension),
                                d => $"{Math.Round(d.Score, 1)}/{d.MaxPoints:0}")),
                            Tag("DATA_SOURCES", ctx.DataSources
                                .Select((s, i) => (key: $"SOURCE_{i + 1}", value: s))
                                .ToDictionary(x => x.key, x => x.value))
                        }
                    }
                }
            };
        }

        private static object Tag(string code, Dictionary<string, string> values) => new
        {
            descriptor = new { code },
            list = values.Select(kv => new { descriptor = new { code = kv.Key }, value = kv.Value })
        };

        private static string ToKey(string name)
            => new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray())
                .Trim().Replace(' ', '_').ToUpperInvariant();

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
