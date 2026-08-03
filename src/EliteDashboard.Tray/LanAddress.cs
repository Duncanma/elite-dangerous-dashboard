using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EliteDashboard.Tray;

public static class LanAddress
{
    /// <summary>
    /// Best-effort LAN IPv4 address for this machine - prefers a real,
    /// up, non-loopback, non-virtual adapter over just "the first IP found",
    /// since machines often have several (VPN, Hyper-V, Docker) that aren't
    /// reachable from a phone on the same physical network.
    /// </summary>
    public static string? Resolve()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .OrderBy(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0);

        foreach (var nic in candidates)
        {
            var address = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            if (address is not null)
            {
                return address.Address.ToString();
            }
        }

        return null;
    }
}
