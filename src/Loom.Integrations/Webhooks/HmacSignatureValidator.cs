using System.Security.Cryptography;
using System.Text;

namespace Loom.Integrations.Webhooks;

/// <summary>
/// HMAC-SHA256 webhook-signature validator. Constant-time comparison; no
/// short-circuiting on length mismatch is exposed to the caller. Used by
/// the /webhooks/{system} controllers to reject payloads whose signature
/// header doesn't match the body keyed against the connection's stored
/// shared secret.
///
/// Header format is system-specific: ADO uses an X-Vss-Hmac-Sha256 header
/// containing the raw base64 digest; GitHub uses sha256=&lt;hex&gt;; Atlassian
/// uses an x-hub-signature-256 header in sha256=&lt;hex&gt; form. The validator
/// accepts both shapes for portability.
/// </summary>
public static class HmacSignatureValidator
{
    public static bool Validate(string signatureHeader, byte[] body, string sharedSecret)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(sharedSecret))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret), body);

        // Try both wire shapes:
        if (TryDecodeBase64(signatureHeader, out var decodedB64) &&
            CryptographicOperations.FixedTimeEquals(expected, decodedB64))
        {
            return true;
        }

        var hexCandidate = signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signatureHeader[7..]
            : signatureHeader;
        if (TryDecodeHex(hexCandidate, out var decodedHex) &&
            CryptographicOperations.FixedTimeEquals(expected, decodedHex))
        {
            return true;
        }

        return false;
    }

    private static bool TryDecodeBase64(string raw, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(raw);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static bool TryDecodeHex(string raw, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(raw);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
