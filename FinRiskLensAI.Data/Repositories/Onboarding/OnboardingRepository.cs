using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces.ICommon;
using FinRiskLensAI.Core.Interfaces.IRepositories.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IRepositories.OnBoarding;
using FinRiskLensAI.Core.Models;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Universal;
using FinRiskLensAI.Core.Models.User_Activity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinRiskLensAI.Data.Repositories.Onboarding
{
    public class OnboardingRepository : IOnboardingRepository
    {
        private readonly DbContextEDMX.ApplicationDbContext _context;
        private readonly IEncryption _encryption;


        public OnboardingRepository(DbContextEDMX.ApplicationDbContext context, IEncryption encryption)
        {
            _context = context; _encryption = encryption;
        }
        public async Task<ResultModel<StaticResponseModel>> FetchUdyam(string uan)
        {
            var result = new ResultModel<StaticResponseModel>();

            try
            {
                var entity = await _context.StaticResponseModel.FirstOrDefaultAsync(x => x.UdyamNumber == uan);

                if (entity != null)
                {

                    var model = new StaticResponseModel();

                    entity.MapToModelObject(model);

                    result.Result = tflResultType.tflSuccess;
                    result.Message = "Data Fetched Successfully";
                    result.Data = model;
                }
                else
                {
                    result.Result = tflResultType.tflNoRecordFound;
                    result.Message = "No Data Found";
                    result.Data = null;
                }
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                result.Data = null;
            }

            return result;
        }

        public async Task<ResultModel<StaticResponseModel>> AddUpdateStaticResponse(StaticResponseModel entity)
        {
            var result = new ResultModel<StaticResponseModel>();

            try
            {
                var existing = await _context.StaticResponseModel
                    .FirstOrDefaultAsync(x => x.UdyamNumber == entity.UdyamNumber)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    // Copy all properties from entity to existing object
                    entity.MapToModelObject(existing);

                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    result.Result = tflResultType.tflSuccess;
                    result.Message = "Static Response Updated Successfully";
                    result.Data = existing;
                }
                else
                {
                    StaticResponseModel newEntity = new StaticResponseModel();

                    // Copy all properties from entity to new object
                    entity.MapToModelObject(newEntity);

                    _context.StaticResponseModel.Add(newEntity);

                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    entity.StaticResponcesID = newEntity.StaticResponcesID;

                    result.Result = tflResultType.tflSuccess;
                    result.Message = "Static Response Saved Successfully";
                    result.Data = newEntity;
                }
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                result.Data = null;
            }

            return result;
        }

        public async Task<SaveMsmeResultModel> SaveMsmeData(string json)
        {
            try
            {
                var model = JsonConvert.DeserializeObject<UdyamResponseModel>(json);

                if (model == null)
                    return new SaveMsmeResultModel { Status = false, Message = "Invalid data." };

                var existing = await _context.MsmeEnquiries.FirstOrDefaultAsync(x => x.Uan == model.uan);
                if (existing != null)
                {
                    // User is already registered
                    if (existing.IsRegister == true)
                    {
                        return new SaveMsmeResultModel
                        {
                            Status = true,
                            AlreadyRegistered = true,
                            MsmeEnquiryID = existing.MsmeEnquiryID,
                            Message = "User is already registered. Kindly login."
                        };
                    }

                    // MSME record exists but registration is pending
                    return new SaveMsmeResultModel
                    {
                        Status = true,
                        AlreadyRegistered = false,
                        MsmeEnquiryID = existing.MsmeEnquiryID,
                        Message = "MSME details found."
                    };
                }

                var enquiry = new MsmeEnquiry
                {
                    ClientId = model.client_id,
                    Uan = model.uan,
                    CertificateUrl = model.certificate_url,
                    NameOfEnterprise = model.main_details.name_of_enterprise,
                    MajorActivity = model.main_details.major_activity,
                    SocialCategory = model.main_details.social_category,
                    DateOfCommencement = model.main_details.date_of_commencement,
                    DicName = model.main_details.dic_name,
                    State = model.main_details.state,
                    AppliedDate = model.main_details.applied_date,

                    Flat = model.main_details.flat,
                    NameOfBuilding = model.main_details.name_of_building,
                    Road = model.main_details.road,
                    Village = model.main_details.village,
                    Block = model.main_details.block,
                    City = model.main_details.city,
                    Pin = model.main_details.pin,

                    MobileNumber = model.main_details.mobile_number,
                    Email = model.main_details.email,
                    OrganizationType = model.main_details.organization_type,
                    Gender = model.main_details.gender,
                    DateOfIncorporation = model.main_details.date_of_incorporation,
                    MsmeDfo = model.main_details.msme_dfo,
                    RegistrationDate = model.main_details.registration_date,
                    GstinNumber = model.main_details.gstin,
                    PanNumber = model.main_details.Pan,

                    Payload = JsonConvert.SerializeObject(model),

                    CreatedAt = DateTime.Now
                };

                _context.MsmeEnquiries.Add(enquiry);

                await _context.SaveChangesAsync();

                if (model.location_of_plant_details != null)
                {
                    foreach (var item in model.location_of_plant_details)
                    {
                        var location = new MsmeLocation
                        {
                            MsmeEnquiryID = enquiry.MsmeEnquiryID,

                            UnitName = item.unit_name,
                            Line1 = item.line_1,
                            Building = item.building,
                            Village = item.village,
                            Street = item.street,
                            Road = item.road,
                            City = item.city,
                            Pin = item.pin,
                            State = item.state,
                            District = item.district,

                            CreatedAt = DateTime.Now
                        };

                        _context.MsmeLocations.Add(location);
                    }
                }
                if (model.nic_code != null)
                {
                    foreach (var item in model.nic_code)
                    {
                        var nic = new MsmeNicCode
                        {
                            MsmeEnquiryID = enquiry.MsmeEnquiryID,

                            Nic2Digit = item.nic_2_digit,
                            Nic4Digit = item.nic_4_digit,
                            Nic5Digit = item.nic_5_digit,
                            ActivityType = item.activity_type,
                            AddedOn = item.added_on,

                            CreatedAt = DateTime.Now
                        };

                        _context.MsmeNicCodes.Add(nic);
                    }
                }

                await _context.SaveChangesAsync();

                return new SaveMsmeResultModel
                {
                    Status = true,
                    AlreadyRegistered = false,
                    MsmeEnquiryID = enquiry.MsmeEnquiryID
                };
            }
            catch (Exception ex)
            {
                return new SaveMsmeResultModel
                {
                    Status = false,
                    AlreadyRegistered = false,
                    MsmeEnquiryID = 0,
                    Message = ex.Message
                };
            }
        }
        public async Task<UdyamDetailsModel> GetUdyamDetails(string uan)
        {
            var data = await (
                from e in _context.MsmeEnquiries
                join l in _context.MsmeLocations
                    on e.MsmeEnquiryID equals l.MsmeEnquiryID into loc
                from l in loc.DefaultIfEmpty()

                where e.Uan == uan

                select new
                {
                    e.Uan,e.NameOfEnterprise,e.OrganizationType, e.DateOfIncorporation,e.Email,e.MsmeEnquiryID,
                    e.MobileNumber,l.Line1,l.Building,l.Road,l.Village,l.City, l.District, l.State,l.Pin,e.GstinNumber,e.PanNumber
                }).FirstOrDefaultAsync();

            if (data == null)
                return null;

            return new UdyamDetailsModel
            {
                MsmeEnquiryID=data.MsmeEnquiryID,
                UdyamNumber = data.Uan,
                EnterpriseName = data.NameOfEnterprise,
                OrganizationType = data.OrganizationType,
                EnterpriseType = data.OrganizationType,
                DateOfIncorporation = data.DateOfIncorporation,
                Email = data.Email,
                Mobile = data.MobileNumber,
                gstin = data.GstinNumber,
                Pan = data.PanNumber,

                Address = string.Join(", ", new[]
                {data.Line1, data.Building, data.Road,data.Village, data.City, data.District, data.State,
                    data.Pin}.Where(x => !string.IsNullOrWhiteSpace(x))),
                District = data.District,
                State = data.State,
                PinCode = data.Pin
            };
        }

        public async Task<string> GetUdyamPayload(string uan)
        {
            var data = await (
                from e in _context.MsmeEnquiries
                join l in _context.MsmeLocations
                    on e.MsmeEnquiryID equals l.MsmeEnquiryID into loc
                from l in loc.DefaultIfEmpty()
                where e.Uan == uan
                select new
                {
                   e.Payload
                }).FirstOrDefaultAsync();

            if (data == null)
                return null;

            return data.Payload;
        }

        public async Task<ResultModel<UserRegistrationModel>> AddUpdateUserRegst(int? msmeEnquiryId, string email, string clientIp)
        {
            var result = new ResultModel<UserRegistrationModel>();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var enquiry = await _context.MsmeEnquiries
                    .FirstOrDefaultAsync(x => x.MsmeEnquiryID == msmeEnquiryId && x.Email == email);

                if (enquiry == null)
                {
                    result.Result = tflResultType.tflWarning;
                    result.Message = "MSME enquiry record not found.";
                    return result;
                }

                if (enquiry.IsRegister == true)
                {
                    result.Result = tflResultType.tflWarning;
                    result.Message = "This user is already registered.";
                    return result;
                }

                var dup = await _context.UserRegistration
                    .FirstOrDefaultAsync(x => x.Email == enquiry.Email || x.MobileNumber == enquiry.MobileNumber);
                if (dup != null)
                {
                    result.Result = tflResultType.tflWarning;
                    result.Message = dup.Email == enquiry.Email
                        ? "This email is already registered."
                        : "This mobile number is already registered.";
                    return result;
                }

                var userReg = new UserRegistrationModel
                {
                    MsmeEnquiryID = enquiry.MsmeEnquiryID,
                    Email = enquiry.Email,
                    MobileNumber = enquiry.MobileNumber,
                    UdyamNumber = enquiry.Uan,
                    GstinNumber = enquiry.GstinNumber,
                    PanNumber = enquiry.PanNumber,
                    IPAddress = clientIp,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "SYSTEM"
                };
                _context.UserRegistration.Add(userReg);
                await _context.SaveChangesAsync();

                var otpRec = await _context.UserOtpModel
                    .FirstOrDefaultAsync(x => x.MsmeEnquiryID == msmeEnquiryId && x.Email == email);
                if (otpRec != null)
                {
                    otpRec.UserRegistrationID = userReg.UserRegistrationID;
                    otpRec.UpdatedAt = DateTime.Now;
                    otpRec.UpdatedBy = "system";
                }

                enquiry.IsRegister = true;
                enquiry.UpdatedAt = DateTime.Now;
                enquiry.UpdatedBy = "system";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                result.Result = tflResultType.tflSuccess;
                result.Message = "User registered successfully.";
                result.Data = userReg;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                result.Data = null;
            }
            return result;
        }

        public async Task<ResultModel<UserOtpModel>> AddUpdateUserOtp(UserOtpModel entity)
        {
            var result = new ResultModel<UserOtpModel>();

            try
            {
                var existing = await _context.UserOtpModel.FirstOrDefaultAsync(x => x.Email == entity.Email ).ConfigureAwait(false);

                if (existing != null)
                {
                    existing.OTP = entity.OTP;
                    existing.UpdatedAt = DateTime.Now;
                    existing.UpdatedBy = "system";

                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    result.Result = tflResultType.tflSuccess;
                    result.Message = "OTP Updated Successfully";
                    result.Data = existing;
                }
                else
                {
                    UserOtpModel paObj = new UserOtpModel();

                    entity.MapToModelObject(paObj);

                    paObj.CreatedAt = DateTime.Now;
                    paObj.CreatedBy = "system";

                    _context.UserOtpModel.Add(paObj);

                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    entity.UserOtpID = paObj.UserOtpID;

                    result.Result = tflResultType.tflSuccess;
                    result.Message = "OTP Saved Successfully";
                    result.Data = paObj;
                }
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                result.Data = null;
            }

            return result;
        }
        public async Task<ResultModel<UserSessionModel>> FetchUserOTPDet(UserOtpModel model)
        {
            var result = new ResultModel<UserSessionModel>();

            try
            {
                var entity = await _context.UserOtpModel
                    .FirstOrDefaultAsync(x =>
                        x.Email == model.Email);

                if (entity == null)
                {
                    result.Result = tflResultType.tflNoRecordFound;
                    result.Message = "OTP record not found.";
                    result.Data = null;
                    return result;
                }

                // UpdatedAt is stamped UtcNow by ApplicationDbContext.SetAuditableFields() on every save — compare in UTC.
                if (DateTime.UtcNow - entity.UpdatedAt > TimeSpan.FromMinutes(5))   // matches the 5-min email expiry
                {
                    result.Result = tflResultType.tflError;
                    result.Message = "OTP has expired. Please resend a new OTP.";
                    result.Data = null;
                    return result;
                }

                var decryptedOtp = _encryption.DecryptString(entity.OTP);

                if (decryptedOtp != model.OTP)
                {
                    result.Result = tflResultType.tflError;
                    result.Message = "Invalid OTP.";
                    result.Data = null;
                    return result;
                }

                // OTP is valid — pull the registration record for the fields we
                // want to keep in session (Udyam / GSTIN / PAN / Mobile).
                var registration = await _context.UserRegistration
                    .FirstOrDefaultAsync(x => x.Email == entity.Email);

                var enquiryId = registration?.MsmeEnquiryID ?? entity.MsmeEnquiryID;

                // Enterprise name for the dashboard welcome greeting.
                var enterpriseName = enquiryId.HasValue
                    ? await _context.MsmeEnquiries
                        .Where(x => x.MsmeEnquiryID == enquiryId.Value)
                        .Select(x => x.NameOfEnterprise)
                        .FirstOrDefaultAsync()
                    : null;

                result.Result = tflResultType.tflSuccess;
                result.Message = "OTP validated successfully.";
                result.Data = new UserSessionModel
                {
                    UserRegistrationID = registration?.UserRegistrationID ?? 0,
                    MsmeEnquiryID = enquiryId,
                    NameOfEnterprise = enterpriseName,
                    Email = entity.Email,
                    MobileNumber = registration?.MobileNumber ?? entity.MobileNumber,
                    UdyamNumber = registration?.UdyamNumber,
                    GstinNumber = registration?.GstinNumber,
                    PanNumber = registration?.PanNumber
                };
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                result.Data = null;
            }

            return result;
        }
        public async Task<bool> IsUdyamRegistered(string uan)
        {
            return await _context.MsmeEnquiries
                .AnyAsync(x => x.Uan == uan);
        }

        public async Task<ResultModel<UserOtpModel>> ValidateCustomerEmail(string email)
        {
            var result = new ResultModel<UserOtpModel>();

            try
            {
                var user = await _context.UserRegistration
                    .FirstOrDefaultAsync(x => x.Email == email);

                if (user == null)
                {
                    result.Result = tflResultType.tflNoRecordFound;
                    result.Message = "Email is not registered.";
                    return result;
                }

                var otp = await _context.UserOtpModel
                    .FirstOrDefaultAsync(x => x.Email == email);

                if (otp == null)
                {
                    result.Result = tflResultType.tflNoRecordFound;
                    result.Message = "OTP record not found.";
                    return result;
                }

                var enquiry = await _context.MsmeEnquiries
                    .FirstOrDefaultAsync(x => x.MsmeEnquiryID == user.MsmeEnquiryID);

                otp.MsmeEnquiryID = user.MsmeEnquiryID;
                otp.UserRegistrationID = user.UserRegistrationID;
                otp.MsmeEnquiryID = user.MsmeEnquiryID;
                otp.MobileNumber = user.MobileNumber;
                otp.Email = user.Email;
                otp.UserOtpID = otp.UserOtpID;

                result.Result = tflResultType.tflSuccess;
                result.Message = enquiry?.NameOfEnterprise ?? "";
                result.Data = otp;
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
            }

            return result;
        }


    }
}
