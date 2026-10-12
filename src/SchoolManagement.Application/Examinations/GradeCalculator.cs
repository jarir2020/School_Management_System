namespace SchoolManagement.Application.Examinations;

/// <summary>Applies the academic year's configured percentage thresholds to one subject result.</summary>
public static class GradeCalculator
{
    public static CalculatedGrade Calculate(
        decimal marksObtained,
        decimal fullMarks,
        decimal passMarks,
        IReadOnlyList<GradeBandSummary> gradeBands)
    {
        if (fullMarks <= 0 || marksObtained < 0 || marksObtained > fullMarks)
        {
            throw new ArgumentOutOfRangeException(nameof(marksObtained), "Marks must be between zero and full marks.");
        }

        var percentage = Math.Round(marksObtained * 100m / fullMarks, 2);
        var band = gradeBands
            .Where(item => item.MinimumPercentage <= percentage)
            .OrderByDescending(item => item.MinimumPercentage)
            .FirstOrDefault();
        if (band is null)
        {
            return new CalculatedGrade("—", 0, false, percentage, "No matching grade band");
        }

        return new CalculatedGrade(
            band.LetterGrade,
            band.GradePoint,
            band.IsPassing && marksObtained >= passMarks,
            percentage,
            band.Remark);
    }
}
