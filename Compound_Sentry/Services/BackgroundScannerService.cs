using Microsoft.AspNetCore.SignalR;
using Compound_Sentry.Hubs;
using Compound_Sentry.Models;
using Compound_Sentry.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compound_Sentry.Services
{
    public class BackgroundScannerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly NetworkScannerService _networkScanner;
        private int _peakConcurrent = 0;

        public BackgroundScannerService(IServiceScopeFactory scopeFactory, IHubContext<DashboardHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _networkScanner = new NetworkScannerService();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Background Real Network Scanner Started - Scanning every 20 seconds");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAndUpdateRealNetwork();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background scan error: {ex.Message}");
                }

                await Task.Delay(20000, stoppingToken); // Scan every 20 seconds
            }
        }

        private async Task ScanAndUpdateRealNetwork()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Auto-scanning network...");

            // Perform real network scan
            var devices = await _networkScanner.GetConnectedDevices();
            var now = DateTime.Now;

            // Get current devices from database
            var activeDevices = await dbContext.CurrentPresences
                .Select(p => p.MacAddress)
                .ToListAsync();

            var scannedMacs = devices.Select(d => d.MacAddress).ToList();
            bool hasChanges = false;

            // NEW DEVICES (ENTER)
            var newDevices = scannedMacs.Except(activeDevices).ToList();
            foreach (var mac in newDevices)
            {
                var device = devices.First(d => d.MacAddress == mac);

                var presence = new CurrentPresence
                {
                    MacAddress = mac,
                    PhoneType = device.Hostname,
                    FirstSeen = now,
                    LastSeen = now,
                    SignalStrength = 0
                };
                await dbContext.CurrentPresences.AddAsync(presence);

                var enterEvent = new DeviceEvent
                {
                    MacAddress = mac,
                    PhoneType = device.Hostname,
                    EventType = "ENTER",
                    SignalStrength = 0,
                    EventTimestamp = now
                };
                await dbContext.DeviceEvents.AddAsync(enterEvent);
                hasChanges = true;
                Console.WriteLine($"  [AUTO] New device ENTERED: {mac} - {device.Hostname}");
            }

            // DEVICES THAT LEFT (EXIT)
            var leftDevices = activeDevices.Except(scannedMacs).ToList();
            foreach (var mac in leftDevices)
            {
                var presence = await dbContext.CurrentPresences.FirstOrDefaultAsync(p => p.MacAddress == mac);
                if (presence != null)
                {
                    var duration = (int)(now - presence.FirstSeen).TotalSeconds;

                    var exitEvent = new DeviceEvent
                    {
                        MacAddress = mac,
                        PhoneType = presence.PhoneType,
                        EventType = "EXIT",
                        SignalStrength = presence.SignalStrength,
                        EventTimestamp = now,
                        DurationSeconds = duration
                    };
                    await dbContext.DeviceEvents.AddAsync(exitEvent);
                    dbContext.CurrentPresences.Remove(presence);
                    hasChanges = true;
                    Console.WriteLine($"  [AUTO] Device LEFT: {mac} (Duration: {duration} seconds)");
                }
            }

            // UPDATE LAST SEEN for existing devices
            var existingDevices = scannedMacs.Intersect(activeDevices).ToList();
            foreach (var mac in existingDevices)
            {
                var presence = await dbContext.CurrentPresences.FirstOrDefaultAsync(p => p.MacAddress == mac);
                if (presence != null)
                {
                    presence.LastSeen = now;
                    dbContext.CurrentPresences.Update(presence);
                }
            }

            if (hasChanges)
            {
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"  [AUTO] Database updated - Changes detected");
            }

            // Update peak concurrent
            var currentCount = await dbContext.CurrentPresences.CountAsync();
            if (currentCount > _peakConcurrent)
                _peakConcurrent = currentCount;

            // Send updates via SignalR to all connected clients
            await SendCurrentDevices(dbContext);
            await SendRecentActivity(dbContext);
            await SendStats(dbContext);

            if (!hasChanges)
            {
                Console.WriteLine($"  [AUTO] No changes - {currentCount} devices present");
            }
        }

        private async Task SendCurrentDevices(ApplicationDbContext dbContext)
        {
            var devices = await dbContext.CurrentPresences
                .OrderByDescending(p => p.LastSeen)
                .ToListAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveCurrentDevices", devices);
        }

        private async Task SendRecentActivity(ApplicationDbContext dbContext)
        {
            var activities = await dbContext.DeviceEvents
                .OrderByDescending(e => e.EventTimestamp)
                .Take(20)
                .ToListAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveRecentActivity", activities);
        }

        private async Task SendStats(ApplicationDbContext dbContext)
        {
            var today = DateTime.Today;
            var totalToday = await dbContext.DeviceEvents
                .Where(e => e.EventTimestamp.Date == today && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                .Select(e => e.MacAddress)
                .Distinct()
                .CountAsync();

            var currentCount = await dbContext.CurrentPresences.CountAsync();

            if (currentCount > _peakConcurrent)
                _peakConcurrent = currentCount;

            var avgDuration = await dbContext.DeviceEvents
                .Where(e => e.EventType == "EXIT" && e.EventTimestamp.Date == today && e.DurationSeconds.HasValue)
                .AverageAsync(e => (double?)e.DurationSeconds) ?? 0;

            await _hubContext.Clients.All.SendAsync("ReceiveStats", new
            {
                totalToday,
                currentCount,
                peakConcurrent = _peakConcurrent,
                avgDuration = Math.Round(avgDuration / 60, 1)
            });
        }
    }
}