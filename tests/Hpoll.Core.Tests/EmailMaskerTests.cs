using Hpoll.Core.Utilities;

namespace Hpoll.Core.Tests;

public class EmailMaskerTests
{
    [Theory]
    [InlineData("user@example.com", "us**@example.com")]
    [InlineData("ab@example.com", "ab@example.com")]
    [InlineData("a@example.com", "a@example.com")]
    [InlineData("longname@test.org", "lo******@test.org")]
    public void Mask_NormalEmail_MasksLocalPart(string input, string expected)
    {
        Assert.Equal(expected, EmailMasker.Mask(input));
    }

    [Theory]
    [InlineData("@example.com")]
    [InlineData("noatsign")]
    public void Mask_NoLocalPart_ReturnsTripleAsterisk(string input)
    {
        Assert.Equal("***", EmailMasker.Mask(input));
    }

    [Fact]
    public void MaskList_NullInput_ReturnsNull()
    {
        Assert.Null(EmailMasker.MaskList(null!));
    }

    [Fact]
    public void MaskList_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EmailMasker.MaskList(string.Empty));
    }

    [Fact]
    public void MaskList_SingleEmail_MasksIt()
    {
        Assert.Equal("us**@example.com", EmailMasker.MaskList("user@example.com"));
    }

    [Fact]
    public void MaskList_CommaSeparated_MasksEachAndJoinsWithCommaSpace()
    {
        var result = EmailMasker.MaskList("alice@a.com, bob@b.com");
        Assert.Equal("al***@a.com, bo*@b.com", result);
    }

    [Fact]
    public void MaskList_UntrimmedEntries_TrimsBeforeMasking()
    {
        var result = EmailMasker.MaskList("  user@example.com  ");
        Assert.Equal("us**@example.com", result);
    }
}
