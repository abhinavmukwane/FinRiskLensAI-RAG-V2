using System.Collections.Generic;

namespace FinRiskLensAI.Core.Models.Mca
{
    /// <summary>
    /// Strongly-typed DIN (Director Identification Number) verification response,
    /// stored in m_StaticResponces.DINResponce. The column may hold a single
    /// director object or an array of them (one per company director); the
    /// controller locates the entry matching the requested DIN.
    /// </summary>
    public class DinResponseModel
    {
        public string? din { get; set; }
        /// <summary>The nested message node names it din_number rather than din.</summary>
        public string? din_number { get; set; }
        public string? name { get; set; }
        public string? full_name { get; set; }
        public string? father_name { get; set; }
        public string? dob { get; set; }
        public string? date_of_birth { get; set; }
        public string? nationality { get; set; }
        public string? pan { get; set; }
        /// <summary>The nested message node names it pan_number rather than pan.</summary>
        public string? pan_number { get; set; }
        public string? email { get; set; }
        public string? email_id { get; set; }
        public string? present_address { get; set; }
        public string? permanent_address { get; set; }
        public string? date_of_appointment { get; set; }
        public string? din_allocation_date { get; set; }
        public string? status { get; set; }

        public List<DinCompanyModel>? companies { get; set; }
        public List<DinCompanyModel>? company_list { get; set; }
        /// <summary>The nested message node names the directorship list companies_associated.</summary>
        public List<DinCompanyModel>? companies_associated { get; set; }

        public class DinCompanyModel
        {
            public string? cin { get; set; }
            public string? company_name { get; set; }
            public string? designation { get; set; }
            public string? date_of_appointment { get; set; }
        }
    }
}
