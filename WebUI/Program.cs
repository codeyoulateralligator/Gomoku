using GameEngine;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;
using Persistence.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddGamePersistence(builder.Configuration);
var app = builder.Build();
app.Services.InitializeGamePersistence();

app.MapGet("/", () => Results.Content(WebPage.Html, "text/html"));

app.MapGet("/api/configurations", (IConfigRepository repository) =>
    Results.Ok(repository.List()));

app.MapPost("/api/configurations", (ConfigurationRequest request, IConfigRepository repository) =>
{
    var configuration = request.ToConfiguration(Guid.NewGuid(), request.Name, false);
    try
    {
        configuration.Validate();
        repository.Save(configuration);
        return Results.Created($"/api/configurations/{configuration.Id}", configuration);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPut("/api/configurations/{id:guid}", (Guid id, ConfigurationRequest request, IConfigRepository repository) =>
{
    var existing = repository.Get(id);
    if (existing is null)
    {
        return Results.NotFound();
    }

    if (existing.IsPreset)
    {
        return Results.BadRequest(new { error = "Presets cannot be edited." });
    }

    var configuration = request.ToConfiguration(id, request.Name, false);
    try
    {
        configuration.Validate();
        repository.Save(configuration);
        return Results.Ok(configuration);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapDelete("/api/configurations/{id:guid}", (Guid id, IConfigRepository repository) =>
    repository.Delete(id)
        ? Results.NoContent()
        : repository.Get(id) is { IsPreset: true }
            ? Results.BadRequest(new { error = "Presets cannot be deleted." })
            : Results.NotFound());

app.MapGet("/api/games", (IGameRepository repository) =>
    Results.Ok(repository.List().Select(GameSummary.From)));

app.MapGet("/api/games/{id:guid}", (Guid id, IGameRepository repository) =>
{
    var game = repository.Get(id);
    return game is null ? Results.NotFound() : Results.Ok(game);
});

app.MapPost("/api/games", (NewGameRequest request, IConfigRepository configurations, IGameRepository games, GameBrain brain) =>
{
    var configuration = configurations.Get(request.ConfigurationId);
    if (configuration is null)
    {
        return Results.BadRequest(new { error = "Select an existing configuration." });
    }

    if (string.IsNullOrWhiteSpace(request.FirstPlayerName) || string.IsNullOrWhiteSpace(request.SecondPlayerName))
    {
        return Results.BadRequest(new { error = "Both player names are required." });
    }

    var players = request.Mode?.ToUpperInvariant() switch
    {
        "HUMANVSHUMAN" => (
            new GamePlayer(request.FirstPlayerName.Trim(), 'X', PlayerKind.Human),
            new GamePlayer(request.SecondPlayerName.Trim(), 'O', PlayerKind.Human)),
        "HUMANVSAI" => (
            new GamePlayer(request.FirstPlayerName.Trim(), 'X', PlayerKind.Human),
            new GamePlayer("Random AI", 'O', PlayerKind.RandomAi)),
        "HUMANVSSEARCHAI" => (
            new GamePlayer(request.FirstPlayerName.Trim(), 'X', PlayerKind.Human),
            new GamePlayer("Search AI", 'O', PlayerKind.SearchAi)),
        "AIVSAI" => (
            new GamePlayer("Random AI 1", 'X', PlayerKind.RandomAi),
            new GamePlayer("Random AI 2", 'O', PlayerKind.RandomAi)),
        _ => ((GamePlayer First, GamePlayer Second)?)null
    };

    if (players is null)
    {
        return Results.BadRequest(new { error = "Mode must be HumanVsHuman, HumanVsAi, or AiVsAi." });
    }

    var game = brain.StartGame(configuration, request.Name?.Trim() is { Length: > 0 } name ? name : "Gomoku", players.Value.First, players.Value.Second);
    games.Save(game);
    game = AdvanceAi(game, games, brain);
    return Results.Created($"/api/games/{game.Id}", game);
});

app.MapPost("/api/games/{id:guid}/moves", (Guid id, MoveRequest request, IGameRepository games, GameBrain brain) =>
{
    var game = games.Get(id);
    if (game is null)
    {
        return Results.NotFound();
    }

    var result = brain.MakeMove(game, request.Row, request.Column);
    if (result.Status is MoveStatus.OutOfBounds or MoveStatus.Occupied or MoveStatus.GameFinished)
    {
        return Results.BadRequest(new { error = $"Move rejected: {result.Status}." });
    }

    games.Save(result.State);
    var updated = AdvanceAi(result.State, games, brain);
    return Results.Ok(updated);
});

app.MapDelete("/api/games/{id:guid}", (Guid id, IGameRepository repository) =>
    repository.Delete(id) ? Results.NoContent() : Results.NotFound());

app.Run();

static GameState AdvanceAi(GameState game, IGameRepository repository, GameBrain brain)
{
    var providers = new Dictionary<PlayerKind, IMoveProvider>
    {
        [PlayerKind.RandomAi] = new RandomAiMoveProvider(brain),
        [PlayerKind.SearchAi] = new SearchAiMoveProvider(brain)
    };
    while (game.Outcome == GameOutcome.InProgress && providers.TryGetValue(game.CurrentPlayer.Kind, out var provider))
    {
        var (row, column) = provider.GetMoveAsync(game, CancellationToken.None).Result;
        var result = brain.MakeMove(game, row, column);
        if (result.Status is not (MoveStatus.Played or MoveStatus.Won or MoveStatus.Draw))
        {
            throw new InvalidOperationException($"AI move provider returned an illegal move: {result.Status}.");
        }

        game = result.State;
        repository.Save(game);
    }

    return game;
}

internal sealed record ConfigurationRequest(
    string Name,
    int Width,
    int Height,
    int WinningLength,
    bool WrapHorizontal,
    OverlineRule OverlineRule)
{
    public GameConfiguration ToConfiguration(Guid id, string name, bool isPreset) =>
        new(id, name, Width, Height, WinningLength, WrapHorizontal, OverlineRule, isPreset);
}

internal sealed record NewGameRequest(
    Guid ConfigurationId,
    string? Name,
    string FirstPlayerName,
    string SecondPlayerName,
    string Mode);

internal sealed record MoveRequest(int Row, int Column);

internal sealed record GameSummary(
    Guid Id,
    string Name,
    string ConfigurationName,
    DateTimeOffset UpdatedAt,
    GameOutcome Outcome,
    string CurrentPlayer)
{
    public static GameSummary From(GameState game) => new(
        game.Id,
        game.Name,
        game.Configuration.Name,
        game.UpdatedAt,
        game.Outcome,
        game.CurrentPlayer.Name);
}

internal static class WebPage
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Gomoku</title>
          <style>
            body{font:16px system-ui,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#17212b;background:#f3f6f8}
            h1{color:#087e8b}section{background:white;padding:1rem 1.25rem;margin:1rem 0;border-radius:10px;box-shadow:0 2px 8px #18304418}
            label{display:inline-grid;gap:.25rem;margin:.35rem}input,select,button{font:inherit;padding:.45rem;border:1px solid #b8c5cc;border-radius:5px}
            button{cursor:pointer;background:#087e8b;color:white;border:0}button.secondary{background:#52636d}button:disabled{opacity:.5;cursor:default}
            #board{display:grid;gap:3px;overflow:auto;max-width:100%;margin-top:1rem}.cell{min-width:34px;min-height:34px;padding:0;background:#e7d6b1;color:#15232b;font-weight:bold}
            .last{outline:3px solid #f08a24;z-index:1}.list{display:grid;gap:.4rem}.status{min-height:1.5em;color:#a33}
          </style>
        </head>
        <body>
          <h1>Gomoku</h1>
          <p>Five in a row. Cylinder boards wrap across the left/right edge; exact-five configurations reject overlines.</p>
          <section>
            <h2>New game</h2>
            <label>Configuration <select id="config"></select></label>
            <label>Mode <select id="mode"><option value="HumanVsHuman">Human vs human</option>            <option value="HumanVsAi">Human vs random AI</option><option value="HumanVsSearchAi">Human vs search AI</option><option value="AiVsAi">Random AI vs random AI</option></select></label>
            <label>Game name <input id="gameName" value="Gomoku"></label>
            <label>Player 1 <input id="player1" value="Player 1"></label>
            <label>Player 2 <input id="player2" value="Player 2"></label>
            <button id="newGame">Start</button>
          </section>
          <section>
            <h2>Configurations</h2>
            <div class="list" id="configList"></div>
            <h3>Create / edit custom</h3>
            <input id="editId" type="hidden">
            <label>Name <input id="cfgName" value="My Gomoku"></label>
            <label>Width <input id="width" type="number" min="2" value="15"></label>
            <label>Height <input id="height" type="number" min="2" value="15"></label>
            <label>Winning length <input id="winning" type="number" min="2" value="5"></label>
            <label><input id="wrap" type="checkbox"> Wrap horizontally (cylinder)</label>
            <label>Overline <select id="overline"><option value="AtLeast">Five or more</option><option value="Exactly">Exactly five</option></select></label>
            <button id="saveConfig">Save custom configuration</button>
            <button id="deleteConfig" class="secondary">Delete selected custom</button>
          </section>
          <section>
            <h2>Saved games</h2>
            <button id="refreshGames" class="secondary">Refresh</button>
            <div id="games" class="list"></div>
          </section>
          <section>
            <h2 id="gameTitle">Board</h2>
            <div id="status" class="status"></div>
            <div id="board"></div>
          </section>
          <script>
            let currentGame=null, configurations=[];
            const byId=id=>document.getElementById(id);
            async function request(url, options={}) {
              const response=await fetch(url,{headers:{'Content-Type':'application/json'},...options});
              if(!response.ok){const body=await response.json().catch(()=>({}));throw new Error(body.error||response.statusText);}
              return response.status===204?null:response.json();
            }
            function showError(error){byId('status').textContent=error.message;}
            async function loadConfigurations(){
              configurations=await request('/api/configurations');
              const selector=byId('config'), list=byId('configList');selector.replaceChildren();list.replaceChildren();
              for(const c of configurations){
                const option=document.createElement('option');option.value=c.id;
                option.textContent=`${c.name} (${c.width}x${c.height}, ${c.overlineRule}${c.wrapHorizontal?', cylinder':''})`;selector.append(option);
                const row=document.createElement('div');row.append(document.createTextNode(`${c.name}: ${c.width}x${c.height}, line ${c.winningLength}`));
                if(!c.isPreset){
                  const edit=document.createElement('button');edit.textContent='Edit';edit.className='secondary';edit.onclick=()=>editConfig(c.id);row.append(' ',edit);
                  const remove=document.createElement('button');remove.textContent='Delete';remove.className='secondary';remove.onclick=()=>deleteConfig(c.id);row.append(' ',remove);
                }else{row.append(document.createTextNode(' [preset]'));}
                list.append(row);
              }
            }
            function editConfig(id){
              const c=configurations.find(x=>x.id===id);if(!c)return;
              byId('editId').value=c.id;byId('cfgName').value=c.name;byId('width').value=c.width;
              byId('height').value=c.height;byId('winning').value=c.winningLength;byId('wrap').checked=c.wrapHorizontal;
              byId('overline').value=c.overlineRule;window.scrollTo(0,0);
            }
            async function saveConfig(){
              const c={name:byId('cfgName').value,width:+byId('width').value,height:+byId('height').value,
                winningLength:+byId('winning').value,wrapHorizontal:byId('wrap').checked,overlineRule:byId('overline').value};
              const id=byId('editId').value;
              await request(id?`/api/configurations/${id}`:'/api/configurations',{method:id?'PUT':'POST',body:JSON.stringify(c)});
              byId('editId').value='';await loadConfigurations();byId('status').textContent='Configuration saved.';
            }
            async function deleteConfig(id){
              await request(`/api/configurations/${id}`,{method:'DELETE'});await loadConfigurations();
            }
            async function loadGames(){
              const games=await request('/api/games');
              const list=byId('games');list.replaceChildren();
              for(const g of games){
                const row=document.createElement('div'),load=document.createElement('button'),remove=document.createElement('button');
                load.className='secondary';load.textContent=`${g.name} — ${g.configurationName} — ${g.outcome} — ${new Date(g.updatedAt).toLocaleString()}`;
                load.onclick=()=>loadGame(g.id);remove.textContent='Delete';
                remove.onclick=async()=>{try{await request(`/api/games/${g.id}`,{method:'DELETE'});await loadGames();}catch(e){showError(e);}};
                row.append(load,' ',remove);list.append(row);
              }
            }
            async function loadGame(id){currentGame=await request(`/api/games/${id}`);renderGame();}
            async function createGame(){
              currentGame=await request('/api/games',{method:'POST',body:JSON.stringify({
                configurationId:byId('config').value,name:byId('gameName').value,
                firstPlayerName:byId('player1').value,secondPlayerName:byId('player2').value,mode:byId('mode').value})});
              renderGame();await loadGames();
            }
            function renderGame(){
              if(!currentGame)return;
              const g=currentGame,c=g.configuration,last=g.moveHistory.at(-1);
              byId('gameTitle').textContent=`${g.name} — ${g.outcome==='InProgress'?g.players[g.currentPlayerIndex].name+' to move':g.outcome+(g.winnerSymbol?' — '+g.players.find(p=>p.symbol===g.winnerSymbol).name+' wins':'')}`;
              const board=byId('board');board.style.gridTemplateColumns=`repeat(${c.width},minmax(34px,1fr))`;board.replaceChildren();
              for(let r=0;r<c.height;r++)for(let col=0;col<c.width;col++){
                const button=document.createElement('button');button.className='cell'+(last&&last.row===r&&last.column===col?' last':'');
                button.textContent=g.board[r][col]==='.'?'':g.board[r][col];button.disabled=g.outcome!=='InProgress'||g.players[g.currentPlayerIndex].kind!=='Human';
                button.title=`${col+1}, ${r+1}`;button.onclick=async()=>{try{currentGame=await request(`/api/games/${g.id}/moves`,{method:'POST',body:JSON.stringify({row:r,column:col})});renderGame();await loadGames();}catch(e){showError(e);}};
                board.append(button);
              }
              byId('status').textContent=g.configuration.wrapHorizontal?'Cylinder edge wraps horizontally.':'';
            }
            byId('newGame').onclick=()=>createGame().catch(showError);
            byId('saveConfig').onclick=()=>saveConfig().catch(showError);
            byId('deleteConfig').onclick=()=>{const id=byId('editId').value;if(id)deleteConfig(id).catch(showError);};
            byId('refreshGames').onclick=()=>loadGames().catch(showError);
            Promise.all([loadConfigurations(),loadGames()]).catch(showError);
          </script>
        </body>
        </html>
        """;
}
