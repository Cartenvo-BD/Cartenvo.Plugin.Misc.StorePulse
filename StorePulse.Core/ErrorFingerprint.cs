using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StorePulse.Core;

public static class ErrorFingerprint
{
    // Fingerprints are never displayed as error text. Bound work before normalization.
    public static string Create(string message)
    {
        var bounded = message.Length > 2048 ? message[..2048] : message;
        var template = Regex.Replace(bounded, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}\b|\b\d+\b", "#",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(template)));
    }
}
