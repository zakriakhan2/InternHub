using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace InternHub.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
        public static string UserGroup(int userId) => $"user-{userId}";

        private int CurrentUserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(CurrentUserId));
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(CurrentUserId));
            await base.OnDisconnectedAsync(exception);
        }
    }
}