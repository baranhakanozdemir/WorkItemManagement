using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

public sealed class RequirementNumberAllocatorTests
{
    [Theory]
    [InlineData("R-001", 1)]
    [InlineData("r-042", 42)]
    [InlineData("R-150", 150)]
    public void TryParseSequence_parses_valid_numbers(string number, int expected)
    {
        Assert.True(RequirementNumberAllocator.TryParseSequence(number, out var sequence));
        Assert.Equal(expected, sequence);
    }

    [Theory]
    [InlineData("")]
    [InlineData("REQ-001")]
    [InlineData("R-")]
    [InlineData("R-abc")]
    public void TryParseSequence_rejects_invalid_numbers(string number)
    {
        Assert.False(RequirementNumberAllocator.TryParseSequence(number, out _));
    }

    [Fact]
    public void Format_produces_three_digit_suffix()
    {
        Assert.Equal("R-007", RequirementNumberAllocator.Format(7));
    }
}
