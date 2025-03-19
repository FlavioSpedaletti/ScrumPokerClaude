$(document).ready(function () {
    // Variáveis de estado
    let connection;
    let username = "";
    let selectedValue = null;
    let isModerator = false;
    let votingInProgress = false;

    // Estabelecer conexão com o SignalR
    setupSignalRConnection();

    function setupSignalRConnection() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/pokerHub")
            .withAutomaticReconnect()
            .build();

        setupSignalREvents();

        connection.start().catch(err => {
            console.error(err);
            alert("Erro ao conectar com o servidor. Por favor, recarregue a página.");
        });
    }

    function setupSignalREvents() {
        // Receber atualizações de participantes
        connection.on("UpdateParticipants", (participants) => {
            updateParticipantsUI(participants);
        });

        // Atualizar o status da votação
        connection.on("UpdateVotingStatus", (status) => {
            updateVotingStatusUI(status);
        });

        // Nova tarefa para votar
        connection.on("NewTask", (taskDescription) => {
            startNewTask(taskDescription);
        });

        // Revelar votos
        connection.on("RevealVotes", (votes) => {
            revealVotes(votes);
        });

        // Reset da votação
        connection.on("ResetVoting", () => {
            resetVoting();
        });

        // Definir moderador
        connection.on("SetModerator", (isUserModerator) => {
            setModerator(isUserModerator);
        });

        // Quando o usuário se conecta
        connection.on("UserConnected", (userName) => {
            console.log(`${userName} conectou-se.`);
        });

        // Quando o usuário se desconecta
        connection.on("UserDisconnected", (userName) => {
            console.log(`${userName} desconectou-se.`);
        });
    }

    // Atualizar UI de participantes
    function updateParticipantsUI(participants) {
        $("#participantsCount").text(participants.length);

        let participantsContainer = $("#participantsContainer");
        participantsContainer.empty();

        participants.forEach(participant => {
            let hasVoted = participant.hasVoted ? "✓" : "...";
            let participantElement = `
                        <div class="col-md-3 mb-2">
                            <div class="user-vote">
                                <strong>${participant.name}</strong> ${participant.isModerator ? "(Moderador)" : ""}
                                <br>
                                <span class="badge ${participant.hasVoted ? "bg-success" : "bg-secondary"}">${hasVoted}</span>
                            </div>
                        </div>
                    `;
            participantsContainer.append(participantElement);
        });
    }

    // Atualizar UI de status de votação
    function updateVotingStatusUI(status) {
        votingInProgress = status.isVotingActive;

        if (status.isVotingActive) {
            $("#votingStatusArea").removeClass("alert-success").addClass("alert-info");
            $("#votingStatusArea").text("Votação em andamento...");
            $("#revealVotesButton").prop("disabled", false);
        } else {
            $("#votingStatusArea").removeClass("alert-info").addClass("alert-success");
            $("#votingStatusArea").text("Votação finalizada!");
            $("#revealVotesButton").prop("disabled", true);
        }
    }

    // Iniciar nova tarefa
    function startNewTask(taskDescription) {
        $("#currentTaskDescription").text(taskDescription);
        $("#currentTaskArea").removeClass("hidden");
        $("#votingArea").removeClass("hidden");

        // Resetar seleção
        $(".poker-card").removeClass("selected");
        selectedValue = null;
        $("#selectedValue").text("Nenhuma");

        // Limpar tabela de resultados
        $("#resultsTableBody").empty();
    }

    // Revelar votos
    function revealVotes(votes) {
        let resultsTable = $("#resultsTableBody");
        resultsTable.empty();

        votes.forEach(vote => {
            let row = `
                        <tr>
                            <td>${vote.userName}</td>
                            <td>${vote.value}</td>
                        </tr>
                    `;
            resultsTable.append(row);
        });

        // Atualizar status
        $("#votingStatusArea").removeClass("alert-info").addClass("alert-success");
        $("#votingStatusArea").text("Votação finalizada!");

        // Ativar botão de nova votação
        $("#resetVotingButton").prop("disabled", false);
    }

    // Resetar votação
    function resetVoting() {
        $("#currentTaskArea").addClass("hidden");
        $("#votingArea").addClass("hidden");
        $("#resultsTableBody").empty();

        // Resetar seleção
        $(".poker-card").removeClass("selected");
        selectedValue = null;
        $("#selectedValue").text("Nenhuma");

        // Resetar botões
        $("#startVotingButton").prop("disabled", false);
        $("#revealVotesButton").prop("disabled", true);
        $("#resetVotingButton").prop("disabled", true);
    }

    // Definir moderador
    function setModerator(isUserModerator) {
        isModerator = isUserModerator;

        if (isModerator) {
            $("#moderatorArea").show();
        } else {
            $("#moderatorArea").hide();
        }
    }

    // Evento de clique no botão de entrada
    $("#enterButton").click(function () {
        username = $("#username").val().trim();

        if (username) {
            // Enviar nome para o servidor
            connection.invoke("JoinSession", username).catch(err => {
                console.error(err);
                alert("Erro ao entrar na sessão.");
            });

            // Mostrar tela principal
            $("#loginScreen").addClass("hidden");
            $("#mainScreen").removeClass("hidden");
            $("#userNameDisplay").text(username);
        } else {
            alert("Por favor, digite seu nome.");
        }
    });

    // Evento de clique no botão de saída
    $("#leaveButton").click(function () {
        connection.invoke("LeaveSession").catch(err => {
            console.error(err);
        });

        // Mostrar tela de login
        $("#mainScreen").addClass("hidden");
        $("#loginScreen").removeClass("hidden");
        username = "";
    });

    // Evento de clique nas cartas
    $(document).on("click", ".poker-card", function () {
        if (!votingInProgress) return;

        $(".poker-card").removeClass("selected");
        $(this).addClass("selected");

        selectedValue = $(this).data("value");
        $("#selectedValue").text(selectedValue);

        // Enviar voto para o servidor
        connection.invoke("SubmitVote", selectedValue).catch(err => {
            console.error(err);
            alert("Erro ao enviar voto.");
        });
    });

    // Evento de clique no botão de iniciar votação
    $("#startVotingButton").click(function () {
        let taskDescription = $("#taskDescription").val().trim();

        if (taskDescription) {
            connection.invoke("StartVoting", taskDescription).catch(err => {
                console.error(err);
                alert("Erro ao iniciar votação.");
            });

            // Desativar botão de iniciar votação
            $(this).prop("disabled", true);
        } else {
            alert("Por favor, descreva a tarefa.");
        }
    });

    // Evento de clique no botão de revelar votos
    $("#revealVotesButton").click(function () {
        connection.invoke("RevealVotes").catch(err => {
            console.error(err);
            alert("Erro ao revelar votos.");
        });
    });

    // Evento de clique no botão de resetar votação
    $("#resetVotingButton").click(function () {
        connection.invoke("ResetVoting").catch(err => {
            console.error(err);
            alert("Erro ao resetar votação.");
        });
    });
});