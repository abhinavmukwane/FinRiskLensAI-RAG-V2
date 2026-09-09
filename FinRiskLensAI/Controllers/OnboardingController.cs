using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.ICommon;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Interfaces.IServices.OnBoarding;
using FinRiskLensAI.Core.Models;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.User_Activity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    public class OnboardingController : Controller
    {
        private readonly IOnboardingService _onboardingService;
        private readonly IEmailService _emailService;
        private readonly IEncryption _encryption;
        private readonly IMsmeDataStore _store;
        private readonly IDummyDataService _dummyData;
        private readonly ISimBankAccountService _simBank;

        public OnboardingController(IOnboardingService onboardingService, IEmailService emailService,
            IEncryption encryption, IMsmeDataStore store, IDummyDataService dummyData,
            ISimBankAccountService simBank)
        {
            _onboardingService = onboardingService;
            _emailService = emailService;
            _encryption = encryption;
            _store = store;
            _dummyData = dummyData;
            _simBank = simBank;
        }

        /// <summary>
        /// Pulls the generated mobile / email out of a Udyam response so the
        /// SimBanks account can be keyed to the same identity the MSME will use.
        /// </summary>
        private static (string? Mobile, string? Email) ReadContact(string? udyamJson)
        {
            if (string.IsNullOrWhiteSpace(udyamJson)) return (null, null);
            try
            {
                var main = Newtonsoft.Json.Linq.JObject.Parse(udyamJson)["main_details"];
                return (main?.Value<string>("mobile_number"), main?.Value<string>("email"));
            }
            catch
            {
                return (null, null);
            }
        }
        public IActionResult CustOnboarding()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> FetchUdyam(string uan)
        {
            try
            {
                var result = await _onboardingService.FetchUdyam(uan);
                SaveMsmeResultModel saveResult;
                string effectiveUan;

                if (result.Result == tflResultType.tflSuccess && result.Data != null)
                {
                    saveResult = await _onboardingService.SaveMsmeData(result.Data.UdyamNumberResponce);
                    effectiveUan = result.Data.UdyamNumber;
                }
                else
                {
                    var dummyUdyamResp = _dummyData.GetDummyUdyam(uan);
                    saveResult = await _onboardingService.SaveMsmeData(dummyUdyamResp);
                    effectiveUan = uan;

                    // Only upload for ML pipeline if this is a genuinely new enquiry.
                    if (!saveResult.AlreadyRegistered)
                    {
                        await _store.UploadAsync(uan, MsmeDataFiles.Udyam, dummyUdyamResp, HttpContext.RequestAborted);

                        // Register the freshly generated mobile with the SimBanks
                        // sandbox so the AA consent flow has an account to discover
                        // for this MSME. Best-effort — never blocks onboarding.
                        var (mobile, email) = ReadContact(dummyUdyamResp);
                        await _simBank.RegisterAccountAsync(mobile, email, HttpContext.RequestAborted);
                    }
                }

                if (!saveResult.Status)
                {
                    return Json(new { status = false, message = saveResult.Message ?? "Failed to save MSME data." });
                }

                if (saveResult.AlreadyRegistered)
                {
                    return Json(new
                    {
                        status = true,
                        isRegistered = true,
                        message = saveResult.Message ?? "User is already registered. Kindly login."
                    });
                }

                return Json(new
                {
                    status = true,
                    isRegistered = false,
                    uan = effectiveUan,
                    message = "Success"
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        //[HttpPost]
        //public async Task<IActionResult> FetchUdyam(string uan)
        //{
        //    try
        //    {
        //        var result = await _onboardingService.FetchUdyam(uan);

        //        if (result.Result == tflResultType.tflSuccess && result.Data != null)
        //        {
        //            await _onboardingService.SaveMsmeData(result.Data.UdyamNumberResponce);

        //            return Json(new{status = true, uan = result.Data.UdyamNumber, message = "Success"});
        //        }
        //        else
        //        {
        //            var dummyUdyamResp = _dummyData.GetDummyUdyam(uan);

        //            await _onboardingService.SaveMsmeData(dummyUdyamResp);

        //            // Drop udyam.json into the UAN's blob folder so the ML pipeline has source data.
        //            await _store.UploadAsync(uan, MsmeDataFiles.Udyam, dummyUdyamResp, HttpContext.RequestAborted);

        //            return Json(new { status = true, uan = uan, message = "Success" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new{status = false, message = ex.Message});
        //    }
        //}
        /// <summary>
        /// Dev/test only — sends the login-OTP template email with a random code.
        /// GET /Onboarding/SendTestEmail?to=someone@example.com&name=Abhinav
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SendTestEmail(string to, string? name, string? theme, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(to))
                return Json(new { status = false, message = "Pass ?to=recipient@email.com" });

            try
            {
                var otp = Random.Shared.Next(100000, 999999).ToString();
                var sent = await _emailService.SendLoginOtpAsync(to, otp, name, expiryMinutes: 10, theme: theme, ct: ct);

                return Json(new
                {
                    status = sent,
                    message = sent ? $"Test OTP email sent to {to} (code {otp})" : "SMTP send failed — check logs/cre-*.log for details"
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUdyamDetails(string uan)
        {
            try
            {
                var result = await _onboardingService.GetUdyamDetails(uan);

                string? emailEnc = null;
                string? mobileEnc = null;

                if (result.Result == tflResultType.tflSuccess && result.Data != null)
                {
                    // Encrypt the real values for the client's hidden fields first,
                    // then overwrite the model's own Email/Mobile with the masked
                    // display strings — the real values must never reach the page
                    // in plain text, only ever masked (view) or encrypted (hidden).
                    if (!string.IsNullOrWhiteSpace(result.Data.Email))
                        emailEnc = _encryption.EncryptString(result.Data.Email);
                    if (!string.IsNullOrWhiteSpace(result.Data.Mobile))
                        mobileEnc = _encryption.EncryptString(result.Data.Mobile);

                    result.Data.Email = Universal.MaskEmail(result.Data.Email);
                    result.Data.Mobile = Universal.MaskMobile(result.Data.Mobile);
                }

                return Json(new
                {
                    status = result.Result == tflResultType.tflSuccess,
                    data = result.Data,
                    emailEnc,
                    mobileEnc,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> GenerateOtp(UserRegistrationModel model, string nameOfEnterprise, string theme, string? emailEnc, string? mobileEnc)
        {
            try
            {
                // Client only ever holds masked Email/MobileNumber — the real values
                // travel encrypted (see GetUdyamDetails) and are decrypted here, right
                // before the OTP is actually sent to the address / saved.
                if (!string.IsNullOrWhiteSpace(emailEnc))
                    model.Email = _encryption.DecryptString(emailEnc);
                if (!string.IsNullOrWhiteSpace(mobileEnc))
                    model.MobileNumber = _encryption.DecryptString(mobileEnc);

                // 1. Basic validation (adjust as per your model)
                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.MobileNumber))
                {
                    return Json(new { status = false, message = "Email and Mobile Number are required.", clearFields = false });
                }

                // 2. Generate OTP
                var otp = Random.Shared.Next(100000, 999999).ToString();

                // 3. Try sending the OTP email FIRST
                var emailSent = await _emailService.SendLoginOtpAsync(model.Email, otp, nameOfEnterprise,
                                expiryMinutes: 5, theme: theme, ct: HttpContext.RequestAborted);

                if (!emailSent)
                {
                    return Json(new { status = false, message = "Failed to send OTP email. Please try again.", clearFields = false });
                }

                // 4. Only persist the OTP if the email actually went out
                var otpModel = new UserOtpModel
                {
                    MsmeEnquiryID = model.MsmeEnquiryID,
                    Email = model.Email,
                    MobileNumber = model.MobileNumber,
                    OTP = _encryption.EncryptString(otp),
                };

                var otpResult = await _onboardingService.AddUpdateUserOtp(otpModel);
                if (otpResult == null)
                {
                    return Json(new { status = false, message = "OTP sent but could not be saved. Please contact support." });
                }

                return Json(new
                {
                    status = true,
                    message = "OTP sent successfully to your email.",
                    otp = otp
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = "Something went wrong while generating OTP." });
            }
        }



        [HttpPost]
        public async Task<IActionResult> FetchUserOTPDet(UserOtpModel model, string? emailEnc, string? mobileEnc)
        {
            // Client only ever holds masked Email/MobileNumber — the real values
            // travel encrypted (see GetUdyamDetails) and are decrypted here, right
            // before they're used for the OTP match and the saved registration.
            if (!string.IsNullOrWhiteSpace(emailEnc))
                model.Email = _encryption.DecryptString(emailEnc);
            if (!string.IsNullOrWhiteSpace(mobileEnc))
                model.MobileNumber = _encryption.DecryptString(mobileEnc);

            var result = await _onboardingService.FetchUserOTPDet(model);
            if (result.Result != tflResultType.tflSuccess)
            {
                return Json(new { status = false, message = result.Message });
            }

            var clientIp = await IP_Get_Service.GetClientIPAddressAsync(HttpContext);

            var regResult = await _onboardingService.AddUpdateUserRegst(model.MsmeEnquiryID , model.Email, clientIp);
            if (regResult.Result != tflResultType.tflSuccess)
            {
                return Json(new { status = false, message = regResult.Message });
            }

            var sessionUser = new UserSessionModel
            {
                UserRegistrationID = regResult.Data.UserRegistrationID,
                MsmeEnquiryID = regResult.Data.MsmeEnquiryID,
                Email = regResult.Data.Email,
                MobileNumber = regResult.Data.MobileNumber,
                ClientIP = clientIp,
                UdyamNumber=regResult.Data.UdyamNumber,
                PanNumber= regResult.Data.PanNumber,
                GstinNumber= regResult.Data.GstinNumber,
                NameOfEnterprise = result.Data.NameOfEnterprise
            };

            HttpContext.Session.SetCurrentUser(sessionUser);
            //regResult.Data.ClientIP = await IP_Get_Service.GetClientIPAddressAsync(HttpContext);
            //HttpContext.Session.SetCurrentUser(regResult.Data);

            return Json(new
            {
                status = true,
                message = "OTP validated successfully.",
                redirectUrl = Url.Action("CustDashboard", "Dashboard")
            });
        }




    }
}
