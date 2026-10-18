using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Services.Implementation.LoanCase.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Tests
{
    public class LoanCasePayloadTests
    {
        private readonly BankCustomerRow _customer = new()
        {
            Uan = "UDYAM-MH-99-0000301",
            EnterpriseName = "Clear Solar Innovations Pvt Ltd",
            OrganizationType = "Private Limited",
            State = "Maharashtra"
        };

        private readonly SourceFacts _facts = new()
        {
            Version = "1.0",
            ComputedAt = DateTime.UtcNow,
            Features = new MsmeFeatureSet
            {
                HasGst = true,
                GstMonthlyAvgTurnover = 500000,
                HasItr = true,
                ItrBusinessTurnover = 6000000,
                HasAa = true,
                AaMonthlyAvgBankCredit = 520000
            }
        };

        private readonly LoanCaseContext _context = new()
        {
            CaseReference = "TEST-REF-001",
            DataSources = new() { "udyam.json", "gst_taxpayer.json", "itr.json", "aa.json" },
            RiskAssessment = new LoanCaseRiskAssessment
            {
                Verdict = RiskVerdict.Clear,
                AssessedAt = DateTime.UtcNow.AddHours(-1),
                ReviewedFindings = new()
                {
                    new() { Type = "METADATA_ANOMALY", Severity = FindingSeverity.Low, ReviewStatus = ReviewStatus.Dismissed }
                }
            },
            CreditDecision = new LoanCaseCreditDecision
            {
                Decision = "Approve",
                Product = "WorkingCapital",
                Amount = 500000,
                TenureMonths = 36,
                Emi = 16369,
                Foir = 55,
                Reasons = new List<object> { new { text = "Good banking conduct and low volatility", sources = new[] { "§3.1.2" } } },
                Conditions = new() { "Submission of latest GST return prior to disbursement" },
                Model = "gemini-1.5-pro",
                AcceptedBy = "credit_officer_1"
            }
        };

        [Fact]
        public void LosPayload_contains_riskAssessment_and_creditDecision()
        {
            var builder = new LosPayloadBuilder();
            var payload = builder.Build(_customer, _facts, _context);
            var json = JsonConvert.SerializeObject(payload);
            var parsed = JObject.Parse(json);

            var risk = parsed["riskAssessment"];
            Assert.NotNull(risk);
            Assert.Equal("Clear", risk!["verdict"]?.ToString());
            Assert.Equal("METADATA_ANOMALY", risk["reviewedFindings"]?[0]?["type"]?.ToString());
            Assert.Equal("Dismissed", risk["reviewedFindings"]?[0]?["reviewStatus"]?.ToString());

            var decision = parsed["creditDecision"];
            Assert.NotNull(decision);
            Assert.Equal("Approve", decision!["decision"]?.ToString());
            Assert.Equal(500000m, decision.Value<decimal>("amount"));
            Assert.Equal(36, decision.Value<int>("tenure"));
            Assert.Equal(16369m, decision.Value<decimal>("emi"));
            Assert.Equal("credit_officer_1", decision["acceptedBy"]?.ToString());
        }

        [Fact]
        public void UliPayload_contains_riskAssessment_and_creditDecision()
        {
            var builder = new UliPayloadBuilder();
            var payload = builder.Build(_customer, _facts, _context);
            var json = JsonConvert.SerializeObject(payload);
            var parsed = JObject.Parse(json);

            var content = parsed.SelectToken("dataPackets.derived[0].content");
            Assert.NotNull(content);

            var risk = content!["riskAssessment"];
            Assert.NotNull(risk);
            Assert.Equal("Clear", risk!["verdict"]?.ToString());

            var decision = content["creditDecision"];
            Assert.NotNull(decision);
            Assert.Equal("Approve", decision!["decision"]?.ToString());
            Assert.Equal(500000m, decision.Value<decimal>("amount"));
            Assert.Equal("credit_officer_1", decision["acceptedBy"]?.ToString());
        }

        [Fact]
        public void OndcPayload_contains_riskAssessment_and_creditDecision_tags()
        {
            var builder = new OndcPayloadBuilder();
            var payload = builder.Build(_customer, _facts, _context);
            var json = JsonConvert.SerializeObject(payload);
            var parsed = JObject.Parse(json);

            var tags = parsed.SelectTokens("message.order.tags[*]").OfType<JObject>().ToList();
            Assert.NotEmpty(tags);

            var riskTag = tags.FirstOrDefault(t => t.SelectToken("descriptor.code")?.ToString() == "RISK_ASSESSMENT");
            Assert.NotNull(riskTag);
            var verdictItem = riskTag!.SelectTokens("list[*]").FirstOrDefault(i => i.SelectToken("descriptor.code")?.ToString() == "VERDICT");
            Assert.NotNull(verdictItem);
            Assert.Equal("Clear", verdictItem!["value"]?.ToString());

            var decisionTag = tags.FirstOrDefault(t => t.SelectToken("descriptor.code")?.ToString() == "CREDIT_DECISION");
            Assert.NotNull(decisionTag);
            var decisionItem = decisionTag!.SelectTokens("list[*]").FirstOrDefault(i => i.SelectToken("descriptor.code")?.ToString() == "DECISION");
            Assert.NotNull(decisionItem);
            Assert.Equal("Approve", decisionItem!["value"]?.ToString());
            var amountItem = decisionTag.SelectTokens("list[*]").FirstOrDefault(i => i.SelectToken("descriptor.code")?.ToString() == "AMOUNT");
            Assert.NotNull(amountItem);
            Assert.Equal("500000", amountItem!["value"]?.ToString());
        }
    }
}
