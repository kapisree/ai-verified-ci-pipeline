using TaskApi.Validation;
using Xunit;

namespace TaskApi.Tests;

public class TaskValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTitle_ReturnsError_WhenTitleMissingOrBlank(string? title)
    {
        var error = TaskValidator.ValidateTitle(title);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsError_WhenTitleExceeds200Characters()
    {
        var longTitle = new string('a', 201);

        var error = TaskValidator.ValidateTitle(longTitle);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsNull_WhenTitleIsValid()
    {
        var error = TaskValidator.ValidateTitle("Buy milk");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsNull_WhenTitleIsExactly200Characters()
    {
        var title = new string('a', 200);

        var error = TaskValidator.ValidateTitle(title);

        Assert.Null(error);
    }
}
