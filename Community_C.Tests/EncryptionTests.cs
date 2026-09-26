using Community_C.Utility;

namespace Community_C.Tests;

public class EncryptionTests
{
    [Fact]
    public void HashRefreshToken_IsDeterministicAndDoesNotExposePlaintext()
    {
        const string token = "refresh-token-value";

        string? firstHash = Encryption.HashRefreshToken(token);
        string? secondHash = Encryption.HashRefreshToken(token);

        Assert.NotNull(firstHash);
        Assert.Equal(firstHash, secondHash);
        Assert.NotEqual(token, firstHash);
    }

    [Fact]
    public void OAuthStateHelpers_CreateUniqueTokensAndCompareSafely()
    {
        string firstState = Encryption.CreateRandomUrlSafeToken();
        string secondState = Encryption.CreateRandomUrlSafeToken();

        Assert.NotEqual(firstState, secondState);
        Assert.True(Encryption.FixedTimeEquals(firstState, firstState));
        Assert.False(Encryption.FixedTimeEquals(firstState, secondState));
        Assert.False(Encryption.FixedTimeEquals(firstState, null));
    }
}
