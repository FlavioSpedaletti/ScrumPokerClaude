using Microsoft.AspNetCore.SignalR;

public class User
{
    public string ConnectionId { get; set; }
    public string Name { get; set; }
    public double? Vote { get; set; }
}

public class Room
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<User> Users { get; set; } = new List<User>();
    public string CurrentTask { get; set; }
    public bool VotesRevealed { get; set; } = false;
}

// 2. Hub do SignalR
public class ScrumPokerHub : Hub
{
    private static Dictionary<string, Room> _rooms = new Dictionary<string, Room>();

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        // Remover usuário da sala quando desconectar
        foreach (var room in _rooms.Values)
        {
            var user = room.Users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            if (user != null)
            {
                room.Users.Remove(user);
                await Clients.Group(room.Id).SendAsync("UserLeft", user.Name);
                await Clients.Group(room.Id).SendAsync("UpdateUsers", room.Users);
                break;
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId, string userName)
    {
        // Criar sala se não existir
        if (!_rooms.ContainsKey(roomId))
        {
            _rooms[roomId] = new Room { Id = roomId, Name = $"Sala {roomId}" };
        }

        // Adicionar usuário à sala
        var room = _rooms[roomId];
        var user = new User
        {
            ConnectionId = Context.ConnectionId,
            Name = userName,
            Vote = null
        };

        room.Users.Add(user);

        // Adicionar conexão ao grupo da sala
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Notificar outros usuários
        await Clients.Group(roomId).SendAsync("UserJoined", userName);
        await Clients.Group(roomId).SendAsync("UpdateUsers", room.Users);

        // Enviar estado atual da sala para o novo usuário
        await Clients.Caller.SendAsync("RoomState", room);
    }

    public async Task SubmitVote(string roomId, double vote)
    {
        if (!_rooms.ContainsKey(roomId)) return;

        var room = _rooms[roomId];
        var user = room.Users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

        if (user != null)
        {
            user.Vote = vote;

            // Notificar que alguém votou (sem revelar o voto)
            await Clients.Group(roomId).SendAsync("UserVoted", user.Name);

            // Se todos votaram, notificar que a votação está completa
            if (room.Users.All(u => u.Vote.HasValue))
            {
                await Clients.Group(roomId).SendAsync("VotingComplete");
            }
        }
    }

    public async Task RevealVotes(string roomId)
    {
        if (!_rooms.ContainsKey(roomId)) return;

        var room = _rooms[roomId];
        room.VotesRevealed = true;

        // Calcular estatísticas
        var votes = room.Users.Where(u => u.Vote.HasValue).Select(u => u.Vote.Value).ToList();
        var stats = new
        {
            Average = votes.Any() ? votes.Average() : 0,
            Min = votes.Any() ? votes.Min() : 0,
            Max = votes.Any() ? votes.Max() : 0,
            MostCommon = votes.Any() ? votes.GroupBy(v => v)
                                           .OrderByDescending(g => g.Count())
                                           .First().Key : 0
        };

        await Clients.Group(roomId).SendAsync("VotesRevealed", room.Users, stats);
    }

    public async Task StartNewVoting(string roomId, string taskName)
    {
        if (!_rooms.ContainsKey(roomId)) return;

        var room = _rooms[roomId];
        room.CurrentTask = taskName;
        room.VotesRevealed = false;

        // Resetar votos
        foreach (var user in room.Users)
        {
            user.Vote = null;
        }

        await Clients.Group(roomId).SendAsync("NewVotingStarted", taskName);
        await Clients.Group(roomId).SendAsync("UpdateUsers", room.Users);
    }
}