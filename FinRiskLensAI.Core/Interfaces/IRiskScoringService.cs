using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Core.Interfaces
{
    public interface IRiskScoringService
    {
        RiskAnalysisResult Analyze(SourceFacts facts);
        RiskAnalysisResult Analyze(MsmeFeatureSet features, SourceFacts? facts = null);
    }
}
