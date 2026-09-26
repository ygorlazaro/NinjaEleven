using System.Globalization;
using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// What the match says about itself.
///
/// The prototype narrated a match by formatting one sentence per event, and the result
/// read like a spreadsheet with a pulse: the same two constructions a hundred times, in the
/// same voice, saying the same thing. A manager who watches ninety minutes of that stops
/// reading it, and the feed is the part of a football match that is supposed to be worth
/// reading.
///
/// So every beat has several ways of saying it and one of them is drawn per beat. The
/// variety is not decoration: a match is not a script, and the same man carrying the ball at
/// minute 3 and again at minute 80 is two different moments that happen to share a name.
/// The voice is a commentator who has watched too many matches and has opinions about them.
///
/// The lines take their names as {0}, {1} and so on, and they are composed with
/// <see cref="string.Format(IFormatProvider, string, object?)"/> so a player called
/// "Neto {0}" cannot break them. They are written with the accents Portuguese needs, because
/// a feed without them does not look like a mistake in a game — it looks like a game that
/// cannot spell.
/// </summary>
public static class MatchNarration
{
    /// <summary>
    /// Draws one line out of a set. The draw comes from the match's own random source, so
    /// a replayed match says the same things in the same order.
    /// </summary>
    public static string Say(IRandomSource random, params string[] options) =>
        options[random.Next(0, options.Length)];

    /// <summary>
    /// Draws one line and puts the names into it.
    /// </summary>
    public static string Say(IRandomSource random, string[] options, params object?[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Say(random, options), arguments);

    // --- A partida ---------------------------------------------------------------

    public static readonly string[] KickOff =
    {
        "Bola rolando em {0}! A torcida já gritou o primeiro nome do camisa.",
        "Começou! {0} dá o primeiro toque e a partida pega fogo.",
        "E bola rolando no {0}. Quem vier com mais fome leva.",
        "Apitou! {0} contra o mundo nos próximos noventa minutos.",
        "Vamos ao jogo! {0} começa atacando e a noite fica mais longa.",
    };

    public static readonly string[] Lineup =
    {
        "Escalação do {0}: {1}.",
        "O {0} sai com {1}.",
        "Onze do {0}: {1}. É esse time que começa a partida.",
        "O técnico do {0} não mudou nada: {1}.",
    };

    public static readonly string[] HalfTime =
    {
        "Fim do primeiro tempo em {0}. Os jogadores ainda estão se arrumando.",
        "Intervalo! {0}. O técnico da {1} tem uma palavra só na cabeça.",
        "Apito da metade: {0}. Quem quiser o segundo tempo já sabe por onde começar.",
        "Pausa, placar de {0} e um terço de jogo ainda pela frente.",
    };

    public static readonly string[] SecondHalf =
    {
        "Bola rolando de novo! O segundo tempo costuma cobrar os cansados.",
        "Voltamos! {0}, e agora a parte em que o jogo muda de figura.",
        "Recomeça a partida em {0}. As trocas são suas.",
        "Voltou ao gramado. {0}: ainda dá para virar esse jogo.",
        "Bola de novo no meio de campo. {0} ainda tem muito segundo tempo pela frente.",
    };

    public static readonly string[] FullTime =
    {
        "Fim de jogo! {0}. Agora é fazer a soma e dormir.",
        "Apitou pela última vez: {0}.",
        "Acabou! {0} nos noventa minutos.",
        "Fim! {0}. Alegria para um lado, decepção para o outro.",
    };

    // --- A bola sendo jogada ----------------------------------------------------

    public static readonly string[] BuildUp =
    {
        "{0} puxa a bola e vai em frente; o time inteiro acompanha.",
        "{0} recebe e olha para cima. Tem gente passando por baixo.",
        "{0} domina de costas e tenta sair jogando. Confiança.",
        "A {0} puxa a jogada pela direita, e {1} acompanha pelo meio.",
        "{0} é fácil aqui: sai driblando, acelera e leva a marcação junto.",
        "{0} afasta a bola do pé e abre o corpo. Circulação de primeira.",
        "{0} leva a bola na caneta e faz a linha inteira subir um metro.",
    };

    public static readonly string[] PassFoundRunner =
    {
        "{0} encontra {1} em velocidade, e a jogada fica perigosa de uma vez.",
        "Bela passe de {0}, e {1} aparece na cara da área.",
        "{0} mediu o tempo certinho e achou {1} livre.",
        "Encheu o pé! {0} achou {1}, que entrou direto.",
        "{0} cruzou rente, e {1} apareceu de primeira.",
    };

    public static readonly string[] DuelWon =
    {
        "{0} ganhou a disputa de {1} e fica com a bola.",
        "{0} bateu no corpo de {1} e saiu jogando. Bola mantida.",
        "{0} leu a jogada antes de {1} e ficou com a posse.",
        "Disputa dura, mas {0} saiu na frente de {1}.",
        "{0} impôs o ritmo e deixou {1} sem resposta.",
    };

    public static readonly string[] DuelLost =
    {
        "{0} desarma {1} e sai jogando para o outro lado.",
        "{0} tirou a bola de {1} com muita categoria.",
        "{0} esperou {1} dormir e roubou a bola.",
        "Corta! {0} roubou de {1} e limpou o lance.",
        "{0} chegou primeiro, e {1} ficou olhando para o chão.",
    };

    // --- Finalização -------------------------------------------------------------

    public static readonly string[] ShotOffTarget =
    {
        "{0} arriscou de longe e mandou por cima do gol.",
        "{0} bateu de primeira, e a bola foi para fora, à direita.",
        "{0} pegou mal na bola e levantou para a arquibancada.",
        "Que individuada! {0} bateu torto e pegou na trave de fora.",
        "{0} tentou o toque por cobertura. Passou raspando.",
    };

    public static readonly string[] ShotPressed =
    {
        "{0} recebeu e finalizou com alguém em cima: mandou para fora.",
        "Chegou perto! {0} bateu apertado, e a bola saiu pela linha de fundo.",
        "{0} tentou o giro, e o defensor empurrou a bola para fora.",
        "{0} finalizou na pressa, com a marcação colada.",
        "{0} pegou a bola no contra-ataque, mas a marcação estava em cima.",
    };

    public static readonly string[] Save =
    {
        "{0} espalmou para escanteio. Que defesa.",
        "Olha o goleiro! {0} voou e tirou o que ia ser gol.",
        "{0} saiu no tempo certo e segurou firme.",
        "Que defesa de {0}. A bola bateu na mão dele e saiu.",
        "{0} fez a defesa do jogo até agora. Fechou o ângulo.",
    };

    public static readonly string[] SaveAndHoldRebound =
    {
        "{0} pegou a sobra, mas {1} fechou o ângulo de novo.",
        "O rebote sobrou, e {0} abutiu a segunda. Seguro.",
        "Duas defesas do mesmo goleiro em dois segundos. Coisa de campeão.",
        "{0} salvou duas vezes seguidas. A torcida dele quase para.",
    };

    // --- Gol ---------------------------------------------------------------------

    // A goal scored by the club says "GOL!" and never "GOL DO {clube}". That phrase is
    // reserved for <see cref="GoalOwn"/>, where it names the club that *benefited* from a
    // defender's mistake — and a feed that used it for an ordinary goal printed the words
    // of an own goal over the name of the man who had just scored, which reads as the
    // exact opposite of what happened.
    public static readonly string[] Goal =
    {
        "GOL! {1} apareceu na hora certa e mandou para a rede.",
        "GOL! {1} pegou a bola na segunda trave, e {0} explode em festa.",
        "Balançou a rede! {1} finalizou com muita frieza, e {0} sai pulando.",
        "GOL! Que jogada, que finalização de {1}!",
        "Entrou! {1} fez a parte dele, e o {0} vira o jogo.",
        "GOL! {0} não acreditou quando a bola entrou.",
        "GOOOL! {1} deixou o goleiro no chão e mandou para o fundo da rede.",
    };

    public static readonly string[] GoalRebound =
    {
        "REBOTE! {1} pegou a sobra e mandou para a rede. GOL!",
        "A defesa não deu conta do rebote, e {1} pegou a segunda. GOL!",
        "GOL! {1} ficou com a sobra e não perdoou.",
        "De novo na segunda trave! {1} finalizou, e foi GOL.",
    };

    public static readonly string[] GoalOwn =
    {
        "GOL CONTRA! {0} tentou descartar, e a bola foi para a própria rede.",
        "GOL CONTRA de {0}! Ele tentou defender, e a bola enganou todo mundo, inclusive ele.",
        "GOL DO {1}! Só que a jogada era de {0}, que tentou descartar e mandou para a rede.",
        "GOL CONTRA! {0} olhou para o outro lado, e a bola encontrou o caminho dela.",
        "Que persiste! {0} tentou jogar, e o goleiro até agradeceu.",
    };

    // --- Faltas, cartões e escanteios --------------------------------------------

    public static readonly string[] Foul =
    {
        "Falta dura de {0} em cima de {1}. O árbitro não gostou.",
        "{0} chegou atrasado e pegou {1}. Falta de cartão.",
        "Falta de {0}! {1} caiu no gramado e olhou para o árbitro.",
        "{0} segurou {1} e parou o jogo. Amarelo na visão.",
        "Falta! {0} derrubou {1} sem querer, e o gramado já tinha avisado.",
        "{0} escorregou em cima de {1}. Depois de tanta falta?",
    };

    public static readonly string[] Corner =
    {
        "Escanteio para {0}. Bola na área, e a torcida levanta junto.",
        "Escanteio! {0} vai cobrar com a torcida empurrando.",
        "Corta! {0} ganha o escanteio e aperta o lado do gol.",
        "Escanteio para {0}, e o goleiro já sabe que vai apanhar.",
    };

    public static readonly string[] YellowCard =
    {
        "Amarelo para {0}! Ele reclamou, e o árbitro não gostou da reclamação.",
        "Cartão amarelo para {0}. Agora está no limite.",
        "{0} leva amarelo. Cuidado com o segundo.",
        "Amarelo! {0} foi por demais e parou o contra-ataque na hora.",
    };

    public static readonly string[] RedCard =
    {
        "VERMELHO DIRETO! {0} acertou o adversário e foi expulso. {1} fica com um a menos.",
        "EXPULSO! {0} acertou {1} com força e não teve segunda chance.",
        "Vermelho para {0}! O jogador vai para o vestiário. {1} joga com dez.",
        "Caiu para {1}! {0} acertou forte demais e viu o vermelho bem na cara.",
    };

    public static readonly string[] SecondYellow =
    {
        "Segundo amarelo! {0} está expulso. {1} joga com dez agora.",
        "De novo! {0} vira o jogo e vai para o chuveiro. {1} fica a favor dele e contra o resto.",
        "Vermelho por acúmulo! {0} perdeu a chance, e {1} ficou com dez.",
    };

    // --- Lesões ------------------------------------------------------------------

    public static readonly string[] InjuryLight =
    {
        "{0} sentiu uma fisgada, mas fica em campo. Dá para continuar.",
        "Ai! {0} torceu o tornozelo e levantou. Segue jogando.",
        "{0} reclamou da coxa, mas o Viva diz que dá para levar uma.",
        "Dor na panturrilha de {0}, mas ele não sai.",
        "{0} cambaleou, respirou fundo e voltou para o jogo.",
    };

    public static readonly string[] InjuryGrave =
    {
        "{0} não segue o jogo. Deu algo no joelho, e não há condições de voltar.",
        "Péssima notícia para {1}: {0} torceu, e vai ter que sair.",
        "{0} caiu sozinho e pediu substituição. A cara dele é de quem sentiu demais.",
        "Ai, que susto! {0} levantou segurando a perna, e o time já prepara a troca.",
    };

    // --- Goleiro improvisado -----------------------------------------------------

    public static readonly string[] KeeperPromoted =
    {
        "O {0} ficou sem goleiro! {1} teve que pegar as luvas e ir para o gol.",
        "Fica o registro: {1}, que não é goleiro, vai defender a meta do {0}.",
        "{0} com a meta desguarnecida. {1} foi o menos mau da situação.",
    };

    // --- Pênaltis ----------------------------------------------------------------

    public static readonly string[] PenaltyAwarded =
    {
        "PÊNALTI pro {0}! O árbitro viu a falta dentro da área e apontou para o meio.",
        "Pênalti! O árbitro marcou a infração e mandou todo mundo para fora da área.",
        "É pênalti pro {0}. Não teve conversa, o árbitro viu claro e apontou.",
    };

    public static readonly string[] PenaltyScored =
    {
        "GOOOL! {0} bateu com frieza e mandou no canto. GOL!",
        "Pênalti convertido! {0} esperou o goleiro cair e tocou na bola.",
        "Firme! {0} bateu no meio do gol e fez o ponto.",
        "Cobrou, e foi gol! {0} não tremeu na marca do pênalti.",
    };

    public static readonly string[] PenaltyMissed =
    {
        "PERDEU! {0} bateu mal, e {1} defendeu.",
        "Que defesa! {0} cobrou, e {1} voou para fazer a defesa.",
        "Isolou! {0} pegou mal na bola e mandou por cima do gol.",
        "Pênalti perdido! {0} bateu no goleiro, e a torcida suspira.",
    };

    // --- Acréscimos --------------------------------------------------------------

    public static readonly string[] StoppageFirst =
    {
        "O árbitro anuncia {0} minutos de acréscimo no primeiro tempo.",
        "Vão ser {0} minutos de acréscimo. A bola não para mais.",
        "Acréscimos: {0} minutos no primeiro tempo. Preparem os pulmões.",
    };

    public static readonly string[] StoppageSecond =
    {
        "Segundo tempo: {0} minutos de acréscimo. Ainda dá tempo de resolver.",
        "O árbitro marca {0} minutos de acréscimo. Quem ainda tem perna usa.",
        "Mais {0} minutos. Vamos até o fim.",
    };

    // --- Substituições -----------------------------------------------------------

    public static readonly string[] SubstitutionForInjury =
    {
        "{0} entra no lugar de {1}, que não tem condições de continuar.",
        "Mexe {0} no time: {1} sentiu algo, e vai para o vestiário.",
        "Troca rápida! {0} entra, e {1} deixa o gramado.",
        "O técnico mexe: entra {0} no lugar de {1}, que se machucou.",
    };

    public static readonly string[] SubstitutionForFatigue =
    {
        "{0} entra no lugar de {1}, que está no limite.",
        "Mexe {0} no time. {1} deu o que tinha.",
        "Troca de {0} no lugar de {1}: o camisa 11 precisa descansar.",
        "{1} pediu para sair. Entra {0}, e o time respira.",
    };

    public static readonly string[] SubstitutionTactical =
    {
        "{0} entra no lugar de {1}. Mudança de sistema.",
        "O técnico mexe no time: {0} no lugar de {1}.",
        "Troca! {0} entra para mudar a cara do time, e {1} sai.",
        "Ajuste no meio-campo: {0} entra no lugar de {1}.",
    };

    public static readonly string[] SubstitutionRisk =
    {
        "{0} entra no lugar de {1}, que já tinha amarelo. O risco não deu bobeira.",
        "Mexe {0} no time. {1} estava no limite, e o técnico protegeu o zagueiro.",
        "Troca preventiva: entra {0}, sai {1}, que o cartão amarelo pairava no ar.",
    };

    // --- Intervalo ---------------------------------------------------------------

    public static readonly string[] HalftimeNoChanges =
    {
        "Os dois técnicos mantiveram os times no intervalo.",
        "Nenhuma troca no intervalo: os onzes saíram como entraram.",
        "Sem mexer em nada, os dois mantiveram a escalação.",
    };

    public static readonly string[] HalftimeChanges =
    {
        "{0}",
        "Mexidas no intervalo: {0}.",
        "As substituições do intervalo: {0}.",
    };
}
