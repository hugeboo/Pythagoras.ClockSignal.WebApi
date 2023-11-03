using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;

namespace Pythagoras.ClockSignal.WebApi.Hubs
{
    [SignalRHub]
    public sealed class ClockSignalHub : Hub<IClockSignalHub>
    {
        public async Task NewClockTime(DateTime time)
        {
            await Clients.Others.NewClockTime(time);
        }

        public async Task NewVirtualTime(DateTime time)
        {
            await Clients.Others.NewVirtualTime(time);
        }

        public async Task StateChanged(string state)
        {
            await Clients.Others.StateChanged(state);
        }
    }
}
