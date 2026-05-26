using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compound_Sentry.Services
{
    public class NetworkScannerService
    {
        private string _myComputerName = Environment.MachineName;
        private string _myLocalIp = string.Empty;

        public async Task<List<NetworkDevice>> GetConnectedDevices()
        {
            var devices = new List<NetworkDevice>();

            try
            {
                Console.WriteLine("=== Starting Fast Network Scan ===");

                _myLocalIp = GetLocalIpAddress();
                if (string.IsNullOrEmpty(_myLocalIp)) return devices;

                string networkPrefix = GetNetworkPrefix(_myLocalIp);
                Console.WriteLine($"Network: {networkPrefix}.*");
                Console.WriteLine($"My Computer Name: {_myComputerName}");
                Console.WriteLine($"My Local IP: {_myLocalIp}");

                // Ping all devices quickly
                await PingNetworkFast(networkPrefix);
                await Task.Delay(500);

                // Get ARP table
                var arpDevices = GetArpTableFast();
                Console.WriteLine($"Found {arpDevices.Count} devices via ARP");

                // Filter out my own device
                arpDevices = arpDevices.Where(d => d.IpAddress != _myLocalIp).ToList();

                // Get names only for the found devices (parallel)
                var tasks = arpDevices.Select(async device =>
                {
                    device.Hostname = await GetDeviceNameSafe(device.IpAddress);
                    return device;
                });

                devices = (await Task.WhenAll(tasks)).ToList();

                foreach (var device in devices)
                {
                    Console.WriteLine($"  {device.IpAddress} - {device.MacAddress} - {device.Hostname}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scan error: {ex.Message}");
            }

            return devices;
        }

        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254"))
                {
                    return ip.ToString();
                }
            }
            return string.Empty;
        }

        private string GetNetworkPrefix(string ipAddress)
        {
            var parts = ipAddress.Split('.');
            return $"{parts[0]}.{parts[1]}.{parts[2]}.";
        }

        private async Task PingNetworkFast(string networkPrefix)
        {
            var tasks = new List<Task>();
            var ipsToPing = new List<int>();
            for (int i = 1; i <= 50; i++) ipsToPing.Add(i);
            for (int i = 200; i <= 254; i++) ipsToPing.Add(i);
            for (int i = 51; i <= 199; i++) ipsToPing.Add(i);

            foreach (var i in ipsToPing)
            {
                string ip = $"{networkPrefix}{i}";
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var ping = new Ping();
                        await ping.SendPingAsync(ip, 30);
                    }
                    catch { }
                }));
            }
            await Task.WhenAll(tasks);
        }

        private List<NetworkDevice> GetArpTableFast()
        {
            var devices = new List<NetworkDevice>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = "-a",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var ipMatch = Regex.Match(line, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
                var macMatch = Regex.Match(line, @"([0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2})");

                if (ipMatch.Success && macMatch.Success)
                {
                    string ip = ipMatch.Value;
                    string mac = macMatch.Value.Replace('-', ':').ToUpper();

                    if (!ip.StartsWith("224.") && !ip.StartsWith("239.") && !ip.EndsWith(".255") && mac != "FF:FF:FF:FF:FF:FF")
                    {
                        if (!devices.Any(d => d.IpAddress == ip))
                        {
                            devices.Add(new NetworkDevice
                            {
                                IpAddress = ip,
                                MacAddress = mac,
                                Hostname = "Unknown"
                            });
                        }
                    }
                }
            }

            return devices;
        }

        private async Task<string> GetDeviceNameSafe(string ipAddress)
        {
            string result = "Unknown";

            // Method 1: Try DNS reverse lookup
            try
            {
                var hostEntry = Dns.GetHostEntry(ipAddress);
                string name = hostEntry.HostName;
                if (!string.IsNullOrEmpty(name) && !name.Equals(ipAddress) && !name.Contains("unavailable"))
                {
                    // Make sure it's not my own computer name
                    if (!name.Equals(_myComputerName, StringComparison.OrdinalIgnoreCase))
                    {
                        result = name;
                    }
                }
            }
            catch { }

            // Method 2: Try ping -a for reverse lookup (with validation)
            if (result == "Unknown")
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ping",
                            Arguments = $"-a -n 1 -w 500 {ipAddress}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit(1000);

                    var nameMatch = Regex.Match(output, @"Pinging\s+([^\s]+)\s+\[");
                    if (nameMatch.Success)
                    {
                        string name = nameMatch.Groups[1].Value;
                        // Validate this is not my computer name and not a generic response
                        if (!string.IsNullOrEmpty(name) &&
                            !name.Equals(_myComputerName, StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("unreachable") &&
                            !name.Contains("could not") &&
                            !name.Contains("timed out") &&
                            !name.Equals(ipAddress))
                        {
                            result = name;
                        }
                    }
                }
                catch { }
            }

            // Method 3: Try nbtstat for Windows devices
            if (result == "Unknown")
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "nbtstat",
                            Arguments = $"-A {ipAddress} -n",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit(1000);

                    var nameMatch = Regex.Match(output, @"(\S+)\s+<00>\s+UNIQUE");
                    if (nameMatch.Success)
                    {
                        string name = nameMatch.Groups[1].Value;
                        // Validate not my computer name
                        if (!name.Equals(_myComputerName, StringComparison.OrdinalIgnoreCase))
                        {
                            result = name;
                        }
                    }
                }
                catch { }
            }

            return result;
        }
    }

    public class NetworkDevice
    {
        public string IpAddress { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
    }
}