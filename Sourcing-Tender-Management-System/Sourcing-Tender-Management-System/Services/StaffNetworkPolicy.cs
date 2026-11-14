using System.Net;
using Microsoft.Extensions.Options;

namespace Sourcing_Tender_Management_System.Services;

public class StaffNetworkOptions
{
    public string[] AllowedIpRanges { get; set; } = [];
}

public class StaffNetworkPolicy(IOptions<StaffNetworkOptions> options)
{
    public bool IsAllowed(IPAddress? ip)
    {
        var ranges = options.Value.AllowedIpRanges;
        if (ranges.Length == 0) return true;   // empty list = feature off
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        foreach (var range in ranges)
        {
            if (IPNetwork.TryParse(range, out var net) && net.Contains(ip)) return true;
            if (IPAddress.TryParse(range, out var single) && single.Equals(ip)) return true;
        }
        return false;
    }
}