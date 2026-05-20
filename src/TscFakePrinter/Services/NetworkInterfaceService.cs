using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TscFakePrinter.Services;

public static class NetworkInterfaceService
{
    public const string AllInterfacesLabel = "0.0.0.0 (All Interfaces)";
    public const string LoopbackLabel = "127.0.0.1 (Loopback)";

    public static IList<string> EnumerateChoices()
    {
        var list = new List<string> { AllInterfacesLabel, LoopbackLabel };

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var label = $"{addr.Address} ({nic.Name})";
                if (!list.Contains(label)) list.Add(label);
            }
        }

        return list;
    }

    public static IPAddress Resolve(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label == AllInterfacesLabel)
            return IPAddress.Any;
        if (label == LoopbackLabel)
            return IPAddress.Loopback;

        var ipText = label.Split(' ').First();
        return IPAddress.TryParse(ipText, out var ip) ? ip : IPAddress.Any;
    }
}
