using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Facts;

namespace FinRiskLensAI.Services.Implementation.LoanCase.Payloads
{
    /// <summary>
    /// ONDC financial services — beckn protocol, domain ONDC:FIS12 (credit). A beckn
    /// message is a <c>context</c> envelope (who, what action, which transaction)
    /// around a <c>message.order</c>: the borrower is the customer, the loan is the
    /// item, and the verified facts ride along as tags on the order. Offer terms
    /// (rate, tenure, fees) are the lender's to set in its on_init reply, so none are
    /// sent here. Nothing like the other two shapes, which is the whole reason each
    /// channel has its own builder.
    /// </summary>
    public class OndcPayloadBuilder : ILoanCasePayloadBuilder
    {
        public LoanCaseChannel Channel => LoanCaseChannel.ONDC;

        public object Build(BankCustomerRow c, SourceFacts facts, LoanCaseContext ctx)
        {
            var f = facts.Features;
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
                                id = "MSME_WC",
                                descriptor = new { name = "MSME Working Capital", code = "MSME_WC" }
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
                                ["NIC_2_DIGIT"] = facts.Industry?.Nic2Digit ?? ""
                            }),
                            Tag("VERIFIED_FACTS", new Dictionary<string, string>
                            {
                                ["PROVIDER"] = "FINRISKLENS",
                                ["FACTS_VERSION"] = facts.Version,
                                ["COMPUTED_AT"] = facts.ComputedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                                ["GST_MONTHLY_TURNOVER"] = Rupees(f.GstMonthlyAvgTurnover).ToString("0"),
                                ["ITR_MONTHLY_TURNOVER"] = f.ItrBusinessTurnover > 0 ? Rupees(f.ItrBusinessTurnover / 12).ToString("0") : "",
                                ["BANK_MONTHLY_CREDITS"] = Rupees(f.AaMonthlyAvgBankCredit).ToString("0"),
                                ["EXISTING_MONTHLY_EMI"] = Rupees(f.AaEmiMonthlyOutflow).ToString("0"),
                                ["BOUNCES"] = (facts.BankAnalysis?.BounceCount ?? 0).ToString()
                            }),
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

        private static decimal Rupees(double v) => Math.Round((decimal)v, 2);
    }
}
