using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Raise a scored MSME as a loan case in LOS / ULI / ONDC from the bank portal.
    /// <para>
    /// Bank-session only. The payload carries PAN, mobile, email and the full
    /// financials, so every action here is gated by <see cref="BankAdminAuthorize"/>
    /// and reads the bank session explicitly — never inferred from "some session
    /// exists", because the customer and bank sessions share one cookie.
    /// </para>
    /// </summary>
    [BankAdminAuthorize]
    [Route("loan-case")]
    public class LoanCaseController : Controller
    {
        private readonly ILoanCaseService _loanCase;
        private readonly ILogger<LoanCaseController> _logger;
        private readonly ICreditDecisionService _credit;

        public LoanCaseController(ILoanCaseService loanCase, ILogger<LoanCaseController> logger, ICreditDecisionService credit)
        {
            _loanCase = loanCase;
            _credit = credit;
            _logger = logger;
        }

        /// <summary>Latest push per channel for the chips (also used to refresh after a push).</summary>
        [HttpGet("status")]
        public async Task<IActionResult> Status(string uan, CancellationToken ct)
        {
            var pushes = await _loanCase.GetStatusAsync(uan, ct);
            return Json(new
            {
                status = true,
                pushes = pushes.Select(p => new
                {
                    id = p.LoanCasePushID, channel = p.Channel.ToString(), state = p.Status.ToString(),
                    caseReference = p.CaseReference,
                    pushedAt = p.PushedAt.ToString("dd MMM yyyy HH:mm"), pushedBy = p.PushedByName
                }),
                live = Enum.GetValues<LoanCaseChannel>().Where(_loanCase.IsLive).Select(c => c.ToString())
            });
        }

        /// <summary>
        /// The exact JSON a push would send right now. Pure read, no side effects —
        /// opened in a new tab from the modal so the officer can inspect it before
        /// confirming.
        /// </summary>
        [HttpGet("preview")]
        public async Task<IActionResult> Preview(string uan, string channel, CancellationToken ct)
        {
            if (!TryChannel(channel, out var ch))
                return View("Payload", PayloadPageModel.Error($"Unknown channel '{channel}'."));
            // T10: only a Clear customer with an accepted Approve goes downstream
            if (await _credit.PushBlockedAsync(uan, ct) is string blocked)
                return View("Payload", PayloadPageModel.Error(blocked));

            var user = HttpContext.Session.GetCurrentBankUser()!;
            var preview = await _loanCase.PreviewAsync(uan, ch, user, ct);

            if (preview.Error != null)
                return View("Payload", PayloadPageModel.Error(preview.Error));

            return View("Payload", new PayloadPageModel
            {
                Mode = "preview",
                Title = $"{ch} payload — preview",
                Uan = preview.Uan,
                EnterpriseName = preview.EnterpriseName,
                Channel = ch.ToString(),
                IsLive = preview.IsLive,
                Endpoint = preview.Endpoint,
                PayloadJson = preview.PayloadJson
            });
        }

        /// <summary>Builds, sends (or simulates) and records the case. One click, one row.</summary>
        [HttpPost("push")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Push(string uan, string channel, CancellationToken ct)
        {
            if (!TryChannel(channel, out var ch))
                return Json(new { status = false, message = $"Unknown channel '{channel}'." });
            if (await _credit.PushBlockedAsync(uan, ct) is string blocked)
                return Json(new { status = false, message = blocked });

            var user = HttpContext.Session.GetCurrentBankUser()!;
            var ip = await IP_Get_Service.GetClientIPAddressAsync(HttpContext);

            var result = await _loanCase.PushAsync(uan, ch, user, ip, ct);

            return Json(new
            {
                status = result.Succeeded,
                message = result.Message,
                pushId = result.LoanCasePushID,
                channel = result.Channel.ToString(),
                state = result.Status.ToString(),
                caseReference = result.CaseReference,
                receiptUrl = result.LoanCasePushID > 0 ? Url.Action(nameof(Receipt), new { id = result.LoanCasePushID }) : null
            });
        }

        /// <summary>
        /// What was actually sent, plus the downstream response — a stable URL per
        /// push, so it can be shared and shows the recorded bytes, not a re-render.
        /// </summary>
        [HttpGet("receipt/{id:int}")]
        public async Task<IActionResult> Receipt(int id, CancellationToken ct)
        {
            var push = await _loanCase.GetPushAsync(id, ct);
            if (push == null)
                return View("Payload", PayloadPageModel.Error($"No loan-case push #{id} on record."));

            return View("Payload", new PayloadPageModel
            {
                Mode = "receipt",
                Title = $"{push.Channel} payload — {push.Status}",
                Uan = push.Uan,
                EnterpriseName = push.EnterpriseName,
                Channel = push.Channel.ToString(),
                Status = push.Status.ToString(),
                CaseReference = push.CaseReference,
                IsLive = push.Status == LoanCasePushStatus.Sent,
                Endpoint = push.Endpoint,
                HttpStatus = push.HttpStatus,
                PushedAt = push.PushedAt,
                PushedBy = push.PushedByName ?? push.PushedByUserId,
                PayloadJson = push.Payload,
                ResponseJson = push.ResponseBody,
                ErrorMessage = push.ErrorMessage
            });
        }

        private static bool TryChannel(string? value, out LoanCaseChannel channel)
            => Enum.TryParse(value?.Trim(), ignoreCase: true, out channel) && Enum.IsDefined(channel);
    }

    /// <summary>View model for the new-tab payload page (preview and receipt share it).</summary>
    public class PayloadPageModel
    {
        public string Mode { get; set; } = "preview";      // preview | receipt | error
        public string Title { get; set; } = "Loan case payload";
        public string? Uan { get; set; }
        public string? EnterpriseName { get; set; }
        public string? Channel { get; set; }
        public string? Status { get; set; }
        public string? CaseReference { get; set; }
        public bool IsLive { get; set; }
        public string? Endpoint { get; set; }
        public int? HttpStatus { get; set; }
        public DateTime? PushedAt { get; set; }
        public string? PushedBy { get; set; }
        public string PayloadJson { get; set; } = "";
        public string? ResponseJson { get; set; }
        public string? ErrorMessage { get; set; }
        public string? LoadError { get; set; }

        public static PayloadPageModel Error(string message) => new() { Mode = "error", Title = "Loan case payload", LoadError = message };
    }
}
