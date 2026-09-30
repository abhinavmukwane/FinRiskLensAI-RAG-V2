using FinRiskLensAI.Core.Models.Facts;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.ML.Features
{
    /// <summary>
    /// Extracts revenue and compliance features from GST API responses: taxpayer
    /// profile, monthly GSTR-3B details, GSTR-1 summaries / B2B invoices / CDNR
    /// (credit-debit notes) / HSN summaries, and GSTR-2A inward (purchase) invoices.
    /// Real API responses arrive wrapped in a response.message.data envelope —
    /// <see cref="Unwrap"/> handles both wrapped and bare payloads.
    /// </summary>
    public class GstFeatureExtractor
    {
        public void Extract(
            string? taxpayerJson,
            IReadOnlyList<string>? gstr3bJsons,
            IReadOnlyList<string>? gstr1SummaryJsons,
            IReadOnlyList<string>? gstr1B2bJsons,
            IReadOnlyList<string>? gstr1CdnrJsons,
            IReadOnlyList<string>? gstr1HsnJsons,
            IReadOnlyList<string>? gstr2aB2bJsons,
            IReadOnlyList<string>? gstr2bJsons,
            MsmeFeatureSet features)
        {
            var anyData = false;

            if (!string.IsNullOrWhiteSpace(taxpayerJson))
            {
                anyData = true;
                var profile = Unwrap(JObject.Parse(taxpayerJson));
                features.GstRegistrationActive = string.Equals(
                    profile.Value<string>("sts"), "Active", StringComparison.OrdinalIgnoreCase);
            }

            // ── Monthly turnover from GSTR-3B outward taxable supplies (osup_det.txval),
            //    plus tax-payment discipline (cash vs ITC) and net ITC claimed
            double taxPaidCash = 0, taxPaidItc = 0, itcNetTotal = 0;
            int itcMonths = 0;
            foreach (var data in ParseAll(gstr3bJsons, ref anyData))
            {
                var period = data.Value<string>("ret_period");           // MMyyyy
                if (string.IsNullOrEmpty(period) || period.Length != 6) continue;

                var sortablePeriod = period[2..] + period[..2];           // yyyyMM for ordering
                var txval = data.SelectToken("sup_details.osup_det.txval")?.Value<double>() ?? 0;
                var zeroRated = data.SelectToken("sup_details.osup_zero.txval")?.Value<double>() ?? 0;
                features.GstMonthlyTurnover[sortablePeriod] = txval + zeroRated;

                // Tax settled in cash (pdcash: ipd/cpd/spd/cspd) vs via ITC (pditc: *_pd*)
                foreach (var pd in data.SelectTokens("tx_pmt.pdcash[*]").OfType<JObject>())
                    taxPaidCash += Sum(pd, "ipd", "cpd", "spd", "cspd");
                var pditc = data.SelectToken("tx_pmt.pditc") as JObject;
                if (pditc != null)
                    taxPaidItc += Sum(pditc, "i_pdi", "i_pdc", "i_pds", "c_pdi", "c_pdc", "s_pdi", "s_pds", "cs_pdcs");

                // Net input tax credit for the month
                var itcNet = data.SelectToken("itc_elg.itc_net") as JObject;
                if (itcNet != null)
                {
                    itcNetTotal += Sum(itcNet, "iamt", "camt", "samt", "csamt");
                    itcMonths++;
                }
            }

            // ── B2B share + counterparty diversity from GSTR-1 summaries,
            //    plus per-period GSTR-1 declared totals for the 1-vs-3B consistency check
            double b2bTax = 0, totalTax = 0;
            var r1MonthlyTaxable = new Dictionary<string, double>();
            var counterparties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var data in ParseAll(gstr1SummaryJsons, ref anyData))
            {
                var r1Period = data.Value<string>("ret_period");
                var r1Sortable = r1Period?.Length == 6 ? r1Period[2..] + r1Period[..2] : null;
                double r1PeriodTaxable = 0;

                foreach (var sec in data.SelectTokens("sec_sum[*]").OfType<JObject>())
                {
                    var name = sec.Value<string>("sec_nm") ?? string.Empty;
                    var tax = sec.Value<double?>("ttl_tax") ?? 0;
                    if (name is "B2B" or "B2CL" or "B2CS" or "EXP") { totalTax += tax; r1PeriodTaxable += tax; }
                    if (name == "B2B") b2bTax += tax;

                    foreach (var ctin in sec.SelectTokens("$..cpty_sum[*].ctin"))
                    {
                        var value = ctin.Value<string>();
                        if (!string.IsNullOrEmpty(value)) counterparties.Add(value);
                    }
                }
                if (r1Sortable != null) r1MonthlyTaxable[r1Sortable] = r1PeriodTaxable;
            }

            // ── Customer concentration from GSTR-1 B2B / e-invoice payloads (value per buyer GSTIN)
            var customerValue = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var data in ParseAll(gstr1B2bJsons, ref anyData))
            {
                foreach (var buyer in data.SelectTokens("b2b[*]").OfType<JObject>())
                {
                    var ctin = buyer.Value<string>("ctin");
                    if (string.IsNullOrEmpty(ctin)) continue;
                    counterparties.Add(ctin);
                    var value = buyer.SelectTokens("inv[*].itms[*].itm_det.txval")
                        .Sum(t => t.Value<double?>() ?? 0);
                    customerValue[ctin] = customerValue.GetValueOrDefault(ctin) + value;
                }
            }

            // ── Credit/debit notes (CDNR): total note value → revenue-reversal signal
            double creditNoteValue = 0;
            foreach (var data in ParseAll(gstr1CdnrJsons, ref anyData))
            {
                foreach (var txval in data.SelectTokens("cdnr[*].nt[*].itms[*].itm_det.txval"))
                    creditNoteValue += txval.Value<double?>() ?? 0;
            }

            // ── HSN summaries: distinct product/service codes sold
            var hsnCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var data in ParseAll(gstr1HsnJsons, ref anyData))
            {
                foreach (var code in data.SelectTokens("hsn.hsn_b2b[*].hsn_sc")
                             .Concat(data.SelectTokens("hsn.hsn_b2c[*].hsn_sc")))
                {
                    var value = code.Value<string>();
                    if (!string.IsNullOrEmpty(value)) hsnCodes.Add(value);
                }
            }

            // ── GSTR-2A: inward (purchase) invoice values + vendor concentration
            double purchaseValue = 0;
            int purchaseMonths = 0;
            var vendorValue = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var data in ParseAll(gstr2aB2bJsons, ref anyData))
            {
                purchaseMonths++;
                foreach (var supplier in data.SelectTokens("b2b[*]").OfType<JObject>())
                {
                    var ctin = supplier.Value<string>("ctin") ?? "(unregistered)";
                    var value = supplier.SelectTokens("inv[*].itms[*].itm_det.txval")
                        .Sum(t => t.Value<double?>() ?? 0);
                    purchaseValue += value;
                    vendorValue[ctin] = vendorValue.GetValueOrDefault(ctin) + value;
                }
            }

            // ── GSTR-2B: ITC available/unavailable + supplier filing discipline.
            //    Payload is double-nested: response.message.data → { chksum, data: {...} }
            double itcAvailableTotal = 0, itcUnavailableTotal = 0;
            int itc2bMonths = 0, suppliersTotal = 0, suppliersFiled = 0;
            foreach (var outer in ParseAll(gstr2bJsons, ref anyData))
            {
                var body = outer["data"] as JObject ?? outer;
                if (body["itcsumm"] == null && body["docdata"] == null) continue;

                itc2bMonths++;
                itcAvailableTotal += SumItcHeads(body.SelectToken("itcsumm.itcavl") as JObject);
                itcUnavailableTotal += SumItcHeads(body.SelectToken("itcsumm.itcunavl") as JObject);

                foreach (var supplier in body.SelectTokens("docdata.b2b[*]").OfType<JObject>())
                {
                    suppliersTotal++;
                    if (!string.IsNullOrWhiteSpace(supplier.Value<string>("supfildt")))
                        suppliersFiled++;
                }
            }

            if (!anyData) return;
            features.HasGst = true;
            features.GstB2bShare = totalTax > 0 ? Math.Clamp(b2bTax / totalTax, 0, 1) : 0;
            features.GstCounterpartyCount = counterparties.Count;
            features.GstHsnProductCount = hsnCodes.Count;

            // ── Tax-payment discipline + ITC pattern
            if (taxPaidCash + taxPaidItc > 0)
            {
                features.GstHasTaxPaymentData = true;
                features.GstCashTaxShare = taxPaidCash / (taxPaidCash + taxPaidItc);
            }
            features.GstItcMonthlyAvg = itcMonths > 0 ? itcNetTotal / itcMonths : 0;

            // ── GSTR-2B derived signals
            if (itc2bMonths > 0)
            {
                features.GstHas2bData = true;
                features.GstItcAvailableMonthly = Math.Round(itcAvailableTotal / itc2bMonths);
                if (features.GstItcAvailableMonthly > 0 && features.GstItcMonthlyAvg > 0)
                    features.GstItcClaimVsAvailable = Math.Round(
                        Math.Clamp(features.GstItcMonthlyAvg / features.GstItcAvailableMonthly, 0, 3), 4);
                if (itcAvailableTotal + itcUnavailableTotal > 0)
                    features.GstItcUnavailableShare = Math.Round(
                        itcUnavailableTotal / (itcAvailableTotal + itcUnavailableTotal), 4);
                if (suppliersTotal > 0)
                    features.GstSupplierFilingRate = Math.Round((double)suppliersFiled / suppliersTotal, 4);
            }

            // ── Customer / vendor concentration (top-5 shares, masked GSTINs)
            FillConcentration(customerValue, features.GstTopCustomers, v => features.GstTopCustomerShare = v);
            FillConcentration(vendorValue, features.GstTopVendors, v => features.GstTopVendorShare = v);

            if (features.GstMonthlyTurnover.Count > 0)
            {
                var series = features.GstMonthlyTurnover.Values.ToArray();
                features.GstTurnoverTrendSlope = TrendMath.NormalizedSlope(series);
                features.GstMonthlyAvgTurnover = series.Average();
                features.GstAnnualisedTurnover = series.Average() * 12;

                var totalTurnover = series.Sum();
                features.GstCreditNoteRatio = totalTurnover > 0
                    ? Math.Clamp(creditNoteValue / totalTurnover, 0, 1) : 0;

                if (purchaseMonths > 0)
                {
                    features.GstMonthlyAvgPurchases = purchaseValue / purchaseMonths;
                    features.GstPurchaseToSalesRatio = features.GstMonthlyAvgTurnover > 0
                        ? Math.Clamp(features.GstMonthlyAvgPurchases / features.GstMonthlyAvgTurnover, 0, 3) : 0;
                }

                // Filed periods vs the span they should cover (expects one 3B per month)
                var months = MonthsBetween(
                    features.GstMonthlyTurnover.Keys.First(),
                    features.GstMonthlyTurnover.Keys.Last()) + 1;
                features.GstFilingRegularity = Math.Clamp(
                    (double)features.GstMonthlyTurnover.Count / Math.Max(1, months), 0, 1);

                // GSTR-1 vs GSTR-3B consistency: average per-period agreement of
                // declared taxable values (persistent gaps = misdeclaration signal)
                var overlapping = features.GstMonthlyTurnover.Keys
                    .Where(p => r1MonthlyTaxable.ContainsKey(p) && features.GstMonthlyTurnover[p] > 0)
                    .ToList();
                if (overlapping.Count > 0)
                {
                    var avgGap = overlapping.Average(p =>
                        Math.Abs(r1MonthlyTaxable[p] - features.GstMonthlyTurnover[p])
                        / features.GstMonthlyTurnover[p]);
                    features.GstR1Vs3bConsistency = Math.Clamp(1 - avgGap, 0, 1);
                }
            }
        }

        private static double Sum(JObject obj, params string[] fields)
            => fields.Sum(f => obj.Value<double?>(f) ?? 0);

        /// <summary>
        /// Total tax across a GSTR-2B ITC summary node (itcavl / itcunavl): sums the
        /// top-level igst/cgst/sgst/cess of each supply category (nonrevsup, revsup,
        /// othersup, …) — nested b2b/cdnr breakups are skipped to avoid double counting.
        /// </summary>
        private static double SumItcHeads(JObject? summaryNode)
        {
            if (summaryNode == null) return 0;
            double total = 0;
            foreach (var category in summaryNode.Properties().Select(p => p.Value).OfType<JObject>())
                total += Sum(category, "igst", "cgst", "sgst", "cess");
            return total;
        }

        /// <summary>Top-5 counterparty shares of total value, GSTINs masked for display.</summary>
        private static void FillConcentration(
            Dictionary<string, double> valueByParty,
            Dictionary<string, double> target,
            Action<double> setTopShare)
        {
            var total = valueByParty.Values.Sum();
            if (total <= 0) return;
            var top = valueByParty.OrderByDescending(kv => kv.Value).Take(5).ToList();
            setTopShare(Math.Round(top[0].Value / total, 4));
            foreach (var (ctin, value) in top)
                target[Mask(ctin)] = Math.Round(value / total, 4);
        }

        private static string Mask(string ctin)
            => ctin.Length >= 15 ? $"{ctin[..4]}•••••{ctin[^4..]}" : ctin;

        /// <summary>
        /// Real GST APIs wrap the payload as response.message.data; schema samples and
        /// fixtures are bare. Accept both.
        /// </summary>
        private static JObject Unwrap(JObject root)
            => root.SelectToken("response.message.data") as JObject ?? root;

        private static IEnumerable<JObject> ParseAll(IReadOnlyList<string>? jsons, ref bool anyData)
        {
            var result = new List<JObject>();
            foreach (var json in jsons ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(json)) continue;
                anyData = true;
                result.Add(Unwrap(JObject.Parse(json)));
            }
            return result;
        }

        private static int MonthsBetween(string fromYyyyMm, string toYyyyMm)
        {
            var fy = int.Parse(fromYyyyMm[..4]); var fm = int.Parse(fromYyyyMm[4..]);
            var ty = int.Parse(toYyyyMm[..4]); var tm = int.Parse(toYyyyMm[4..]);
            return (ty - fy) * 12 + (tm - fm);
        }
    }
}
