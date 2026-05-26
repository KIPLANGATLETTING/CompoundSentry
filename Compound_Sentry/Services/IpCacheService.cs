using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compound_Sentry.Services
{
    public class IpCacheService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static Dictionary<string, string> _ipCache = new Dictionary<string, string>();
        private static DateTime _lastUpdate = DateTime.MinValue;
        private static readonly object _lock = new object();

        public IpCacheService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("IP Cache Service Started - Updating every 30 seconds");

            // Update immediately on start
            await UpdateIpCache();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(30000, stoppingToken); // Update every 30 seconds
                await UpdateIpCache();
            }
        }

        private async Task UpdateIpCache()
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Updating IP cache...");

                var scanner = new NetworkScannerService();
                var devices = await scanner.GetConnectedDevices();

                var newCache = new Dictionary<string, string>();
                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(device.MacAddress))
                    {
                        newCache[device.MacAddress.ToUpper()] = device.IpAddress ?? "Unknown";
                    }
                }

                lock (_lock)
                {
                    _ipCache = newCache;
                    _lastUpdate = DateTime.Now;
                }

                Console.WriteLine($"IP Cache updated: {_ipCache.Count} devices cached");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IP cache update failed: {ex.Message}");
            }
        }

        public static string GetIpAddress(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return "Unknown";

            lock (_lock)
            {
                if (_ipCache.TryGetValue(macAddress.ToUpper(), out string? ip))
                    return ip;
            }
            return "Unknown";
        }

        public static int GetCacheSize()
        {
            lock (_lock)
            {
                return _ipCache.Count;
            }
        }
    }
}