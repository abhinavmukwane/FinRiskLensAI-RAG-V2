SET NOCOUNT ON;
BEGIN TRAN;

-- UDYAM-MH-20-0091447
UPDATE FinRiskLensAI.t_MsmeEnquiry SET
    OrganizationType=N'Partnership',
    MajorActivity=N'Manufacturing',
    GstinNumber=N'27AABFS4417K1Z3',
    PanNumber=N'AABFS4417K',
    Flat=N'Plot 14', NameOfBuilding=N'Chinchwad MIDC Industrial Area', Road=N'Telco Road',
    City=N'Pune', Pin=N'411019',
    Payload=N'{
 "uan": "UDYAM-MH-20-0091447",
 "main_details": {
  "name_of_enterprise": "SHREEJI PRECISION COMPONENTS",
  "organization_type": "Partnership",
  "date_of_incorporation": "2015-09-01",
  "date_of_commencement": "2015-09-01",
  "registration_date": "2015-11-01",
  "major_activity": "Manufacturing",
  "social_category": "General",
  "gender": "Male",
  "flat": "Plot 14",
  "name_of_building": "Chinchwad MIDC Industrial Area",
  "road": "Telco Road",
  "city": "Pune",
  "state": "Maharashtra",
  "pin": "411019",
  "gstin": "27AABFS4417K1Z3",
  "Pan": "AABFS4417K",
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
   "unit_name": "SHREEJI PRECISION COMPONENTS Unit 1",
   "city": "Pune",
   "district": "Pune",
   "state": "Maharashtra",
   "pin": "411019"
  },
  {
   "unit_name": "SHREEJI PRECISION COMPONENTS Unit 2",
   "city": "Pune",
   "district": "Pune",
   "state": "Maharashtra",
   "pin": "411019"
  },
  {
   "unit_name": "SHREEJI PRECISION COMPONENTS Unit 3",
   "city": "Pune",
   "district": "Pune",
   "state": "Maharashtra",
   "pin": "411019"
  }
 ],
 "nic_code": [
  {
   "nic_2_digit": "25-Manufacture of fabricated metal products",
   "nic_4_digit": "2510",
   "nic_5_digit": "25100",
   "activity_type": "Manufacturing"
  },
  {
   "nic_2_digit": "25-Manufacture of fabricated metal products",
   "nic_4_digit": "2511",
   "nic_5_digit": "25110",
   "activity_type": "Manufacturing"
  },
  {
   "nic_2_digit": "25-Manufacture of fabricated metal products",
   "nic_4_digit": "2512",
   "nic_5_digit": "25120",
   "activity_type": "Manufacturing"
  },
  {
   "nic_2_digit": "25-Manufacture of fabricated metal products",
   "nic_4_digit": "2513",
   "nic_5_digit": "25130",
   "activity_type": "Manufacturing"
  }
 ]
}',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE Uan=N'UDYAM-MH-20-0091447';

UPDATE FinRiskLensAI.t_UserRegistration SET
    GstinNumber=N'27AABFS4417K1Z3', PanNumber=N'AABFS4417K',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE UdyamNumber=N'UDYAM-MH-20-0091447';

-- UDYAM-GJ-01-0044219
UPDATE FinRiskLensAI.t_MsmeEnquiry SET
    OrganizationType=N'Partnership',
    MajorActivity=N'Trading',
    GstinNumber=N'24AAGFR2210M1ZE',
    PanNumber=N'AAGFR2210M',
    Flat=N'Plot 27', NameOfBuilding=N'GIDC Industrial Estate', Road=N'Pandesara Road',
    City=N'Surat', Pin=N'394221',
    Payload=N'{
 "uan": "UDYAM-GJ-01-0044219",
 "main_details": {
  "name_of_enterprise": "RATNADEEP TEXTILE TRADERS",
  "organization_type": "Partnership",
  "date_of_incorporation": "2021-01-01",
  "date_of_commencement": "2021-01-01",
  "registration_date": "2021-03-01",
  "major_activity": "Trading",
  "social_category": "General",
  "gender": "Male",
  "flat": "Plot 27",
  "name_of_building": "GIDC Industrial Estate",
  "road": "Pandesara Road",
  "city": "Surat",
  "state": "Gujarat",
  "pin": "394221",
  "gstin": "24AAGFR2210M1ZE",
  "Pan": "AAGFR2210M",
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
   "unit_name": "RATNADEEP TEXTILE TRADERS Unit 1",
   "city": "Surat",
   "district": "Surat",
   "state": "Gujarat",
   "pin": "394221"
  },
  {
   "unit_name": "RATNADEEP TEXTILE TRADERS Unit 2",
   "city": "Surat",
   "district": "Surat",
   "state": "Gujarat",
   "pin": "394221"
  }
 ],
 "nic_code": [
  {
   "nic_2_digit": "46-Wholesale trade except of motor vehicles",
   "nic_4_digit": "4610",
   "nic_5_digit": "46100",
   "activity_type": "Trading"
  },
  {
   "nic_2_digit": "46-Wholesale trade except of motor vehicles",
   "nic_4_digit": "4611",
   "nic_5_digit": "46110",
   "activity_type": "Trading"
  },
  {
   "nic_2_digit": "46-Wholesale trade except of motor vehicles",
   "nic_4_digit": "4612",
   "nic_5_digit": "46120",
   "activity_type": "Trading"
  }
 ]
}',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE Uan=N'UDYAM-GJ-01-0044219';

UPDATE FinRiskLensAI.t_UserRegistration SET
    GstinNumber=N'24AAGFR2210M1ZE', PanNumber=N'AAGFR2210M',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE UdyamNumber=N'UDYAM-GJ-01-0044219';

-- UDYAM-UP-28-0007733
UPDATE FinRiskLensAI.t_MsmeEnquiry SET
    OrganizationType=N'Proprietary',
    MajorActivity=N'Trading',
    GstinNumber=N'09AFQPN8123L1ZR',
    PanNumber=N'AFQPN8123L',
    Flat=N'Shop 8', NameOfBuilding=N'UPSIDA Industrial Area', Road=N'Panki Site V',
    City=N'Kanpur', Pin=N'208020',
    Payload=N'{
 "uan": "UDYAM-UP-28-0007733",
 "main_details": {
  "name_of_enterprise": "NAVDEEP AUTO SPARES",
  "organization_type": "Proprietary",
  "date_of_incorporation": "2025-08-01",
  "date_of_commencement": "2025-08-01",
  "registration_date": "2025-10-01",
  "major_activity": "Trading",
  "social_category": "General",
  "gender": "Male",
  "flat": "Shop 8",
  "name_of_building": "UPSIDA Industrial Area",
  "road": "Panki Site V",
  "city": "Kanpur",
  "state": "Uttar Pradesh",
  "pin": "208020",
  "gstin": "09AFQPN8123L1ZR",
  "Pan": "AFQPN8123L",
  "enterprise_type_list": [
   {
    "classification_year": "2026",
    "enterprise_type": "Micro",
    "classification_date": "2026-04-01"
   }
  ]
 },
 "location_of_plant_details": [
  {
   "unit_name": "NAVDEEP AUTO SPARES Unit 1",
   "city": "Kanpur",
   "district": "Kanpur",
   "state": "Uttar Pradesh",
   "pin": "208020"
  }
 ],
 "nic_code": [
  {
   "nic_2_digit": "45-Wholesale and retail trade and repair of motor vehicles",
   "nic_4_digit": "4510",
   "nic_5_digit": "45100",
   "activity_type": "Trading"
  }
 ]
}',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE Uan=N'UDYAM-UP-28-0007733';

UPDATE FinRiskLensAI.t_UserRegistration SET
    GstinNumber=N'09AFQPN8123L1ZR', PanNumber=N'AFQPN8123L',
    UpdatedAt=SYSUTCDATETIME(), UpdatedBy=N'demo-data-fix'
WHERE UdyamNumber=N'UDYAM-UP-28-0007733';

COMMIT;
SELECT Uan,OrganizationType,MajorActivity,GstinNumber,PanNumber,NameOfBuilding,City,Pin FROM FinRiskLensAI.t_MsmeEnquiry WHERE Uan IN (N'UDYAM-MH-20-0091447',N'UDYAM-GJ-01-0044219',N'UDYAM-UP-28-0007733');
SELECT UserRegistrationID,UdyamNumber,GstinNumber,PanNumber FROM FinRiskLensAI.t_UserRegistration WHERE UdyamNumber IN (N'UDYAM-MH-20-0091447',N'UDYAM-GJ-01-0044219',N'UDYAM-UP-28-0007733');
