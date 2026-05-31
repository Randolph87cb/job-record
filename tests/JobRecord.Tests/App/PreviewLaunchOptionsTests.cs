using FluentAssertions;
using JobRecord.App.Preview;
using JobRecord.Core.Enums;

namespace JobRecord.Tests.App;

public sealed class PreviewLaunchOptionsTests
{
    [Fact]
    public void Parse_ShouldReturnDisabled_WhenPreviewArgumentIsMissing()
    {
        var options = PreviewLaunchOptions.Parse(["--dock=left"]);

        options.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_ShouldMapPreviewArguments()
    {
        var options = PreviewLaunchOptions.Parse([
            "--preview",
            "--dock=right",
            "--expanded=true",
            "--compact=true",
            "--state=paused",
            "--screenshot-path=C:\\temp\\right.png"
        ]);

        options.IsEnabled.Should().BeTrue();
        options.DockMode.Should().Be(DockMode.RightEdge);
        options.IsExpanded.Should().BeTrue();
        options.IsCompact.Should().BeTrue();
        options.WorkState.Should().Be(PreviewWorkState.Paused);
        options.ShouldCaptureScreenshot.Should().BeTrue();
        options.ScreenshotPath.Should().Be("C:\\temp\\right.png");
        options.GetDatabasePath().Should().Contain("JobRecord");
        options.GetDatabasePath().Should().EndWith(".db");
    }
}
