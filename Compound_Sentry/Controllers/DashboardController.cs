using Compound_Sentry.Data;
using Compound_Sentry.Models;
using Compound_Sentry.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Compound_Sentry.Hubs;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Compound_Sentry.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AdminUser> _userManager;
        private readonly SignInManager<AdminUser> _signInManager;
        private readonly ApplicationDbContext _dbContext;

        // Kenya Time Zone (UTC+3)
        private static readonly TimeZoneInfo KenyaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. Africa Standard Time");

        public DashboardController(UserManager<AdminUser> userManager, SignInManager<AdminUser> signInManager, ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
        }

        // Helper method to get current Kenya time
        private DateTime GetKenyaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KenyaTimeZone);
        }

        // Helper method to convert UTC to Kenya time for display/storage
        private DateTime ConvertToKenyaTime(DateTime utcDate)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDate, KenyaTimeZone);
        }

        // ===== MAIN DASHBOARD PAGES =====
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Devices()
        {
            return View();
        }

        public IActionResult Analytics()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        // ===== USER PROFILE PAGES =====
        [HttpGet]
        public IActionResult MyProfile()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AdminSettings()
        {
            return View();
        }

        // ===== API ENDPOINTS =====
        [HttpGet]
        [Route("api/admin/profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return NotFound();
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.UserName,
                user.FullName,
                user.Email,
                user.CreatedAt
            });
        }

        [HttpGet]
        [Route("api/analytics/daily-traffic")]
        public async Task<IActionResult> GetDailyTraffic()
        {
            try
            {
                var kenyaToday = GetKenyaTime().Date;
                var dailyData = new List<int>();

                for (int i = 6; i >= 0; i--)
                {
                    var date = kenyaToday.AddDays(-i);
                    var count = await _dbContext.DeviceEvents
                        .Where(e => e.EventTimestamp.Date == date && e.EventType == "ENTER")
                        .CountAsync();
                    dailyData.Add(count);
                }

                return Ok(dailyData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting daily traffic: {ex.Message}");
                return Ok(new List<int> { 0, 0, 0, 0, 0, 0, 0 });
            }
        }

        [HttpGet]
        [Route("api/analytics/device-models")]
        public async Task<IActionResult> GetDeviceModels()
        {
            try
            {
                var deviceModels = await _dbContext.DeviceEvents
                    .Where(e => e.PhoneType != null && e.PhoneType != "Unknown")
                    .GroupBy(e => e.PhoneType)
                    .Select(g => new { Model = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(5)
                    .ToListAsync();

                var labels = deviceModels.Select(m => m.Model).ToList();
                var counts = deviceModels.Select(m => m.Count).ToList();

                // Add "Others" category if there are more devices
                var otherCount = await _dbContext.DeviceEvents
                    .Where(e => e.PhoneType == null || e.PhoneType == "Unknown" ||
                        !deviceModels.Select(m => m.Model).Contains(e.PhoneType))
                    .CountAsync();

                if (otherCount > 0)
                {
                    labels.Add("Others");
                    counts.Add(otherCount);
                }

                return Ok(new { labels, counts });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting device models: {ex.Message}");
                return Ok(new { labels = new List<string>(), counts = new List<int>() });
            }
        }

        [HttpGet]
        [Route("api/analytics/stats")]
        public async Task<IActionResult> GetAnalyticsStats()
        {
            try
            {
                var kenyaToday = GetKenyaTime().Date;
                var thirtyDaysAgo = kenyaToday.AddDays(-30);

                // Total devices in last 30 days (unique MAC addresses from ENTER and NETWORK)
                var total30Days = await _dbContext.DeviceEvents
                    .Where(e => e.EventTimestamp.Date >= thirtyDaysAgo && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                    .Select(e => e.MacAddress)
                    .Distinct()
                    .CountAsync();

                // Average daily visits (combine ENTER and NETWORK)
                var dailyVisits = await _dbContext.DeviceEvents
                    .Where(e => e.EventTimestamp.Date >= thirtyDaysAgo && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                    .GroupBy(e => e.EventTimestamp.Date)
                    .Select(g => g.Count())
                    .ToListAsync();

                var avgDaily = dailyVisits.Count > 0 ? dailyVisits.Average() : 0;

                // Peak hour
                var peakHourData = await _dbContext.DeviceEvents
                    .Where(e => e.EventType == "ENTER" || e.EventType == "NETWORK")
                    .GroupBy(e => e.EventTimestamp.Hour)
                    .Select(g => new { Hour = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .FirstOrDefaultAsync();

                var peakHour = peakHourData != null ? $"{peakHourData.Hour}:00" : "--";

                // Return rate (devices that visited more than once)
                var totalUniqueDevices = await _dbContext.DeviceEvents
                    .Where(e => e.EventType == "ENTER" || e.EventType == "NETWORK")
                    .Select(e => e.MacAddress)
                    .Distinct()
                    .CountAsync();

                var returningDevices = await _dbContext.DeviceEvents
                    .Where(e => e.EventType == "ENTER" || e.EventType == "NETWORK")
                    .GroupBy(e => e.MacAddress)
                    .Where(g => g.Count() > 1)
                    .CountAsync();

                var returnRate = totalUniqueDevices > 0 ? (returningDevices * 100 / totalUniqueDevices) : 0;

                return Ok(new
                {
                    total30Days,
                    avgDaily = Math.Round(avgDaily, 1),
                    peakHour,
                    returnRate
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stats: {ex.Message}");
                return Ok(new { total30Days = 0, avgDaily = 0, peakHour = "--", returnRate = 0 });
            }
        }

        [HttpGet]
        [Route("api/dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var kenyaToday = GetKenyaTime().Date;
                var nowKenya = GetKenyaTime();
                var last24Hours = nowKenya.AddHours(-24);

                // Total devices today (unique MAC addresses from ENTER or NETWORK events)
                var totalToday = await _dbContext.DeviceEvents
                    .Where(e => e.EventTimestamp.Date == kenyaToday && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                    .Select(e => e.MacAddress)
                    .Distinct()
                    .CountAsync();

                // Currently present (from CurrentPresences table)
                var currentCount = await _dbContext.CurrentPresences.CountAsync();

                // Peak concurrent in last 24 hours (max entries per hour)
                var peakConcurrent = await _dbContext.DeviceEvents
                    .Where(e => e.EventTimestamp >= last24Hours && e.EventType == "ENTER")
                    .GroupBy(e => e.EventTimestamp.Hour)
                    .Select(g => g.Count())
                    .OrderByDescending(c => c)
                    .FirstOrDefaultAsync();

                // Average visit duration from last 24 hours
                var avgDurationData = await _dbContext.DeviceEvents
                    .Where(e => e.EventType == "EXIT" && e.EventTimestamp >= last24Hours && e.DurationSeconds.HasValue)
                    .AverageAsync(e => (double?)e.DurationSeconds);

                var avgDuration = avgDurationData.HasValue ? Math.Round(avgDurationData.Value / 60, 1) : 0;

                return Ok(new
                {
                    totalToday,
                    currentCount,
                    peakConcurrent = peakConcurrent > 0 ? peakConcurrent : currentCount,
                    avgDuration
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting dashboard stats: {ex.Message}");
                return Ok(new { totalToday = 0, currentCount = 0, peakConcurrent = 0, avgDuration = 0 });
            }
        }

        [HttpGet]
        [Route("api/dashboard/devices")]
        public async Task<IActionResult> GetDashboardDevices()
        {
            try
            {
                var devices = await _dbContext.CurrentPresences
                    .OrderByDescending(p => p.LastSeen)
                    .ToListAsync();

                // Use cached IP addresses - NO NETWORK SCAN HERE!
                var result = devices.Select(d => new
                {
                    d.MacAddress,
                    d.PhoneType,
                    d.FirstSeen,
                    d.LastSeen,
                    IpAddress = IpCacheService.GetIpAddress(d.MacAddress)
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting dashboard devices: {ex.Message}");
                return Ok(new List<object>());
            }
        }

        [HttpGet]
        [Route("api/dashboard/activity")]
        public async Task<IActionResult> GetRecentActivity()
        {
            try
            {
                var activities = await _dbContext.DeviceEvents
                    .OrderByDescending(e => e.EventTimestamp)
                    .Take(20)
                    .ToListAsync();

                // Use cached IP addresses - NO NETWORK SCAN HERE!
                var result = activities.Select(a => new
                {
                    a.MacAddress,
                    a.PhoneType,
                    a.EventType,
                    a.EventTimestamp,
                    a.DurationSeconds,
                    IpAddress = IpCacheService.GetIpAddress(a.MacAddress)
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting activity: {ex.Message}");
                return Ok(new List<object>());
            }
        }

        // ===== NETWORK SCAN API =====
        [HttpGet]
        [Route("api/network/scan")]
        public async Task<IActionResult> ScanNetwork()
        {
            try
            {
                Console.WriteLine("API: ScanNetwork called");
                var scanner = new NetworkScannerService();
                var devices = await scanner.GetConnectedDevices();

                Console.WriteLine($"API: Found {devices.Count} devices");

                // FIXED: Use Kenya time instead of server local time
                var now = GetKenyaTime();

                // Get current devices from database
                var activeDevices = await _dbContext.CurrentPresences
                    .Select(p => p.MacAddress)
                    .ToListAsync();

                var scannedMacs = devices.Select(d => d.MacAddress).ToList();

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
                    };
                    await _dbContext.CurrentPresences.AddAsync(presence);

                    var enterEvent = new DeviceEvent
                    {
                        MacAddress = mac,
                        PhoneType = device.Hostname,
                        EventType = "ENTER",
                        EventTimestamp = now
                    };
                    await _dbContext.DeviceEvents.AddAsync(enterEvent);
                }

                // DEVICES THAT LEFT (EXIT)
                var leftDevices = activeDevices.Except(scannedMacs).ToList();
                foreach (var mac in leftDevices)
                {
                    var presence = await _dbContext.CurrentPresences.FirstOrDefaultAsync(p => p.MacAddress == mac);
                    if (presence != null)
                    {
                        var duration = (int)(now - presence.FirstSeen).TotalSeconds;
                        var exitEvent = new DeviceEvent
                        {
                            MacAddress = mac,
                            PhoneType = presence.PhoneType,
                            EventType = "EXIT",
                            EventTimestamp = now,
                            DurationSeconds = duration
                        };
                        await _dbContext.DeviceEvents.AddAsync(exitEvent);
                        _dbContext.CurrentPresences.Remove(presence);
                    }
                }

                // UPDATE LAST SEEN FOR EXISTING DEVICES
                var existingDevices = scannedMacs.Intersect(activeDevices).ToList();
                foreach (var mac in existingDevices)
                {
                    var presence = await _dbContext.CurrentPresences
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.MacAddress == mac);

                    if (presence != null)
                    {
                        presence.LastSeen = now;
                        _dbContext.Entry(presence).State = EntityState.Modified;
                    }
                }

                await _dbContext.SaveChangesAsync();

                // BROADCAST UPDATES VIA SIGNALR TO ALL CONNECTED CLIENTS
                var updatedPresence = await _dbContext.CurrentPresences.ToListAsync();
                var recentEvents = await _dbContext.DeviceEvents
                    .OrderByDescending(e => e.EventTimestamp)
                    .Take(20)
                    .ToListAsync();

                var kenyaToday = GetKenyaTime().Date;
                var stats = new
                {
                    totalToday = await _dbContext.DeviceEvents
                        .Where(e => e.EventTimestamp.Date == kenyaToday && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                        .Select(e => e.MacAddress)
                        .Distinct()
                        .CountAsync(),
                    currentCount = updatedPresence.Count,
                    peakConcurrent = updatedPresence.Count,
                    avgDuration = 0
                };

                var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<DashboardHub>>();
                await hub.Clients.All.SendAsync("ReceiveCurrentDevices", updatedPresence);
                await hub.Clients.All.SendAsync("ReceiveRecentActivity", recentEvents);
                await hub.Clients.All.SendAsync("ReceiveStats", stats);

                return Ok(devices);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API Scan error: {ex.Message}");
                return Ok(new List<NetworkDevice>());
            }
        }

        [HttpGet]
        [Route("api/devices/all")]
        public async Task<IActionResult> GetAllDevices()
        {
            try
            {
                var activeDevices = await _dbContext.CurrentPresences
                    .Select(p => p.MacAddress)
                    .ToListAsync();

                var allDeviceMacs = await _dbContext.DeviceEvents
                    .Select(e => e.MacAddress)
                    .Distinct()
                    .ToListAsync();

                var devices = new List<DeviceHistoryDto>();

                foreach (var mac in allDeviceMacs)
                {
                    var firstSeen = await _dbContext.DeviceEvents
                        .Where(e => e.MacAddress == mac && (e.EventType == "ENTER" || e.EventType == "NETWORK"))
                        .OrderBy(e => e.EventTimestamp)
                        .Select(e => e.EventTimestamp)
                        .FirstOrDefaultAsync();

                    var lastSeen = await _dbContext.DeviceEvents
                        .Where(e => e.MacAddress == mac)
                        .OrderByDescending(e => e.EventTimestamp)
                        .Select(e => e.EventTimestamp)
                        .FirstOrDefaultAsync();

                    var latestEvent = await _dbContext.DeviceEvents
                        .Where(e => e.MacAddress == mac)
                        .OrderByDescending(e => e.EventTimestamp)
                        .FirstOrDefaultAsync();

                    var deviceName = latestEvent?.PhoneType ?? "Unknown";
                    var totalVisits = await _dbContext.DeviceEvents
                        .CountAsync(e => e.MacAddress == mac && e.EventType == "ENTER");
                    var totalTimeSeconds = await _dbContext.DeviceEvents
                        .Where(e => e.MacAddress == mac && e.EventType == "EXIT" && e.DurationSeconds.HasValue)
                        .SumAsync(e => e.DurationSeconds ?? 0);
                    var totalTimeMinutes = totalTimeSeconds / 60;
                    var isOnline = activeDevices.Contains(mac);

                    // Use cached IP address
                    var ipAddress = IpCacheService.GetIpAddress(mac);

                    devices.Add(new DeviceHistoryDto
                    {
                        MacAddress = mac,
                        DeviceName = deviceName,
                        FirstSeen = firstSeen,
                        LastSeen = lastSeen,
                        TotalVisits = totalVisits,
                        TotalTimeMinutes = totalTimeMinutes,
                        IsOnline = isOnline,
                        IpAddress = ipAddress
                    });
                }

                return Ok(devices.OrderByDescending(d => d.LastSeen));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all devices: {ex.Message}");
                return Ok(new List<DeviceHistoryDto>());
            }
        }

        [HttpPost]
        [Route("api/admin/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return Unauthorized();

            if (model.CurrentPassword == null || model.NewPassword == null)
                return BadRequest(new { success = false, message = "Password fields are required" });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return Ok(new { success = true });
            }

            return Ok(new { success = false, message = string.Join(", ", result.Errors) });
        }

        [HttpPost]
        [Route("api/admin/update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return Unauthorized();

            user.FullName = model.FullName;
            user.Email = model.Email;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok(new { success = true });
            }

            return Ok(new { success = false, message = string.Join(", ", result.Errors) });
        }
    }

    public class ChangePasswordModel
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class UpdateProfileModel
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class DeviceHistoryDto
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int TotalVisits { get; set; }
        public int TotalTimeMinutes { get; set; }
        public bool IsOnline { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }
}