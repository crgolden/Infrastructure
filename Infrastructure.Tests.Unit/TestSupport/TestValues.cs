namespace Infrastructure.Tests.Unit.TestSupport;

internal static class TestValues
{
    internal static string LowercaseToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)));

    internal static string NewTransportFailureMessage() => $"transport-failure-{LowercaseToken(10)}";

    internal static string NewServiceDescription() => $"description-{LowercaseToken(10)}";

    internal static string NewServiceAddress() => $"https://{LowercaseToken(12)}.example";
}
