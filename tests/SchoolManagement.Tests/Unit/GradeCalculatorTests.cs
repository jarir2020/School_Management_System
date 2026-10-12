using SchoolManagement.Application.Examinations;

namespace SchoolManagement.Tests.Unit;

public sealed class GradeCalculatorTests
{
    private static readonly GradeBandSummary[] GradeBands =
    [
        new(1, 1, "2026", 0, "F", 0, false, "Fail"),
        new(2, 1, "2026", 40, "D", 1, true, "Pass"),
        new(3, 1, "2026", 80, "A", 5, true, "Excellent")
    ];

    [Fact]
    public void CalculateUsesTheHighestMatchingThreshold()
    {
        var grade = GradeCalculator.Calculate(80m, 100m, 40m, GradeBands);

        Assert.Equal("A", grade.LetterGrade);
        Assert.Equal(80m, grade.Percentage);
        Assert.True(grade.IsPassing);
    }

    [Fact]
    public void CalculateRoundsPercentageBeforeSelectingTheGradeBand()
    {
        var grade = GradeCalculator.Calculate(79.995m, 100m, 40m, GradeBands);

        Assert.Equal("A", grade.LetterGrade);
        Assert.Equal(80m, grade.Percentage);
    }

    [Fact]
    public void GradeBandPassingStatusIsAlsoLimitedByTheSubjectPassMark()
    {
        var grade = GradeCalculator.Calculate(39m, 100m, 40m, GradeBands);

        Assert.Equal("F", grade.LetterGrade);
        Assert.False(grade.IsPassing);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(101, 100)]
    [InlineData(0, 0)]
    public void CalculateRejectsInvalidMarkRanges(decimal marks, decimal fullMarks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GradeCalculator.Calculate(marks, fullMarks, 40m, GradeBands));
    }
}
