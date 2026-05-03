using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Loom.Integrations.Webhooks;
using Xunit;

namespace Loom.Integrations.Tests;

public sealed class HmacSignatureValidatorTests
{
    private const string Secret = "shhh-its-a-secret";

    [Fact]
    public void Validate_AcceptsBase64DigestHeader()
    {
        var body = "{\"hello\":\"world\"}"u8.ToArray();
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));

        HmacSignatureValidator.Validate(signature, body, Secret).Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsHexDigestHeaderWithSha256Prefix()
    {
        var body = "{\"hello\":\"world\"}"u8.ToArray();
        var hex = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));

        HmacSignatureValidator.Validate($"sha256={hex}", body, Secret).Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsTamperedBody()
    {
        var body = "{\"hello\":\"world\"}"u8.ToArray();
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));
        var tampered = "{\"hello\":\"WORLD\"}"u8.ToArray();

        HmacSignatureValidator.Validate(signature, tampered, Secret).Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsBadSecret()
    {
        var body = "{\"hello\":\"world\"}"u8.ToArray();
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));

        HmacSignatureValidator.Validate(signature, body, "wrong-secret").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyHeader_IsRejected(string? header)
    {
        var body = "x"u8.ToArray();
        HmacSignatureValidator.Validate(header!, body, Secret).Should().BeFalse();
    }
}
