using FinRiskLensAI.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinRiskLensAI.Utility
{
    /// <summary>
    /// Guards the bank portal. Mirrors CustDashboardAuthorize but reads the
    /// separate bank-user session slot, so an MSME session can never satisfy it
    /// and a bank session can never satisfy the customer dashboard.
    /// </summary>
    public class BankAdminAuthorize : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.Session.GetCurrentBankUser();

            if (user == null || user.AdmBankLoginID <= 0 || string.IsNullOrEmpty(user.UserId))
            {
                context.Result = new RedirectToActionResult("BankLogin", "Auth", null);
            }
        }
    }
}
