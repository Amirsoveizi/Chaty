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


        private Task BroadcastUserList()
        {
            var userNames = UserConnections.Keys.ToList();
            System.Diagnostics.Debug.WriteLine($"Broadcasting users: {string.Join(", ", userNames)}");
            return Clients.All.updateUserList(userNames); 
        }

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

            if (string.IsNullOrWhiteSpace(userName)) return;

            using (var db = new ChatContext())
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null)
                {
                    user = new User { UserName = userName };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                    await BroadcastUserList();
                }
            }

            UserConnections.AddOrUpdate(userName, connectionId, (key, oldId) => connectionId);
            System.Diagnostics.Debug.WriteLine($"User '{userName}' connected. ID: {connectionId}");

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

        public async Task CreateOrJoinGroup(string groupName)
        {
            string userName = GetCurrentUserName();
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(groupName))
            {
                Clients.Caller.notifyError("Username or group name is missing.");
                return;
            }

            using (var db = new ChatContext())
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null) { Clients.Caller.notifyError("User not found."); return; }

                var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupName == groupName);
                if (group == null)
                {
                    group = new Group { GroupName = groupName };
                    db.Groups.Add(group);
                    db.UserGroups.Add(new UserGroup { UserId = user.UserId, GroupId = group.GroupId });
                }
                else
                {
                    bool isMember = await db.UserGroups.AnyAsync(ug => ug.GroupId == group.GroupId && ug.UserId == user.UserId);
                    if (!isMember)
                    {
                        db.UserGroups.Add(new UserGroup { UserId = user.UserId, GroupId = group.GroupId });
                    }
                }
                await db.SaveChangesAsync();

                await Groups.Add(Context.ConnectionId, groupName);
                Clients.Group(groupName).notifyUserJoinedGroup(userName, groupName);
                Clients.Caller.notifyJoinedGroup(groupName, $"Successfully joined/created '{groupName}'.");
            }
        }

        public async Task SendMessageToGroup(string groupName, string messageContent)
        {
            string senderUserName = GetCurrentUserName();
            if (string.IsNullOrWhiteSpace(senderUserName) || string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(messageContent))
            {
                Clients.Caller.notifyError("Missing information for group message.");
                return;
            }

            using (var db = new ChatContext())
            {
                var sender = await db.Users.FirstOrDefaultAsync(u => u.UserName == senderUserName);
                var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupName == groupName);

                if (sender == null || group == null)
                {
                    Clients.Caller.notifyError("Sender or group not found.");
                    return;
                }

                bool isMember = await db.UserGroups.AnyAsync(ug => ug.GroupId == group.GroupId && ug.UserId == sender.UserId);
                if (!isMember) {
                     Clients.Caller.notifyError($"You are not a member of group '{groupName}'.");
                     return;
                }


                var chatMessage = new ChatMessage
                {
                    SenderId = sender.UserId,
                    SenderUserName = sender.UserName,
                    GroupId = group.GroupId,
                    MessageContent = messageContent,
                    Timestamp = DateTime.UtcNow
                };
                db.ChatMessages.Add(chatMessage);
                await db.SaveChangesAsync();

                Clients.Group(groupName).receiveGroupMessage(senderUserName, groupName, messageContent, chatMessage.Timestamp.ToString("g"));
            }
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
        
        public async Task LoadHistoricalMessages(string targetType, string targetName)
        {
            string currentUserName = GetCurrentUserName();
            if (string.IsNullOrWhiteSpace(currentUserName)) return;

            using (var db = new ChatContext())
            {
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.UserName == currentUserName);
                if (currentUser == null) return;

                IQueryable<ChatMessage> query = null;

                if (targetType == "group")
                {
                    var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupName == targetName);
                    if (group != null)
                    {
                        query = db.ChatMessages
                                  .Where(m => m.GroupId == group.GroupId)
                                  .Include(m => m.Sender);
                    }
                }
                else if (targetType == "private")
                {
                    var otherUser = await db.Users.FirstOrDefaultAsync(u => u.UserName == targetName);
                    if (otherUser != null)
                    {
                        query = db.ChatMessages
                                  .Where(m => (m.SenderId == currentUser.UserId && m.ReceiverId == otherUser.UserId) ||
                                               (m.SenderId == otherUser.UserId && m.ReceiverId == currentUser.UserId))
                                  .Include(m => m.Sender);
                    }
                }

                if (query != null)
                {
                    var messages = await query.OrderBy(m => m.Timestamp) 
                                              .Take(50) 
                                              .ToListAsync();
                    
                    Clients.Caller.loadMessages(messages.Select(m => new {
                        sender = m.SenderUserName,
                        receiver = (m.Receiver != null ? m.Receiver.UserName : null), 
                        group = (m.TargetGroup != null ? m.TargetGroup.GroupName : null), 
                        content = m.MessageContent,
                        timestamp = m.Timestamp.ToString("g"),
                        isPrivate = m.ReceiverId != null,
                        isGroup = m.GroupId != null
                    }).ToList());
                }
            }
        }
    }
}