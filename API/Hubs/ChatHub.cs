using System;
using System.Collections.Concurrent;
using API.Data;
using API.DTOs;
using API.Extenions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using API.Models;

[Authorize]
public class ChatHub(UserManager<AppUser> userManager, AppDbContext context) : Hub
{
    public static readonly ConcurrentDictionary<string, OnlineUserDto>
onlineUsers = new();
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var ReceiverId = httpContext?.Request.Query["senderId"].ToString();
        var userName = Context.User!.Identity!.Name!;
        var currentUser = await userManager.FindByNameAsync(userName);
        var connectionId = Context.ConnectionId;

        if (onlineUsers.ContainsKey(userName))
        {
            onlineUsers[userName].ConnectionId = connectionId;
        }
        else
        {
            var user = new OnlineUserDto
            {
                ConnectionId = connectionId,
                UserName = userName,
                ProfilePicture = currentUser!.ProfileImage,
                FullName = currentUser!.FullName,
            };
            onlineUsers.TryAdd(userName, user);
            await Clients.AllExcept(connectionId).SendAsync("Notify", currentUser);
        }
        if (!string.IsNullOrEmpty(ReceiverId))
        {
            await LoadMessages(ReceiverId);
        }
        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
    }

    public async Task LoadMessages(string recipientId, int pageNumber = 1)
    {
        int pageSize = 10;
        var username = Context.User!.Identity!.Name;
        var currentUser = await userManager.FindByNameAsync(username!);

        if (currentUser is null)
        {
            return;
        }
        List<MessageResponseDto> messeges = await context.Messages
        .Where(x => x.ReceiverId == currentUser!.Id && x.SenderId ==
        recipientId || x.SenderId == currentUser!.Id && x.ReceiverId == recipientId)
        .OrderByDescending(x => x.CreatedDate)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new MessageResponseDto
        {
            Id = x.Id,
            SenderId = x.SenderId,
            ReceiverId = x.ReceiverId,
            Content = x.Content,
            IsRead = x.IsRead,
            CreatedDate = x.CreatedDate
        })
        .ToListAsync();

        foreach (var message in messeges)
        {
            var msg = await context.Messages.FirstOrDefaultAsync(x => x.Id
            == message.Id);
            if (msg != null && msg.ReceiverId == currentUser.Id)
            {
                msg.IsRead = true;
                await context.SaveChangesAsync();
            }
        }
        await Clients.User(currentUser.Id).SendAsync("ReceivedMessageList", messeges);
    }
    public async Task SendMessage(MessageRequestDto message)
    {
        var senderId = Context.User!.Identity!.Name;
        var recipientId = message.ReceiverId;

        var newMsg = new Message
        {
            Sender = await userManager.FindByNameAsync(senderId!),
            Receiver = await userManager.FindByNameAsync(recipientId!),
            IsRead = false,
            CreatedDate = DateTime.UtcNow,
            Content = message.Content
        };
        context.Messages.Add(newMsg);
        await context.SaveChangesAsync();

        await Clients.User(recipientId!).SendAsync("ReceiveNewMessage", newMsg);
    }

    public async Task NotifyTyping(string recipientUserName)
    {
        var senderUserName = Context.User!.Identity!.Name;
        if (senderUserName is null)
        {
            return;
        }
        var connectionId = onlineUsers.Values.FirstOrDefault(x => x.UserName ==
        recipientUserName)?.ConnectionId;
        if (connectionId != null)
        {
            await Clients.Client(connectionId).SendAsync("NotifyTypingToUser", senderUserName);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User!.Identity!.Name;
        onlineUsers.TryRemove(username!, out _);
        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
    }
    private async Task<IEnumerable<OnlineUserDto>> GetAllUsers()
    {
        var username = Context.User!.GetUserName();
        var onlineUsersSet = new HashSet<string>(onlineUsers.Keys);

        var users = await userManager.Users.Select(u => new OnlineUserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            FullName = u.FullName,
            ProfilePicture = u.ProfileImage,
            IsOnline = onlineUsersSet.Contains(u.UserName!),
            UnreadCount = context.Messages.Count(x => x.ReceiverId ==
            username && x.SenderId == u.Id && !x.IsRead)
        }).OrderByDescending(u => u.IsOnline)
          .ToListAsync();
        return users;
    }
}
