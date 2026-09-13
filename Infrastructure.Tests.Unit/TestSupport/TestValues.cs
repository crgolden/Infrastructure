namespace Infrastructure.Tests.Unit.TestSupport;

internal static class TestValues
{
    internal static string LowercaseToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)));

    internal static string NewTransportFailureMessage() => $"transport-failure-{LowercaseToken(10)}";

    internal static string NewServiceDescription() => $"description-{LowercaseToken(10)}";

    internal static string NewServiceAddress() => $"https://{LowercaseToken(12)}.example";

    internal static string LoopbackHost => System.Net.IPAddress.Loopback.ToString();

    internal static string NewUnexpectedHealthBody() => LowercaseToken(8);

    internal static string NewFailureMessage() => $"failure-{LowercaseToken(10)}";

    internal static string NewHostname() => $"{LowercaseToken(12)}.example";

    internal static TimeSpan NewPingInterval() => TimeSpan.FromSeconds(Random.Shared.Next(1, 3_600));

    internal static string NewMonitoredServiceName() => $"{LowercaseToken(5)} {LowercaseToken(7)}";

    internal static string NewDatabaseName() => LowercaseToken(8);

    internal static string NewUserId() => LowercaseToken(6);

    internal static string NewPassword() => LowercaseToken(10);

    internal static int NewClosedLoopbackPort()
    {
        using var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
