using FinRiskLensAI.Core.Models.AccountAggregator;

namespace FinRiskLensAI.Core.Interfaces.IServices.Common
{
    public interface IDummyDataService
    {
        /// <summary>Random-but-valid dummy Udyam API response (JSON) for a UAN.</summary>
        string GetDummyUdyam(string uan);

        /// <summary>
        /// Dummy MCA (Ministry of Corporate Affairs) response (JSON) for a company —
        /// same company/owner name as the Udyam/GST data, with dynamic charges.
        /// </summary>
        string GetDummyMca(string uan, string companyName, string? pan = null);

        /// <summary>
        /// Dummy DIN (Director Identification Number) verification response (JSON) for a
        /// single director, mirroring the real MCA DIN API shape. Name is taken from the
        /// MCA director entry so the DIN profile stays in sync with the company's board.
        /// </summary>
        string GetDummyDin(string din, string directorName, string? pan = null);

        /// <summary>
        /// Dummy ITR (Income Tax Return) API response (JSON), mirroring the real ITR
        /// vendor's response contract. Shape depends on <paramref name="constitutionType"/>:
        /// "PROPRIETORSHIP" gets the presumptive-taxation ITR-4 (SUGAM) shape; anything
        /// else (partnership, LLP, company) gets the books-of-account ITR-5 shape, the
        /// only two response shapes documented for this API so far.
        /// <para>
        /// Only the financial figures are fabricated — identity fields (name, PAN,
        /// GSTIN, email, mobile, address) use the real onboarded values whenever the
        /// caller supplies them, falling back to a random-but-valid value only for
        /// whichever ones are missing.
        /// </para>
        /// <para>
        /// <paramref name="aaAccounts"/> — the MSME's real linked bank accounts from the
        /// Account Aggregator statement (<c>CustomerProfileBuilder.GetAaAnalysisAsync</c>
        /// → <c>AaAnalysisResult.Accounts</c>). <c>bank_details</c> is built from these;
        /// only when no AA data exists yet does it fall back to one random-but-valid account.
        /// </para>
        /// <para>
        /// <paramref name="assessmentYear"/> defaults to the assessment year for the most
        /// recently completed Indian financial year (April–March) as of today — never a
        /// fixed value — so the return always looks like it was just filed.
        /// </para>
        /// </summary>
        string GetDummyItr(string uan, string entityName, string constitutionType,
            string? pan = null, string? gstin = null, string? email = null, string? mobile = null,
            string? addressLine1 = null, string? city = null, string? state = null, string? pincode = null,
            IReadOnlyList<AaAccountInfo>? aaAccounts = null, string? assessmentYear = null);
    }
}
