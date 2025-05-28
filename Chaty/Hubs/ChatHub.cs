using Chaty.Models;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Chaty.Hubs
{
public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> UserConnections =
            new ConcurrentDictionary<string, string>();

        private Task BroadcastOnlineUserList()
        {
            var onlineUserNames = UserConnections.Keys.ToList();
            System.Diagnostics.Debug.WriteLine($"Broadcasting online users: {string.Join(", ", onlineUserNames)}");
            return Clients.All.updateOnlineStatus(onlineUserNames); 
        }

        public override async Task OnConnected()
        {
            string userName = Context.QueryString["username"];
            string connectionId = Context.ConnectionId;
            bool isNewUser = false;

            if (string.IsNullOrWhiteSpace(userName)) return;

            using (var db = new ChatContext())
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null)
                {
                    user = new User { UserName = userName };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                    isNewUser = true;
                }
            }

            UserConnections.AddOrUpdate(userName, connectionId, (key, oldId) => connectionId);
            System.Diagnostics.Debug.WriteLine($"User '{userName}' connected. ID: {connectionId}");


            if (isNewUser) 
            {
                await Clients.All.newUserAdded(userName);
            }

            await BroadcastOnlineUserList();

            await base.OnConnected();
        }

        public override async Task OnDisconnected(bool stopCalled)
        {
            var item = UserConnections.FirstOrDefault(kvp => kvp.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(item.Key))
            {
                string connectionId;
                UserConnections.TryRemove(item.Key, out connectionId);
                System.Diagnostics.Debug.WriteLine($"User '{item.Key}' disconnected.");

                await BroadcastOnlineUserList();
            }
            await base.OnDisconnected(stopCalled);
        }
        public async Task GetAllUsers()
        {
            using (var db = new ChatContext())
            {
                var allUserNames = await db.Users
                                         .Select(u => u.UserName)
                                         .ToListAsync();
                Clients.Caller.loadAllUsers(allUserNames);
            }
        }

        //[Authorize(Roles = "Owner")]
        public async Task GetChatHistory(string partnerUserName)
        {
            string currentUserName = GetCurrentUserName();
            if (string.IsNullOrWhiteSpace(currentUserName) || string.IsNullOrWhiteSpace(partnerUserName))
            {
                Clients.Caller.notifyError("Cannot load history, user not identified.");
                return;
            }

            using (var db = new ChatContext())
            {
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.UserName == currentUserName);
                var partnerUser = await db.Users.FirstOrDefaultAsync(u => u.UserName == partnerUserName);

                if (currentUser == null || partnerUser == null)
                {
                    Clients.Caller.notifyError("User not found for history lookup.");
                    return;
                }

                var messages = await db.ChatMessages
                    .Where(m => (m.SenderId == currentUser.UserId && m.ReceiverId == partnerUser.UserId) ||
                                (m.SenderId == partnerUser.UserId && m.ReceiverId == currentUser.UserId))
                    .OrderBy(m => m.Timestamp)
                    .Select(m => new
                    {
                        from = m.SenderUserName,
                        message = m.MessageContent,
                        time = m.Timestamp
                    })
                    .Take(100) 
                    .ToListAsync();
               
                Clients.Caller.loadChatHistory(partnerUserName, messages);
            }
        }

        private string GetCurrentUserName()
        {
            return UserConnections.FirstOrDefault(kvp => kvp.Value == Context.ConnectionId).Key;
        }

        public async Task SendPrivateMessage(string receiverUserName, string messageContent)
        {
            string senderUserName = GetCurrentUserName();
            if (string.IsNullOrWhiteSpace(senderUserName) || string.IsNullOrWhiteSpace(receiverUserName) || string.IsNullOrWhiteSpace(messageContent))
            {
                 Clients.Caller.notifyError("Missing information for private message.");
                return;
            }

            if (senderUserName.Equals(receiverUserName, StringComparison.OrdinalIgnoreCase))
            {
                Clients.Caller.notifyError("You cannot send a private message to yourself.");
                return;
            }

            using (var db = new ChatContext())
            {
                var sender = await db.Users.FirstOrDefaultAsync(u => u.UserName == senderUserName);
                var receiver = await db.Users.FirstOrDefaultAsync(u => u.UserName == receiverUserName);

                if (sender == null || receiver == null)
                {
                    Clients.Caller.notifyError("Sender or receiver not found.");
                    return;
                }

                var chatMessage = new ChatMessage
                {
                    SenderId = sender.UserId,
                    SenderUserName = sender.UserName,
                    ReceiverId = receiver.UserId,
                    MessageContent = messageContent,
                    Timestamp = DateTime.UtcNow
                };
                db.ChatMessages.Add(chatMessage);
                await db.SaveChangesAsync();

                string receiverConnectionId;
                if (UserConnections.TryGetValue(receiverUserName, out receiverConnectionId))
                {
                    Clients.Client(receiverConnectionId).receivePrivateMessage(senderUserName, receiverUserName, messageContent, chatMessage.Timestamp.ToString("g"));
                }
                Clients.Caller.receivePrivateMessage(senderUserName, receiverUserName, messageContent, chatMessage.Timestamp.ToString("g"));
            }
        }
    }
}