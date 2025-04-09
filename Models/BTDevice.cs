using Windows.Networking.Sockets;
using Windows.Networking;

namespace ConsoleBattery;

public sealed class BTDevice
{
    public string FriendlyName { get; private set; }

    public HostName DeviceHost { get; private set; }

    public string ServiceName { get; private set; }

    public SocketProtectionLevel ProtectionLevel { get; private set; }

    internal BTDevice(HostName deviceHost, string serviceName, SocketProtectionLevel socketProtectionLevel, string friendlyName)
    {
        DeviceHost = deviceHost;
        ServiceName = serviceName;
        ProtectionLevel = socketProtectionLevel;
        FriendlyName = friendlyName;
    }
}