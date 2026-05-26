using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Compound_Sentry.Hubs
{
    public class DashboardHub : Hub
    {
        public async Task SendDeviceUpdate(string deviceData)
        {
            await Clients.All.SendAsync("ReceiveDeviceUpdate", deviceData);
        }

        public async Task SendActivityUpdate(string activityData)
        {
            await Clients.All.SendAsync("ReceiveActivityUpdate", activityData);
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.All.SendAsync("UserConnected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }
    }
}