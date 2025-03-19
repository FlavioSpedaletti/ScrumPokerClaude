using Microsoft.AspNetCore.SignalR;

public class Participant
{
    public string ConnectionId { get; set; }
    public string Name { get; set; }
    public bool IsModerator { get; set; }
    public bool HasVoted { get; set; }
    public double? Vote { get; set; }
}

// Classe para representar o estado da votação
public class VotingStatus
{
    public bool IsVotingActive { get; set; }
    public string CurrentTask { get; set; }
}

// Classe para representar um voto
public class Vote
{
    public string UserName { get; set; }
    public double Value { get; set; }
}

// Hub do SignalR
public class ScrumPokerHub : Hub
{
    // Lista de participantes
    private static List<Participant> participants = new List<Participant>();

    // Estado atual da votação
    private static VotingStatus votingStatus = new VotingStatus
    {
        IsVotingActive = false,
        CurrentTask = ""
    };

    // Método chamado quando um usuário se conecta
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    // Método chamado quando um usuário se desconecta
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null)
        {
            participants.Remove(participant);
            await Clients.All.SendAsync("UserDisconnected", participant.Name);
            await UpdateParticipants();

            // Se o moderador sair, escolher um novo moderador
            if (participant.IsModerator && participants.Count > 0)
            {
                participants.First().IsModerator = true;
                await Clients.Client(participants.First().ConnectionId).SendAsync("SetModerator", true);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Método para entrar na sessão
    public async Task JoinSession(string username)
    {
        var participant = new Participant
        {
            ConnectionId = Context.ConnectionId,
            Name = username,
            IsModerator = participants.Count == 0, // Primeiro usuário é o moderador
            HasVoted = false,
            Vote = null
        };

        participants.Add(participant);

        await Clients.All.SendAsync("UserConnected", username);
        await Clients.Caller.SendAsync("SetModerator", participant.IsModerator);
        await UpdateParticipants();

        // Se houver uma votação em andamento, notificar o novo participante
        if (votingStatus.IsVotingActive)
        {
            await Clients.Caller.SendAsync("NewTask", votingStatus.CurrentTask);
            await Clients.Caller.SendAsync("UpdateVotingStatus", votingStatus);
        }
    }

    // Método para sair da sessão
    public async Task LeaveSession()
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null)
        {
            participants.Remove(participant);
            await Clients.All.SendAsync("UserDisconnected", participant.Name);
            await UpdateParticipants();

            // Se o moderador sair, escolher um novo moderador
            if (participant.IsModerator && participants.Count > 0)
            {
                participants.First().IsModerator = true;
                await Clients.Client(participants.First().ConnectionId).SendAsync("SetModerator", true);
            }
        }
    }

    // Método para iniciar uma votação
    public async Task StartVoting(string taskDescription)
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null && participant.IsModerator)
        {
            votingStatus.IsVotingActive = true;
            votingStatus.CurrentTask = taskDescription;

            // Resetar votos de todos os participantes
            foreach (var p in participants)
            {
                p.HasVoted = false;
                p.Vote = null;
            }

            await Clients.All.SendAsync("NewTask", taskDescription);
            await Clients.All.SendAsync("UpdateVotingStatus", votingStatus);
            await UpdateParticipants();
        }
    }

    // Método para submeter um voto
    public async Task SubmitVote(double value)
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null && votingStatus.IsVotingActive)
        {
            participant.HasVoted = true;
            participant.Vote = value;

            await UpdateParticipants();

            // Verificar se todos votaram
            if (participants.All(p => p.HasVoted))
            {
                await Clients.Group("Moderators").SendAsync("AllVotesSubmitted");
            }
        }
    }

    // Método para revelar votos
    public async Task RevealVotes()
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null && participant.IsModerator)
        {
            votingStatus.IsVotingActive = false;

            var votes = participants
                .Where(p => p.HasVoted && p.Vote.HasValue)
                .Select(p => new Vote { UserName = p.Name, Value = p.Vote.Value })
                .ToList();

            await Clients.All.SendAsync("RevealVotes", votes);
            await Clients.All.SendAsync("UpdateVotingStatus", votingStatus);
        }
    }

    // Método para resetar votação
    public async Task ResetVoting()
    {
        var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null && participant.IsModerator)
        {
            votingStatus.IsVotingActive = false;
            votingStatus.CurrentTask = "";

            // Resetar votos de todos os participantes
            foreach (var p in participants)
            {
                p.HasVoted = false;
                p.Vote = null;
            }

            await Clients.All.SendAsync("ResetVoting");
            await UpdateParticipants();
        }
    }

    // Método para atualizar a lista de participantes
    private async Task UpdateParticipants()
    {
        var participantsDTO = participants.Select(p => new
        {
            p.Name,
            p.IsModerator,
            p.HasVoted
        }).ToList();

        await Clients.All.SendAsync("UpdateParticipants", participantsDTO);
    }
}