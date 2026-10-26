using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class ErrorFingerprintTests
{
    [Test]
    public void DynamicOrderNumbersGroupWithoutReturningSourceText()
    {
        Assert.That(ErrorFingerprint.Create("Request failed for order 12345"),
            Is.EqualTo(ErrorFingerprint.Create("Request failed for order 67890")));
        Assert.That(ErrorFingerprint.Create("private@example.com token=secret"), Does.Not.Contain("secret"));
        Assert.That(ErrorFingerprint.Create("SMTP failed"), Is.Not.EqualTo(ErrorFingerprint.Create("Payment failed")));
    }
}
