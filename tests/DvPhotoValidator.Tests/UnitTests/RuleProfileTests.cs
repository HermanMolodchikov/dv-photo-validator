using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Tests.UnitTests;

public class RuleProfileTests
{
    [Fact]
    public void Baseline_IsValid_AndMatchesSpecification()
    {
        var p = RuleProfile.Baseline;

        Assert.Empty(p.Validate());
        Assert.Equal(600, p.ExportSize);
        Assert.Equal(245_760, p.MaxFileBytes);
        Assert.Equal(0.50, p.HeadMinRatio);
        Assert.Equal(0.69, p.HeadMaxRatio);
    }

    [Fact]
    public void ShippedJsonProfile_LoadsAndEqualsBaselineValues()
    {
        var result = RuleProfileLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Rules"));

        Assert.Empty(result.Errors);
        var profile = Assert.Single(result.Profiles);
        Assert.Equal("dv-baseline", profile.Id);
        Assert.Equal(RuleProfile.Baseline.MaxFileBytes, profile.MaxFileBytes);
        Assert.Equal(RuleProfile.Baseline.EyeMinRatio, profile.EyeMinRatio);
        Assert.Equal(3, profile.Sources.Count);
    }

    [Fact]
    public void InvalidProfile_IsRejectedWithReason()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            RuleProfileLoader.Parse("""{ "id": "bad", "headMinRatio": 0.8, "headMaxRatio": 0.5 }"""));

        Assert.Contains("высоты головы", ex.Message);
    }

    [Fact]
    public void BrokenFile_IsReported_AndBaselineIsUsed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dvpv-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not json");

            var result = RuleProfileLoader.LoadDirectory(dir);

            Assert.Single(result.Errors);
            Assert.Same(RuleProfile.Baseline, Assert.Single(result.Profiles));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
