# Ninja Eleven — Arquitetura, Domínio e Plano de Implementação

## 1. Objetivo deste documento

Este documento define a arquitetura técnica e o plano inicial de implementação do Ninja Eleven.

Ele deve servir como **documento-base para o desenvolvimento no VS Code com GitHub Copilot**. O objetivo é reduzir decisões implícitas: responsabilidades, dependências, contratos, modelos de domínio, persistência, comunicação em tempo real e etapas de migração devem seguir as regras descritas aqui.

A arquitetura foi definida para permitir:

- frontend React + TypeScript totalmente substituível;
- backend ASP.NET Core/.NET como autoridade do jogo;
- PostgreSQL como banco principal;
- Entity Framework Core para persistência;
- REST para consultas e operações que não exigem atualização em tempo real;
- SignalR para comunicação em tempo real durante partidas;
- evolução futura para multiplayer;
- sistema de transferências;
- múltiplas competições/divisões no futuro;
- testes automatizados da simulação;
- possibilidade de escalar a infraestrutura sem acoplar o motor de futebol à tecnologia de comunicação ou persistência.

---

# 2. Decisões arquiteturais obrigatórias

Estas decisões devem ser tratadas como regras do projeto.

## 2.1 Stack

### Backend

- .NET / ASP.NET Core
- C#
- Entity Framework Core
- PostgreSQL
- SignalR
- REST API

### Frontend

- React
- TypeScript

### Banco

- PostgreSQL
- IDs persistidos como `Guid`/UUID

---

# 3. Regra fundamental de dependência

A arquitetura seguirá estritamente:

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
Database
```

Essa ordem é **obrigatória**.

## 3.1 Controller

Responsável por:

- receber requisições HTTP;
- receber conexões/comandos do SignalR;
- validar aspectos básicos do request;
- autenticação/autorização de entrada quando aplicável;
- converter entrada em DTO;
- chamar Services;
- converter o resultado do Service em DTO;
- retornar HTTP status/resultados apropriados.

O Controller **não contém lógica de negócio**.

O Controller **não acessa Repository**.

O Controller **não acessa EF Core**.

O Controller **não conhece entidades de domínio como contrato de API**.

---

# 4. Services

Services são responsáveis pela lógica de negócio e pela orquestração dos casos de uso.

Exemplos:

- iniciar uma partida;
- validar escalação;
- realizar substituição;
- selecionar cobrador de pênalti;
- avançar a simulação;
- processar gol;
- processar cartão;
- processar lesão;
- atualizar classificação;
- iniciar nova rodada;
- finalizar campeonato;
- aplicar suspensão;
- processar recuperação de energia;
- processar transferências.

Um Service pode utilizar um ou mais Repositories.

Exemplo:

```text
MatchController
      ↓
MatchService
      ↓
MatchRepository
PlayerRepository
TeamRepository
```

O Service é o ponto onde as regras do jogo são coordenadas.

---

# 5. Repository

Repositories são responsáveis exclusivamente pela persistência e recuperação de dados.

Responsabilidades:

- consultar PostgreSQL;
- inserir dados;
- atualizar dados;
- remover dados;
- executar queries específicas;
- utilizar EF Core;
- controlar detalhes de persistência.

Repositories **não devem decidir regras de negócio**.

Exemplo incorreto:

```csharp
if (player.YellowCards >= 3)
{
    player.Suspended = true;
}
```

Isso é regra de negócio e deve estar no Service/domínio.

O Repository deve apenas persistir o estado resultante.

---

# 6. DTOs — regra obrigatória

Controllers e SignalR **somente trabalham com DTOs**.

Eles não recebem nem retornam entidades de domínio.

Eles não recebem nem retornam entidades EF Core.

Fluxo:

```text
HTTP Request
    ↓
Request DTO
    ↓
Controller
    ↓
Service
    ↓
Domain / Business Logic
    ↓
Repository
    ↓
Database
```

E na volta:

```text
Database
    ↓
Repository
    ↓
Domain / Business Logic
    ↓
Response DTO
    ↓
Controller / SignalR
    ↓
Client
```

## 6.1 DTOs de entrada

Exemplos:

```csharp
SelectLineupRequestDto
MakeSubstitutionRequestDto
SelectPenaltyTakerRequestDto
StartMatchRequestDto
CreateTransferRequestDto
```

## 6.2 DTOs de saída

Exemplos:

```csharp
MatchDto
MatchEventDto
MatchStateDto
PlayerDto
TeamDto
StandingDto
FixtureDto
ScorerDto
```

## 6.3 DTOs nunca devem conter entidades EF

Não fazer:

```csharp
public class TeamDto
{
    public Team Entity { get; set; }
}
```

Fazer:

```csharp
public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

---

# 7. SignalR

SignalR é o canal de comunicação em tempo real.

Ele será utilizado principalmente durante partidas.

O SignalR **não possui lógica de futebol**.

Ele apenas:

1. recebe um DTO/comando;
2. chama um Service;
3. recebe o resultado/eventos;
4. envia DTOs ao cliente.

Exemplo:

```text
React
   │
   │ SignalR command DTO
   ▼
MatchHub
   │
   ▼
MatchService
   │
   ▼
Match Engine
   │
   ▼
MatchEvent DTO
   │
   ▼
SignalR
   │
   ▼
React
```

O Hub não deve:

- calcular gol;
- calcular posse;
- decidir substituição;
- atualizar energia;
- acessar EF Core;
- acessar Repository diretamente.

---

# 8. REST

REST será utilizado para operações que não dependem de comunicação contínua em tempo real.

Exemplos:

```text
GET /team/{id}
GET /player/{id}
GET /competition/{id}
GET /season/{id}
GET /fixture/{id}
GET /standing/{id}
GET /scorer/{id}
```

As rotas devem usar **singular**, conforme decisão do projeto.

Exemplo:

```text
/player/{id}
```

e não:

```text
/players/{id}
```

## 8.1 Exemplos de endpoints

### Times

```text
GET /team
GET /team/{id}
```

### Jogadores

```text
GET /player
GET /player/{id}
```

### Competições

```text
GET /competition
GET /competition/{id}
```

### Temporadas

```text
GET /season
GET /season/{id}
```

### Partidas

```text
GET /match
GET /match/{id}
```

### Rodadas

```text
GET /round
GET /round/{id}
```

### Classificação

```text
GET /standing
```

A modelagem exata dos endpoints deve ser definida durante a implementação dos Services e DTOs.

---

# 9. Frontend React + TypeScript

O frontend deve ser considerado um cliente do backend.

Ele não é responsável pelas regras do futebol.

O React deve cuidar de:

- apresentação;
- navegação;
- estado visual;
- formulários;
- interação do usuário;
- renderização de eventos;
- conexão SignalR;
- chamadas REST;
- tratamento de loading/error;
- cache local de dados apropriados.

O React não deve:

- calcular resultado de partida;
- decidir quem venceu;
- calcular classificação;
- aplicar suspensão;
- decidir lesões;
- calcular energia;
- gerar narração;
- validar regras fundamentais do jogo.

Pode haver validações de UX no frontend para melhorar a experiência, mas a validação definitiva sempre pertence ao backend.

---

# 10. O backend é a autoridade

O servidor é a única fonte autoritativa para o estado do jogo.

Isso é particularmente importante para multiplayer.

Exemplo:

O React pode mostrar:

```text
Substituir jogador A por jogador B
```

Mas o servidor precisa validar:

- o jogador A está em campo?
- o jogador B está disponível?
- o jogador B está suspenso?
- o jogador B está lesionado?
- a equipe ainda tem substituições?
- o momento da partida permite a substituição?
- o usuário possui autorização para controlar aquela equipe?

Somente depois dessas validações o Service modifica o estado.

---

# 11. Domínio do jogo

O domínio deve ser independente de:

- React;
- TypeScript;
- HTML;
- CSS;
- HTTP;
- SignalR;
- EF Core;
- PostgreSQL.

O motor de futebol deve conhecer apenas regras de futebol e estado da partida.

Regra fundamental:

> O Match Engine sabe futebol. Ele não sabe o que é HTML, React, SignalR, HTTP ou PostgreSQL.

---

# 12. Estrutura inicial da solução

Sugestão:

```text
NinjaEleven/
│
├── backend/
│   ├── NinjaEleven.Api/
│   ├── NinjaEleven.Application/
│   ├── NinjaEleven.Domain/
│   └── NinjaEleven.Infrastructure/
│
├── frontend/
│   └── ninja-eleven-web/
│
├── tests/
│   ├── NinjaEleven.Domain.Tests/
│   ├── NinjaEleven.Application.Tests/
│   └── NinjaEleven.IntegrationTests/
│
└── README.md
```

## 12.1 NinjaEleven.Api

Contém:

- Controllers;
- SignalR Hubs;
- autenticação;
- configuração HTTP;
- middleware;
- DTOs de API;
- composição da aplicação.

Não contém regras de negócio.

---

# 13. NinjaEleven.Application

Contém os Services e casos de uso.

Exemplos:

```text
Services/
    PlayerService
    TeamService
    MatchService
    CompetitionService
    SeasonService
    StandingService
    TransferService
```

Também pode conter:

```text
Interfaces/
    IPlayerRepository
    ITeamRepository
    IMatchRepository
    ICompetitionRepository
    ISeasonRepository
    ITransferRepository
```

A camada Application coordena o negócio.

---

# 14. NinjaEleven.Domain

É o núcleo do jogo.

Contém:

```text
Players/
Teams/
Matches/
Competitions/
Seasons/
Transfers/
Standings/
Common/
```

Aqui ficam:

- entidades;
- value objects;
- enums;
- regras de domínio;
- Match Engine;
- eventos de domínio;
- algoritmos de simulação.

---

# 15. NinjaEleven.Infrastructure

Responsável por detalhes externos.

Exemplos:

```text
Persistence/
    NinjaElevenDbContext.cs

Repositories/
    PlayerRepository.cs
    TeamRepository.cs
    MatchRepository.cs
    CompetitionRepository.cs
    SeasonRepository.cs
    TransferRepository.cs

Configurations/
    PlayerConfiguration.cs
    TeamConfiguration.cs
    MatchConfiguration.cs
```

EF Core pertence aqui.

PostgreSQL pertence aqui.

O Domain não deve depender do EF Core.

---

# 16. Modelagem inicial do domínio

A modelagem deve começar pelas entidades principais.

## 16.1 Player

Representa a identidade e características relativamente estáveis do jogador.

Exemplo conceitual:

```csharp
Player
{
    Id
    Name
    BirthDate
    Position
    Speed
    Accuracy
    Dribbling
    Heading
    Strength
    GoalkeeperPower
}
```

O Player não deve carregar diretamente informações que pertencem a uma temporada ou vínculo com clube.

---

# 17. PlayerSeasonState

Informações variáveis durante a temporada devem ficar separadas.

Exemplo:

```csharp
PlayerSeasonState
{
    Id
    PlayerId
    SeasonId
    TeamId
    Energy
    Goals
    YellowCards
    RedCards
    SuspensionMatches
    Injury
}
```

Essa separação é importante para o futuro sistema de transferências.

Um jogador pode existir durante toda a carreira enquanto seu estado de temporada muda.

---

# 18. Team

Representa o clube.

```csharp
Team
{
    Id
    Name
    ShortName
    PrimaryColor
    SecondaryColor
}
```

O elenco não deve ser necessariamente armazenado como uma lista física dentro de Team.

O vínculo entre jogador e clube deve ser representado por entidades próprias.

---

# 19. TeamMembership / PlayerContract

Para suportar transferências:

```csharp
TeamMembership
{
    Id
    PlayerId
    TeamId
    StartDate
    EndDate
}
```

No futuro isso permitirá:

```text
Player
  ↓
Team A
  ↓
Transfer
  ↓
Team B
```

sem alterar a identidade do jogador.

---

# 20. Competition

Recomenda-se usar `Competition` em vez de amarrar o domínio apenas a `League`.

Isso permite futuramente:

- campeonato estadual;
- divisão;
- copa;
- torneio;
- competição internacional;
- outras estruturas.

Exemplo:

```csharp
Competition
{
    Id
    Name
    Type
}
```

---

# 21. CompetitionSeason

Uma competição existe em várias temporadas.

```csharp
CompetitionSeason
{
    Id
    CompetitionId
    SeasonId
}
```

Exemplo:

```text
Campeonato Brasileiro
    ├── 2026
    ├── 2027
    └── 2028
```

---

# 22. Season

Representa a temporada do jogo.

```csharp
Season
{
    Id
    Name
    StartDate
    EndDate
    Status
}
```

---

# 23. CompetitionParticipant

Representa os times participantes de uma competição/temporada.

```csharp
CompetitionParticipant
{
    Id
    CompetitionSeasonId
    TeamId
}
```

Isso permite que um mesmo clube participe de várias competições.

---

# 24. Round

Representa uma rodada.

```csharp
Round
{
    Id
    CompetitionSeasonId
    Number
}
```

---

# 25. Fixture

Representa uma partida agendada.

```csharp
Fixture
{
    Id
    RoundId
    HomeTeamId
    AwayTeamId
    Status
}
```

Resultado pode ser armazenado diretamente ou através do estado/resultados da partida, dependendo da implementação.

---

# 26. Match

A partida em execução precisa de um modelo próprio.

Ela representa a sessão/estado da partida.

Conceitualmente:

```csharp
Match
{
    Id
    FixtureId
    Status
    CurrentMinute
    HomeScore
    AwayScore
    Half
    Sequence
}
```

O `Sequence` é importante para comunicação em tempo real.

---

# 27. MatchState

O estado transitório da partida deve ser separado do histórico quando necessário.

Ele pode conter:

- minuto;
- placar;
- posse;
- energia;
- jogadores em campo;
- substituições;
- cartões;
- lesões;
- estatísticas;
- estado de cada equipe.

A decisão exata entre armazenar todo o estado ou reconstruí-lo por eventos será tomada durante a implementação.

---

# 28. MatchEvent

Cada evento importante da partida deve possuir estrutura própria.

Exemplos:

```text
GoalScored
OwnGoalScored
PenaltyAwarded
PenaltyTaken
PenaltySaved
Shot
Save
Corner
Foul
YellowCardShown
RedCardShown
PlayerInjured
SubstitutionMade
HalfTimeReached
SecondHalfStarted
MatchFinished
```

Um evento deve possuir, conceitualmente:

```csharp
MatchEvent
{
    MatchId
    Sequence
    Minute
    Type
    Payload
}
```

---

# 29. Sequence de eventos

Todo evento enviado pelo SignalR deve possuir uma sequência crescente.

Exemplo:

```text
1 Goal
2 KickOff
3 Foul
4 YellowCard
5 Substitution
6 Goal
```

Isso permite detectar perda de eventos.

Exemplo:

O React recebeu:

```text
Sequence 15
Sequence 16
Sequence 18
```

Ele sabe que perdeu o evento 17.

Nesse caso pode solicitar um novo snapshot ou eventos posteriores via REST.

---

# 30. Snapshot + eventos

Para permitir reconexão:

```text
REST
GET /match/{id}
        ↓
Estado atual
        ↓
SignalR
        ↓
novos eventos
```

Se o usuário perder conexão durante a partida:

1. React reconecta;
2. solicita estado atual;
3. recebe snapshot;
4. volta a acompanhar eventos SignalR.

Isso evita depender exclusivamente de uma conexão contínua.

---

# 31. Match Engine

O Match Engine é uma das partes mais importantes do sistema.

Ele deve ser isolado.

Exemplo:

```text
MatchEngine
    ↓
MatchState
    ↓
Simulation
    ↓
MatchEvents
```

Ele deve conseguir rodar sem:

- ASP.NET;
- SignalR;
- PostgreSQL;
- EF Core;
- React.

Isso permite testes unitários extremamente mais simples.

---

# 32. RNG — aleatoriedade

A simulação utiliza aleatoriedade.

Não devemos espalhar chamadas diretas a um gerador aleatório pelo domínio.

Criar uma abstração:

```csharp
IRandomSource
```

Exemplo conceitual:

```csharp
public interface IRandomSource
{
    int Next(int min, int max);
    double NextDouble();
}
```

Assim os testes podem usar um RNG determinístico.

---

# 33. Simulação determinística

Uma grande vantagem é conseguir reproduzir uma partida.

Exemplo:

```text
MatchId: X
Seed: 123456
```

Com a mesma configuração e seed:

```text
Match Engine
       ↓
mesmos eventos
       ↓
mesmo resultado
```

Isso será muito útil para:

- testes;
- debugging;
- investigação de bugs;
- replay;
- validação da migração do JavaScript para C#.

---

# 34. Golden Master

O código JavaScript atual deve ser tratado como referência comportamental durante a migração.

A estratégia recomendada é:

```text
JavaScript Engine
       ↓
cenário conhecido
       ↓
resultado

C# Engine
       ↓
mesmo cenário
       ↓
resultado
```

Não necessariamente os números aleatórios precisam ser idênticos desde o primeiro momento, mas as regras fundamentais devem ser comparadas.

Antes de remover o motor JavaScript:

- documentar regras;
- criar cenários;
- criar testes;
- implementar C#;
- comparar resultados;
- corrigir divergências;
- somente depois retirar a simulação do frontend.

---

# 35. Match Commands

O cliente não deve alterar estado diretamente.

Ele envia comandos.

Exemplos:

```text
SelectLineup
MakeSubstitution
SelectPenaltyTaker
PauseMatch
ResumeMatch
ChangeMatchSpeed
```

O Service valida o comando.

Exemplo:

```text
React
  ↓
MakeSubstitutionRequestDto
  ↓
MatchHub
  ↓
MatchService
  ↓
Validate
  ↓
Apply
  ↓
MatchEvent
  ↓
SignalR
```

---

# 36. Escalação

A escalação deve ser validada no backend.

Regras já definidas:

- 11 jogadores;
- exatamente um goleiro efetivo;
- qualquer combinação de DEF/MID/ATT é permitida;
- formações estranhas são permitidas;
- goleiro pode ser substituído por outro goleiro;
- goleiro pode ser substituído por jogador de linha;
- jogador de linha não pode substituir um goleiro enquanto já houver outro goleiro efetivo;
- o backend é a autoridade dessas regras.

---

# 37. Substituições

O backend deve controlar:

- quantidade máxima de substituições;
- jogadores disponíveis;
- jogadores lesionados;
- jogadores suspensos;
- cartões;
- goleiros;
- troca de goleiro;
- emergência de goleiro;
- momento da partida.

O frontend apenas apresenta as possibilidades permitidas.

---

# 38. Cartões e suspensões

As regras devem pertencer ao domínio.

Exemplo:

```text
3 amarelos
    ↓
suspensão da próxima partida
```

Cartão vermelho também pode gerar suspensão.

O estado da suspensão deve persistir entre partidas.

---

# 39. Lesões

Lesões devem possuir estado persistente.

Conceitualmente:

```csharp
Injury
{
    Severity
    MatchesRemaining
}
```

A severidade define a quantidade de partidas que o jogador poderá perder.

---

# 40. Energia

Energia deve persistir entre partidas.

A recuperação depende, entre outras coisas, de:

- participação na partida;
- tempo jogado;
- descanso;
- idade;
- estado físico definido pelo domínio.

Isso é importante porque a energia não pertence exclusivamente ao Match.

Ela pertence ao estado do jogador na temporada.

---

# 41. Artilharia

A artilharia deve ser derivada de estatísticas de temporada.

Exemplo:

```csharp
PlayerSeasonState.Goals
```

O endpoint de artilharia pode consultar o Service:

```text
GET /scorer
```

O Service consulta o Repository.

---

# 42. Classificação

A classificação deve ser calculada pelo backend.

Critérios definidos, nesta ordem:

1. Pontos;
2. Saldo de gols;
3. Gols marcados;
4. Confronto direto pela soma dos placares;
5. Menor número de cartões vermelhos;
6. Menor número de cartões.

A implementação deve tomar cuidado especialmente com o confronto direto envolvendo mais de dois times empatados.

A regra não deve ser simplificada para uma comparação apenas entre dois clubes se houver empate múltiplo.

---

# 43. Estatísticas de partida

A partida deve produzir estatísticas estruturadas.

Exemplos:

```text
Possession
Shots
ShotsOnTarget
Corners
Fouls
YellowCards
RedCards
Saves
Goals
Substitutions
```

Não depender de texto de narração para obter estatísticas.

---

# 44. Narração

A narração deve ser consequência dos eventos.

Não devemos fazer:

```text
if (text.Contains("goal"))
```

O correto é:

```text
GoalScored event
      ↓
NarrationService
      ↓
Narration DTO
```

O sistema pode possuir diferentes mensagens para o mesmo tipo de evento.

Exemplo:

```text
GoalScored
    ↓
NarrationService
    ↓
"GOOOOOL!"
```

A narração é uma representação do evento, não a fonte da verdade.

---

# 45. Cores dos times

Cada Team terá:

```text
PrimaryColor
SecondaryColor
```

A interface pode utilizar essas cores para:

- eventos;
- placar;
- destaque;
- substituições;
- narração;
- identidade visual.

A lógica de negócio não deve depender da cor.

---

# 46. Multiplayer futuro

A arquitetura já deve considerar múltiplos usuários.

Conceito:

```text
User
  ↓
Manager
  ↓
TeamMembership / ManagerAssignment
  ↓
Team
```

O usuário nunca deve simplesmente informar:

```text
teamId = X
```

e receber permissão automaticamente.

O backend deve verificar:

```text
Authenticated User
       ↓
Manager
       ↓
Team controlled by manager?
       ↓
YES
       ↓
execute command
```

Isso é fundamental para evitar que um jogador controle o time de outro usuário.

---

# 47. Autorização

A autorização deve ser implementada no backend.

Exemplos:

- visualizar informações públicas;
- visualizar elenco;
- controlar escalação;
- realizar substituição;
- iniciar partida;
- negociar jogador;
- executar transferência.

As permissões devem ser verificadas antes de executar comandos.

---

# 48. Transferências

O sistema futuro de transferências deve utilizar o modelo de identidade do jogador separado de seu vínculo com clube.

Exemplo:

```text
Player
   │
   ├── TeamMembership A
   │
   └── Transfer
           │
           ▼
      TeamMembership B
```

Isso permite manter:

- histórico;
- estatísticas;
- carreira;
- temporadas;
- clubes anteriores.

---

# 49. Concorrência durante a partida

Não pode existir mais de uma simulação simultânea para a mesma partida.

Precisamos garantir:

```text
Match X
    ↓
1 simulation loop
```

e não:

```text
Match X
    ↓
Thread A
Thread B
Thread C
```

Na primeira versão, uma aplicação ASP.NET Core em uma única instância pode utilizar um mecanismo de controle em memória.

Se o sistema for distribuído posteriormente, essa responsabilidade precisará ser adaptada para um mecanismo distribuído.

---

# 50. Loop da partida

Uma abordagem inicial:

```text
BackgroundService
      ↓
Active Match
      ↓
Match Engine
      ↓
Generate Event
      ↓
Persist important state/event
      ↓
Publish SignalR DTO
```

O BackgroundService não contém as regras do futebol.

Ele apenas orquestra a execução da partida.

---

# 51. Persistência

Não é recomendado escrever no PostgreSQL a cada pequeno tick visual da partida.

O banco deve receber:

- eventos importantes;
- snapshots;
- estado em momentos relevantes;
- resultado final;
- estatísticas;
- alterações persistentes.

O estado visual de 60 FPS do frontend não existe no backend.

---

# 52. Estado do jogo versus estado visual

Backend:

```text
minute = 37
score = 1 x 0
player X energy = 63
```

Frontend pode representar isso como:

```text
animação
barra
efeito
feed
som
```

Essas representações não pertencem ao domínio.

---

# 53. Velocidade da partida

A velocidade deve ser tratada como preferência de execução/cliente ou comando de sessão, e não como regra do futebol.

Exemplo:

```text
1x
2x
4x
```

O backend continua sendo autoridade sobre o tempo lógico.

O cliente não deve simplesmente declarar:

```text
minute = 50
```

A velocidade determina apenas a frequência com que o servidor avança a simulação.

---

# 54. Relógio da partida

O relógio lógico deve ser controlado pelo servidor.

Isso evita:

- manipulação pelo cliente;
- divergência entre jogadores;
- inconsistência de estado;
- problemas de reconexão.

O React recebe:

```text
CurrentMinute
```

e eventos.

---

# 55. Reconexão SignalR

Fluxo recomendado:

```text
Cliente conectado
      ↓
SignalR subscription
      ↓
recebe eventos

CONEXÃO PERDIDA

      ↓

REST GET /match/{id}
      ↓
snapshot atual
      ↓
SignalR reconnect
      ↓
novos eventos
```

O `Sequence` permite identificar se houve perda de eventos.

---

# 56. Segurança

Nunca confiar no frontend.

Tudo que altera o jogo precisa ser validado no servidor.

Exemplos:

- escalação;
- substituição;
- pênalti;
- transferência;
- velocidade;
- início de partida;
- controle do time.

---

# 57. Banco de dados — GUID

Todas as entidades persistidas utilizarão `Guid`.

Exemplo:

```csharp
public Guid Id { get; private set; }
```

No PostgreSQL isso será mapeado para `uuid`.

FKs também serão UUID:

```text
player_id uuid
team_id uuid
season_id uuid
```

Não utilizar IDs incrementais como `int` para entidades de domínio.

---

# 58. EF Core

EF Core será responsável pelo mapeamento entre domínio/persistência e PostgreSQL.

Configurações devem ficar separadas.

Exemplo:

```text
Configurations/
    PlayerConfiguration.cs
    TeamConfiguration.cs
    SeasonConfiguration.cs
    MatchConfiguration.cs
```

Evitar colocar configuração extensa dentro das próprias entidades.

---

# 59. Migrations

O banco deve ser criado através de migrations do EF Core.

Fluxo:

```text
Alterar Model
      ↓
Migration
      ↓
Review
      ↓
Database Update
```

Migrations devem ser versionadas no Git.

---

# 60. Testes

Precisamos de pelo menos três níveis.

## 60.1 Domain tests

Testam:

- regras de futebol;
- Match Engine;
- RNG;
- classificação;
- cartões;
- lesões;
- energia;
- substituições.

## 60.2 Application tests

Testam:

- Services;
- autorização;
- orquestração;
- chamadas aos repositories;
- regras de casos de uso.

## 60.3 Integration tests

Testam:

- API;
- PostgreSQL;
- EF Core;
- SignalR;
- fluxo completo.

---

# 61. Primeira fase — infraestrutura

Criar:

```text
NinjaEleven.sln

NinjaEleven.Api
NinjaEleven.Application
NinjaEleven.Domain
NinjaEleven.Infrastructure

NinjaEleven.Domain.Tests
NinjaEleven.Application.Tests
NinjaEleven.IntegrationTests
```

Configurar referências corretamente.

Regra:

```text
Api
 ├── Application
 └── Infrastructure

Application
 └── Domain

Infrastructure
 ├── Application
 └── Domain

Domain
 └── nenhuma camada externa
```

O Domain deve ser o mais independente possível.

---

# 62. Segunda fase — domínio básico

Criar inicialmente:

```text
Player
Team
Season
Competition
CompetitionSeason
CompetitionParticipant
Round
Fixture
Match
```

Depois:

```text
PlayerSeasonState
TeamMembership
```

---

# 63. Terceira fase — persistência

Criar:

```text
DbContext
Configurations
Repositories
Migrations
```

Criar primeiro as tabelas essenciais.

Não tentar modelar todo o sistema futuro de uma vez.

---

# 64. Quarta fase — Services

Criar:

```text
PlayerService
TeamService
CompetitionService
SeasonService
MatchService
StandingService
```

Os Controllers só poderão chamar esses Services.

---

# 65. Quinta fase — REST

Criar os primeiros endpoints:

```text
GET /team
GET /team/{id}

GET /player
GET /player/{id}

GET /competition
GET /competition/{id}

GET /season
GET /season/{id}

GET /match
GET /match/{id}
```

Depois adicionar:

```text
GET /round
GET /standing
GET /scorer
```

---

# 66. Sexta fase — Match Engine

Migrar as regras existentes do JavaScript para C#.

Ordem sugerida:

1. estado da partida;
2. escalações;
3. atributos;
4. RNG;
5. posse;
6. ataques;
7. finalizações;
8. defesas;
9. gols;
10. cartões;
11. faltas;
12. escanteios;
13. pênaltis;
14. gols contra;
15. lesões;
16. substituições;
17. energia;
18. intervalo;
19. segundo tempo;
20. encerramento.

---

# 67. Sétima fase — eventos

Criar eventos estruturados.

Exemplo:

```csharp
GoalScoredEvent
{
    MatchId
    Sequence
    Minute
    TeamId
    PlayerId
}
```

Não depender de strings para representar eventos.

---

# 68. Oitava fase — SignalR

Criar:

```text
MatchHub
```

Mas manter o Hub fino.

Fluxo:

```text
SignalR
 ↓
DTO
 ↓
Service
 ↓
Domain
 ↓
Event
 ↓
DTO
 ↓
SignalR
```

---

# 69. Nona fase — React

Criar o frontend somente depois dos contratos principais do backend estarem definidos.

Estrutura inicial sugerida:

```text
src/
├── api/
├── signalr/
├── components/
├── pages/
├── hooks/
├── types/
├── services/
└── state/
```

`types/` contém tipos TypeScript correspondentes aos DTOs públicos da API.

---

# 70. Não duplicar modelos de domínio no React

O TypeScript deve representar os contratos da API.

Exemplo:

```typescript
export interface PlayerDto {
    id: string;
    name: string;
    position: string;
    energy: number;
}
```

Isso não significa que React está implementando a entidade Player do domínio.

É apenas o contrato de comunicação.

---

# 71. Contratos de API

Os DTOs devem ser tratados como contratos públicos.

Mudanças incompatíveis precisam ser controladas.

Exemplo:

```text
Backend DTO
      ↓
JSON
      ↓
TypeScript type
```

Idealmente, futuramente podemos automatizar a geração de tipos TypeScript a partir do contrato OpenAPI.

---

# 72. Tratamento de erros

Services devem produzir erros de negócio claros.

Exemplos:

```text
PlayerNotAvailable
InvalidLineup
GoalkeeperRequired
SubstitutionLimitReached
PlayerSuspended
PlayerInjured
UnauthorizedTeamControl
MatchNotActive
InvalidPenaltyTaker
```

A API converte isso para respostas HTTP apropriadas.

O frontend não deve interpretar mensagens arbitrárias para descobrir regras.

---

# 73. Eventos versus DTOs

São conceitos diferentes.

Evento de domínio:

```text
GoalScored
```

DTO:

```text
GoalScoredDto
```

O domínio não deve depender do DTO de API.

O fluxo é:

```text
Domain Event
      ↓
Application
      ↓
DTO Mapper
      ↓
API / SignalR
```

---

# 74. Mapper

A conversão entre domínio e DTO deve ser explícita.

Exemplo:

```text
Player
   ↓
PlayerDto
```

Evitar retornar diretamente objetos internos.

Isso também protege o sistema de mudanças futuras no domínio.

---

# 75. Observabilidade

Desde o começo, registrar:

- início de partida;
- final de partida;
- exceções;
- comandos inválidos;
- erros de persistência;
- conexões SignalR relevantes;
- eventos importantes.

Logs não devem conter informações sensíveis desnecessárias.

---

# 76. Logs de partida

Uma partida deve ser diagnosticável.

Exemplo:

```text
Match started
Match event sequence 18
Goal scored
Player substituted
Half time
Second half started
Match finished
```

Isso facilitará muito debugging.

---

# 77. Modular monolith

A primeira versão deve ser um **monólito modular**, não microserviços.

Mesmo sendo um único backend:

```text
Players
Teams
Competitions
Matches
Transfers
```

devem possuir fronteiras claras.

Microserviços podem ser considerados no futuro se houver necessidade real.

Não criar complexidade distribuída antes dela ser necessária.

---

# 78. Futuro crescimento

A arquitetura permite posteriormente:

```text
                    ┌── React
                    │
                    ├── Mobile
                    │
                    └── outro frontend
                           │
                           ▼
                    ASP.NET Core API
                           │
             ┌─────────────┴─────────────┐
             │                           │
           REST                       SignalR
             │                           │
             └─────────────┬─────────────┘
                           ▼
                       Services
                           ▼
                        Domain
                           ▼
                      Repositories
                           ▼
                      PostgreSQL
```

O motor continua independente.

---

# 79. Regra de ouro para o Copilot

O GitHub Copilot deve ser orientado a seguir estas regras:

1. Não colocar regra de negócio em Controller.
2. Não colocar regra de negócio em Hub.
3. Controller não chama Repository.
4. Hub não chama Repository.
5. Controller e Hub só trabalham com DTOs.
6. Service é responsável pela lógica de negócio/orquestração.
7. Repository é responsável por persistência.
8. Domain não depende de ASP.NET Core.
9. Domain não depende de SignalR.
10. Domain não depende de EF Core.
11. Domain não depende de PostgreSQL.
12. Frontend não é autoridade sobre o jogo.
13. PostgreSQL utiliza UUID/GUID.
14. Rotas REST usam nomes no singular.
15. Match Engine não conhece transporte ou persistência.
16. Eventos do domínio não são strings soltas.
17. A simulação deve ser testável com RNG determinístico.
18. Toda operação que altera o estado deve ser validada no backend.
19. O estado da partida deve permitir reconexão.
20. Multiplayer deve ser considerado desde o início.

---

# 80. Plano de implementação recomendado

A sequência completa é:

```text
FASE 1
Solution + projetos
        ↓
FASE 2
Domain básico
        ↓
FASE 3
EF Core + PostgreSQL
        ↓
FASE 4
Repositories
        ↓
FASE 5
Services
        ↓
FASE 6
REST + DTOs
        ↓
FASE 7
Match Engine em C#
        ↓
FASE 8
Match Events
        ↓
FASE 9
SignalR
        ↓
FASE 10
React + TypeScript
        ↓
FASE 11
Integração completa
        ↓
FASE 12
Testes de regressão
        ↓
FASE 13
Autenticação/autorização multiplayer
        ↓
FASE 14
Transferências
        ↓
FASE 15
Escala e otimização
```

---

# 81. Critério para considerar uma etapa concluída

Uma etapa só deve ser considerada concluída quando:

- compila;
- testes relevantes passam;
- responsabilidades estão respeitadas;
- não há acesso indevido entre camadas;
- contratos estão documentados;
- migrations estão versionadas quando houver alteração de banco;
- o código está preparado para a próxima etapa.

---

# 82. Primeira tarefa prática no VS Code

A primeira tarefa deve ser criar a solução e os projetos.

Depois configurar as referências:

```text
Api
 ├── Application
 └── Infrastructure

Application
 └── Domain

Infrastructure
 ├── Application
 └── Domain

Domain
 └── nenhum projeto
```

Em seguida:

1. configurar PostgreSQL;
2. configurar EF Core;
3. criar DbContext;
4. criar entidades iniciais;
5. criar configurações EF;
6. criar primeira migration;
7. criar repositories;
8. criar Services;
9. criar primeiros DTOs;
10. criar Controllers;
11. criar primeiros testes.

Somente depois disso começar a portar o Match Engine.

---

# 83. Estado final esperado da primeira milestone

A primeira milestone não precisa ter o jogo completo.

Ela deve provar que a arquitetura funciona.

O objetivo é conseguir:

```text
React
  ↓
GET /team
  ↓
Controller
  ↓
TeamService
  ↓
TeamRepository
  ↓
EF Core
  ↓
PostgreSQL
  ↓
TeamRepository
  ↓
TeamService
  ↓
TeamDto
  ↓
Controller
  ↓
React
```

E, paralelamente:

```text
React
  ↓
SignalR
  ↓
MatchHub
  ↓
MatchService
  ↓
MatchEngine
  ↓
MatchEvent
  ↓
DTO
  ↓
SignalR
  ↓
React
```

Quando esses dois fluxos estiverem funcionando corretamente, teremos uma base sólida para migrar o jogo inteiro.

---

# 84. Princípio final

O objetivo não é apenas transformar o HTML atual em React e mover o JavaScript para C#.

O objetivo é transformar o protótipo em um **produto com arquitetura de software sustentável**.

A divisão fundamental será:

```text
React
    = interface

Controller / SignalR
    = transporte

Services
    = casos de uso + lógica de negócio/orquestração

Domain
    = regras do futebol + simulação

Repository
    = persistência

EF Core
    = mapeamento

PostgreSQL
    = armazenamento
```

Nenhuma camada deve assumir a responsabilidade da outra.

Essa separação é o principal requisito arquitetural do projeto.
