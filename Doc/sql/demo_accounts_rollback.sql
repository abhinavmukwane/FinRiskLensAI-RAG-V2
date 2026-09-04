/* =====================================================================
   Demo accounts — remove everything the seeding added to UAT.

   Deletes only the three demo UANs, in FK-safe order. Blob folders are
   NOT touched: delete them separately in Azure Storage Explorer
   (container msme-data -> the three UDYAM-... folders).
   ===================================================================== */

BEGIN TRANSACTION;

DECLARE @uans TABLE (Uan nvarchar(100) PRIMARY KEY);
INSERT INTO @uans VALUES
    ('UDYAM-MH-20-0091447'), ('UDYAM-GJ-01-0044219'), ('UDYAM-UP-28-0007733');

DELETE FROM FinRiskLensAI.t_UserOtp
 WHERE UserRegistrationID IN (
   SELECT UserRegistrationID FROM FinRiskLensAI.t_UserRegistration
    WHERE UdyamNumber IN (SELECT Uan FROM @uans));

DELETE FROM FinRiskLensAI.t_UserRegistration
 WHERE UdyamNumber IN (SELECT Uan FROM @uans);

DELETE FROM FinRiskLensAI.t_MsmeScoreSummary
 WHERE Uan IN (SELECT Uan FROM @uans);

DELETE FROM FinRiskLensAI.t_MsmeEnquiry
 WHERE Uan IN (SELECT Uan FROM @uans);

SELECT (SELECT COUNT(*) FROM FinRiskLensAI.t_MsmeEnquiry       WHERE Uan IN (SELECT Uan FROM @uans)) AS EnquiriesLeft,
       (SELECT COUNT(*) FROM FinRiskLensAI.t_UserRegistration  WHERE UdyamNumber IN (SELECT Uan FROM @uans)) AS RegistrationsLeft,
       (SELECT COUNT(*) FROM FinRiskLensAI.t_MsmeScoreSummary  WHERE Uan IN (SELECT Uan FROM @uans)) AS SummariesLeft;

COMMIT TRANSACTION;   -- all three counts should read 0
GO
