using System.Net.NetworkInformation;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNative.Connectivity;

public enum ConnectionKind
{
    Ethernet,
    Wifi,
    Cellular,
    Loopback,
    Other,
}

public sealed record Connection(string Name, ConnectionKind Kind, long SpeedBitsPerSecond);

public sealed record ConnectivitySnapshot(
    DateTimeOffset SampledAt,
    IReadOnlyList<Connection> ActiveInterfaces
)
{
    public bool HasNetworkInterface => ActiveInterfaces.Count != 0;
}

public interface IConnectivity
{
    ConnectivitySnapshot GetCurrent();
    IAsyncEnumerable<ConnectivitySnapshot> WatchAsync(
        CancellationToken cancellationToken = default
    );
}

/// <summary>Reports OS network interfaces. It does not claim that the Internet or a backend is reachable.</summary>
public sealed class SystemConnectivity : IConnectivity
{
    public SystemConnectivity(PresentationTarget? target = null) =>
        PlatformGuard.Desktop(target ?? PresentationTarget.Local);

    public ConnectivitySnapshot GetCurrent()
    {
        var active = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .Select(x => new Connection(
                x.Name,
                x.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Ethernet
                    or NetworkInterfaceType.GigabitEthernet
                    or NetworkInterfaceType.FastEthernetFx
                    or NetworkInterfaceType.FastEthernetT => ConnectionKind.Ethernet,
                    NetworkInterfaceType.Wireless80211 => ConnectionKind.Wifi,
                    NetworkInterfaceType.Loopback => ConnectionKind.Loopback,
                    _ => ConnectionKind.Other,
                },
                x.Speed
            ))
            .ToArray();
        return new(DateTimeOffset.UtcNow, active);
    }

    public async IAsyncEnumerable<ConnectivitySnapshot> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var channel = Channel.CreateBounded<ConnectivitySnapshot>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            }
        );
        void Changed(object? _, EventArgs __) => channel.Writer.TryWrite(GetCurrent());
        NetworkChange.NetworkAddressChanged += Changed;
        NetworkChange.NetworkAvailabilityChanged += AvailabilityChanged;
        void AvailabilityChanged(object? _, NetworkAvailabilityEventArgs __) =>
            channel.Writer.TryWrite(GetCurrent());
        try
        {
            channel.Writer.TryWrite(GetCurrent());
            await foreach (
                var snapshot in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
            )
                yield return snapshot;
        }
        finally
        {
            NetworkChange.NetworkAddressChanged -= Changed;
            NetworkChange.NetworkAvailabilityChanged -= AvailabilityChanged;
            channel.Writer.TryComplete();
        }
    }
}

public static class ConnectivityServices
{
    public static IServiceCollection AddConnectivity(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectivity>(p => new SystemConnectivity(
            p.GetService<PresentationTarget>()
        ));
        return services;
    }
}
