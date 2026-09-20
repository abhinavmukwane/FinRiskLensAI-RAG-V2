SET NOCOUNT ON;
BEGIN TRAN;

IF NOT EXISTS (SELECT 1 FROM FinRiskLensAI.t_MsmeEnquiry WHERE Uan=N'UDYAM-MH-25-0042817')
INSERT INTO FinRiskLensAI.t_MsmeEnquiry
 (ClientId,Uan,CertificateUrl,NameOfEnterprise,MajorActivity,SocialCategory,DateOfCommencement,
  DicName,State,AppliedDate,Flat,NameOfBuilding,Road,Village,Block,City,Pin,MobileNumber,Email,
  OrganizationType,Gender,DateOfIncorporation,MsmeDfo,RegistrationDate,Payload,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy,GstinNumber,PanNumber,IsRegister)
VALUES
 (N'FRL-DEMO-042817',N'UDYAM-MH-25-0042817',N'https://udyamregistration.gov.in/certificate/UDYAM-MH-25-0042817.pdf',
  N'SANKALP AUTOTECH PRIVATE LIMITED',N'Manufacturing',N'General','2014-06-18',
  N'Nashik',N'Maharashtra','2014-08-04',N'Plot 42',N'MIDC Satpur Industrial Area',N'Trimbak Road',
  N'Satpur',N'Block B',N'Nashik',N'422007',N'9823714402',
  N'sankalp.autotech@finrisklens.demo',N'Private Limited Company',N'Male',
  '2014-06-18',N'Maharashtra','2014-08-04',N'{
 "uan": "UDYAM-MH-25-0042817",
 "main_details": {
  "name_of_enterprise": "SANKALP AUTOTECH PRIVATE LIMITED",
  "organization_type": "Private Limited Company",
  "date_of_incorporation": "2014-06-18",
  "date_of_commencement": "2014-06-18",
  "registration_date": "2014-08-04",
  "major_activity": "Manufacturing",
  "social_category": "General",
  "gender": "Male",
  "flat": "Plot 42",
  "name_of_building": "MIDC Satpur Industrial Area",
  "road": "Trimbak Road",
  "city": "Nashik",
  "state": "Maharashtra",
  "pin": "422007",
  "gstin": "27AAJCS7412K1Z0",
  "Pan": "AAJCS7412K",
  "enterprise_type_list": [
   {
    "classification_year": "2024",
    "enterprise_type": "Small",
    "classification_date": "2024-04-01"
   },
   {
    "classification_year": "2026",
    "enterprise_type": "Small",
    "classification_date": "2026-04-01"
   }
  ]
 },
 "location_of_plant_details": [
  {
   "unit_name": "SANKALP AUTOTECH PRIVATE LIMITED Unit 1",
   "city": "Nashik",
   "district": "Nashik",
   "state": "Maharashtra",
   "pin": "422007"
  },
  {
   "unit_name": "SANKALP AUTOTECH PRIVATE LIMITED Unit 2",
   "city": "Nashik",
   "district": "Nashik",
   "state": "Maharashtra",
   "pin": "422007"
  }
 ],
 "nic_code": [
  {
   "nic_2_digit": "29-Manufacture of motor vehicles, trailers and semi-trailers",
   "nic_4_digit": "2930",
   "nic_5_digit": "29301",
   "activity_type": "Manufacturing"
  },
  {
   "nic_2_digit": "29-Manufacture of motor vehicles, trailers and semi-trailers",
   "nic_4_digit": "2930",
   "nic_5_digit": "29302",
   "activity_type": "Manufacturing"
  },
  {
   "nic_2_digit": "25-Manufacture of fabricated metal products",
   "nic_4_digit": "2599",
   "nic_5_digit": "25999",
   "activity_type": "Manufacturing"
  }
 ]
}',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'demo-seed',N'demo-seed',N'27AAJCS7412K1Z0',N'AAJCS7412K',1);

DECLARE @eid INT = (SELECT MsmeEnquiryID FROM FinRiskLensAI.t_MsmeEnquiry WHERE Uan=N'UDYAM-MH-25-0042817');

IF NOT EXISTS (SELECT 1 FROM FinRiskLensAI.t_UserRegistration WHERE UdyamNumber=N'UDYAM-MH-25-0042817')
INSERT INTO FinRiskLensAI.t_UserRegistration
 (MobileNumber,Email,UdyamNumber,GstinNumber,PanNumber,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy,MsmeEnquiryID,IPAddress)
VALUES (N'9823714402',N'sankalp.autotech@finrisklens.demo',N'UDYAM-MH-25-0042817',N'27AAJCS7412K1Z0',N'AAJCS7412K',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'demo-seed',N'demo-seed',@eid,N'127.0.0.1');

DECLARE @rid INT = (SELECT UserRegistrationID FROM FinRiskLensAI.t_UserRegistration WHERE UdyamNumber=N'UDYAM-MH-25-0042817');

-- placeholder ciphertext; overwritten by the first real Generate OTP
IF NOT EXISTS (SELECT 1 FROM FinRiskLensAI.t_UserOtp WHERE Email=N'sankalp.autotech@finrisklens.demo')
INSERT INTO FinRiskLensAI.t_UserOtp
 (UserRegistrationID,MsmeEnquiryID,MobileNumber,Email,OTP,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (@rid,@eid,N'9823714402',N'sankalp.autotech@finrisklens.demo',N'7hPylYBjnAGlp9zDjmskmQ==',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'demo-seed',N'demo-seed');

COMMIT;

SELECT e.MsmeEnquiryID,e.Uan,e.NameOfEnterprise,e.OrganizationType,e.GstinNumber,r.UserRegistrationID,r.Email,
       o.UserOtpID,s.OverallScore,s.ScoreBand
FROM FinRiskLensAI.t_MsmeEnquiry e
LEFT JOIN FinRiskLensAI.t_UserRegistration r ON r.UdyamNumber=e.Uan
LEFT JOIN FinRiskLensAI.t_UserOtp o ON o.UserRegistrationID=r.UserRegistrationID
LEFT JOIN FinRiskLensAI.t_MsmeScoreSummary s ON s.Uan=e.Uan
WHERE e.Uan=N'UDYAM-MH-25-0042817';
