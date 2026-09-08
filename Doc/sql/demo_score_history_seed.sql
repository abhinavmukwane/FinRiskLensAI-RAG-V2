SET NOCOUNT ON;
BEGIN TRAN;
DELETE FROM FinRiskLensAI.t_MsmeScoreHistory WHERE Source = N'seed';
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',762.0,N'Good',806.3,695.3,0.195,
  174.5,158.4,133.9,
  138.2,127.7,73.5,
  67586708.26,1061843.67,52580811.81,
  0,0,0,N'frl-scoring-v1.0',
  '2026-02-06T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',764.8,N'Good',809.3,697.8,0.1957,
  175.2,159.0,134.4,
  138.7,128.2,73.8,
  67837282.24,1065780.4,52775752.26,
  0,0,0,N'frl-scoring-v1.0',
  '2026-03-08T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',772.1,N'Good',817.0,704.5,0.1975,
  176.8,160.5,135.7,
  140.0,129.4,74.5,
  68483499.36,1075933.01,53278493.43,
  0,0,0,N'frl-scoring-v1.0',
  '2026-04-07T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',782.1,N'Good',827.6,713.6,0.2001,
  179.1,162.6,137.5,
  141.8,131.1,75.4,
  69367102.36,1089815.15,53965915.03,
  0,0,0,N'frl-scoring-v1.0',
  '2026-05-07T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',792.9,N'Good',839.1,723.5,0.2029,
  181.6,164.8,139.4,
  143.8,132.9,76.5,
  70329833.99,1104940.47,54714896.78,
  0,0,0,N'frl-scoring-v1.0',
  '2026-06-06T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',802.9,N'Excellent',849.6,732.6,0.2054,
  183.9,166.9,141.1,
  145.6,134.6,77.4,
  71213436.99,1118822.61,55402318.38,
  0,0,0,N'frl-scoring-v1.0',
  '2026-07-06T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',810.2,N'Excellent',857.3,739.2,0.2073,
  185.6,168.4,142.4,
  146.9,135.8,78.1,
  71859654.11,1128975.22,55905059.55,
  0,0,0,N'frl-scoring-v1.0',
  '2026-08-05T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-20-0091447',813.0,N'Excellent',860.3,741.8,0.208,
  186.2,169.0,142.9,
  147.4,136.3,78.4,
  72110228.10000001,1132911.9508333318,56100000.0,
  0,0,0,N'frl-scoring-v1.0',
  '2026-09-04T09:57:15',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',741.0,N'Good',781.0,682.0,0.1912,
  171.2,146.3,131.4,
  135.5,124.5,72.1,
  78227966.81,1229026.73,60870483.87,
  0,0,0,N'frl-scoring-v1.0',
  '2026-02-10T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',744.6,N'Good',784.8,685.3,0.1922,
  172.0,147.0,132.0,
  136.2,125.1,72.4,
  78608083.27,1234998.68,61166258.82,
  0,0,0,N'frl-scoring-v1.0',
  '2026-03-12T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',753.9,N'Good',794.6,693.8,0.1946,
  174.2,148.8,133.7,
  137.9,126.6,73.3,
  79588383.6,1250400.02,61929046.84,
  0,0,0,N'frl-scoring-v1.0',
  '2026-04-11T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',766.6,N'Good',808.0,705.5,0.1978,
  177.1,151.3,135.9,
  140.2,128.8,74.6,
  80928794.26,1271458.99,62972042.7,
  0,0,0,N'frl-scoring-v1.0',
  '2026-05-11T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',780.4,N'Good',822.5,718.3,0.2014,
  180.3,154.1,138.4,
  142.7,131.1,75.9,
  82389241.7,1294403.84,64108441.17,
  0,0,0,N'frl-scoring-v1.0',
  '2026-06-10T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',793.1,N'Good',835.9,729.9,0.2047,
  183.2,156.6,140.6,
  145.0,133.2,77.1,
  83729652.37,1315462.81,65151437.04,
  0,0,0,N'frl-scoring-v1.0',
  '2026-07-10T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',802.4,N'Excellent',845.7,738.5,0.2071,
  185.4,158.4,142.3,
  146.7,134.8,78.0,
  84709952.7,1330864.15,65914225.05,
  0,0,0,N'frl-scoring-v1.0',
  '2026-08-09T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-MH-25-0042817',806.0,N'Excellent',849.5,741.8,0.208,
  186.2,159.1,142.9,
  147.4,135.4,78.4,
  85090069.16,1336836.0949999997,66210000.0,
  0,0,0,N'frl-scoring-v1.0',
  '2026-09-08T04:15:43',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',692.0,N'Good',701.3,677.5,0.0287,
  161.1,101.5,129.0,
  118.5,111.7,79.4,
  15865881.21,116017.48,4469856.46,
  0,0,0,N'frl-scoring-v1.0',
  '2026-02-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',688.4,N'Good',697.6,674.0,0.0285,
  160.3,101.0,128.3,
  117.9,111.1,78.9,
  15783328.58,115413.82,4446599.1,
  0,0,0,N'frl-scoring-v1.0',
  '2026-03-08T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',679.1,N'Good',688.2,664.9,0.0282,
  158.1,99.6,126.6,
  116.3,109.6,77.9,
  15570429.68,113857.02,4386619.61,
  0,0,0,N'frl-scoring-v1.0',
  '2026-04-07T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',666.4,N'Good',675.3,652.5,0.0276,
  155.2,97.8,124.2,
  114.2,107.6,76.4,
  15279323.02,111728.34,4304606.83,
  0,0,0,N'frl-scoring-v1.0',
  '2026-05-07T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',652.6,N'Good',661.3,638.9,0.0271,
  152.0,95.8,121.7,
  111.8,105.3,74.8,
  14962147.1,109409.02,4215249.63,
  0,0,0,N'frl-scoring-v1.0',
  '2026-06-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',639.9,N'Fair',648.5,626.5,0.0265,
  149.0,93.9,119.3,
  109.6,103.3,73.4,
  14671040.44,107280.34,4133236.85,
  0,0,0,N'frl-scoring-v1.0',
  '2026-07-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',630.6,N'Fair',639.0,617.4,0.0261,
  146.8,92.5,117.6,
  108.0,101.8,72.3,
  14458141.54,105723.54,4073257.35,
  0,0,0,N'frl-scoring-v1.0',
  '2026-08-05T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-GJ-01-0044219',627.0,N'Fair',635.4,613.9,0.026,
  146.0,92.0,116.9,
  107.4,101.2,71.9,
  14375588.904000001,105119.88272727304,4050000.0,
  0,0,0,N'frl-scoring-v1.0',
  '2026-09-04T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',549.0,N'Fair',493.4,630.9,-0.5578,
  180.3,0.0,118.1,
  82.1,62.2,50.7,
  6840971.6,-721867.36,481835.11,
  0,0,0,N'frl-scoring-v1.0',
  '2026-02-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',539.4,N'Fair',484.8,619.9,-0.548,
  177.2,0.0,116.1,
  80.6,61.1,49.8,
  6721558.76,-709266.78,473424.42,
  0,0,0,N'frl-scoring-v1.0',
  '2026-03-08T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',514.7,N'Fair',462.5,591.5,-0.5229,
  169.1,0.0,110.7,
  76.9,58.3,47.5,
  6413599.33,-676770.53,451733.69,
  0,0,0,N'frl-scoring-v1.0',
  '2026-04-07T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',480.9,N'AtRisk',432.2,552.7,-0.4886,
  158.0,0.0,103.5,
  71.9,54.5,44.4,
  5992511.94,-632336.9,422074.93,
  0,0,0,N'frl-scoring-v1.0',
  '2026-05-07T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',444.1,N'AtRisk',399.1,510.3,-0.4512,
  145.9,0.0,95.6,
  66.4,50.3,41.0,
  5533715.24,-583924.13,389760.17,
  0,0,0,N'frl-scoring-v1.0',
  '2026-06-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',410.3,N'AtRisk',368.7,471.5,-0.4168,
  134.8,0.0,88.3,
  61.3,46.5,37.9,
  5112627.86,-539490.5,360101.42,
  0,0,0,N'frl-scoring-v1.0',
  '2026-07-06T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',385.6,N'AtRisk',346.5,443.1,-0.3917,
  126.6,0.0,83.0,
  57.6,43.7,35.6,
  4804668.43,-506994.26,338410.69,
  0,0,0,N'frl-scoring-v1.0',
  '2026-08-05T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
INSERT INTO FinRiskLensAI.t_MsmeScoreHistory
 (Uan,OverallScore,ScoreBand,HeuristicScore,MlCalibratedScore,CashflowTrendSlope,
  RevenueVitality,CashFlowHealth,TransactionTrust,ComplianceQuotient,BusinessStability,DebtServiceability,
  AnnualTurnover,MonthlySurplus,TotalIndicativeEligibility,
  IsAnomalous,DimensionsExcludedCount,DataFilesUsed,ModelVersion,ComputedAt,Source,
  CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
VALUES (N'UDYAM-UP-28-0007733',376.0,N'AtRisk',337.9,432.1,-0.382,
  123.5,0.0,80.9,
  56.2,42.6,34.7,
  4685255.591999999,-494393.67374999996,330000.0,
  1,0,0,N'frl-scoring-v1.0',
  '2026-09-04T09:57:16',N'seed',
  SYSUTCDATETIME(),SYSUTCDATETIME(),N'seed',N'seed');
COMMIT;
SELECT Uan, COUNT(*) AS Points, MIN(OverallScore) AS Low, MAX(OverallScore) AS High
FROM FinRiskLensAI.t_MsmeScoreHistory GROUP BY Uan ORDER BY Uan;