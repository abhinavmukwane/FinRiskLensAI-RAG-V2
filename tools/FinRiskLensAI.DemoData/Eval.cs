using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.DemoData
{
    /// <summary>
    /// How close an extraction is to the truth the document was drawn from. A row counts only
    /// if its date, debit, credit and balance are all exact — the checks are arithmetic, so a
    /// row that is nearly right is wrong.
    /// </summary>
    public static class ExtractionEval
    {
        public sealed record Score(string File, bool TypeOk, int TruthRows, int GotRows, int RowsExact,
                                   int PageValuesOk, int PageValues, int FieldsOk, int Fields);

        public static Score Compare(string file, ExtractedDocument truth, ExtractedDocument got)
        {
            int rowsExact = 0, pageOk = 0, pageTotal = 0;
            foreach (var tp in truth.Pages)
            {
                var gp = got.Pages.FirstOrDefault(p => p.PageNo == tp.PageNo);
                foreach (var (t, g) in new[]
                {
                    (tp.OpeningBalance, gp?.OpeningBalance), (tp.ClosingBalance, gp?.ClosingBalance),
                    (tp.StatedTotalDebit, gp?.StatedTotalDebit), (tp.StatedTotalCredit, gp?.StatedTotalCredit)
                })
                {
                    if (t == null) continue;
                    pageTotal++;
                    if (t == g) pageOk++;
                }
                for (int i = 0; i < tp.Rows.Count && gp != null && i < gp.Rows.Count; i++)
                {
                    var (t, g) = (tp.Rows[i], gp.Rows[i]);
                    if (t.Date == g.Date && (t.Debit ?? 0) == (g.Debit ?? 0) && (t.Credit ?? 0) == (g.Credit ?? 0) && t.Balance == g.Balance)
                        rowsExact++;
                }
            }

            int fieldsOk = truth.Fields.Count(tf =>
            {
                var gf = got.Fields.FirstOrDefault(f => f.Name == tf.Name);
                if (gf == null) return false;
                return tf.NumValue != null
                    ? tf.NumValue == gf.NumValue
                    : string.Equals(tf.Value.Trim(), gf.Value.Trim(), StringComparison.OrdinalIgnoreCase);
            });

            return new Score(file, truth.DocType == got.DocType,
                             truth.Pages.Sum(p => p.Rows.Count), got.Pages.Sum(p => p.Rows.Count), rowsExact,
                             pageOk, pageTotal, fieldsOk, truth.Fields.Count);
        }
    }
}
