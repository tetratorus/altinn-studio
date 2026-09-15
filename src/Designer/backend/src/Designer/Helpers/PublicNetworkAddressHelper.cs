using System;
using System.Net;
using System.Net.Sockets;

namespace Altinn.Studio.Designer.Helpers;

/// <summary>
/// Determines whether a host or IP address belongs to a network range that must never be
/// reached by server-side requests made on behalf of a user (loopback, private, link-local, etc.).
/// </summary>
public static class PublicNetworkAddressHelper
{
    private const string LocalhostHostName = "localhost";
    private const string LocalhostDomainSuffix = ".localhost";

    public static bool IsPublicHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalizedHost == LocalhostHostName || normalizedHost.EndsWith(LocalhostDomainSuffix))
        {
            return false;
        }

        if (IPAddress.TryParse(normalizedHost.Trim('[', ']'), out IPAddress? ipAddress))
        {
            return IsPublicAddress(ipAddress);
        }

        return true;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (
            IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || address.IsIPv6UniqueLocal
        )
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsPublicIPv4Address(address.GetAddressBytes());
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsPublicIPv6Address(address.GetAddressBytes());
        }

        return false;
    }

    private static bool IsPublicIPv4Address(ReadOnlySpan<byte> bytes)
    {
        return bytes[0] switch
        {
            0 => false, // 0.0.0.0/8 "this" network
            10 => false, // 10.0.0.0/8 private
            100 when bytes[1] >= 64 && bytes[1] <= 127 => false, // 100.64.0.0/10 shared address space
            127 => false, // 127.0.0.0/8 loopback
            169 when bytes[1] == 254 => false, // 169.254.0.0/16 link-local (cloud metadata)
            172 when bytes[1] >= 16 && bytes[1] <= 31 => false, // 172.16.0.0/12 private
            192 when bytes[1] == 0 && bytes[2] == 0 => false, // 192.0.0.0/24 IETF protocol assignments
            192 when bytes[1] == 168 => false, // 192.168.0.0/16 private
            198 when bytes[1] == 18 || bytes[1] == 19 => false, // 198.18.0.0/15 benchmarking
            >= 224 => false, // multicast and reserved
            _ => true,
        };
    }

    private static bool IsPublicIPv6Address(ReadOnlySpan<byte> bytes)
    {
        bool isUnspecified = true;
        foreach (byte value in bytes)
        {
            if (value != 0)
            {
                isUnspecified = false;
                break;
            }
        }

        if (isUnspecified)
        {
            return false;
        }

        // 64:ff9b::/96 NAT64 and 2002::/16 6to4 embed IPv4 addresses that must be checked as IPv4
        if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b)
        {
            return IsPublicIPv4Address(bytes[12..16]);
        }

        if (bytes[0] == 0x20 && bytes[1] == 0x02)
        {
            return IsPublicIPv4Address(bytes[2..6]);
        }

        return true;
    }
}
