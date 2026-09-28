using FinRiskLensAI.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinRiskLensAI.Utility
{
    public class CustDashboardAuthorize : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.Session.GetCurrentUser();

            if (user == null || user.MsmeEnquiryID == null || string.IsNullOrEmpty(user.Email))
            {
                context.Result = new RedirectToActionResult(
                    "CustLogin",
                    "Auth",
                    null);
            }
        }

    }
}
