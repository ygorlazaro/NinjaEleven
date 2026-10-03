import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SeasonApi, TransferApi, TeamApi } from '@/api';
import { ApiProblemError } from '@/api/client';
import { useGameState } from '@/state';
import { useOffer } from '@/state/OfferProvider';
import { formatLimo } from '@/services/limo';
import { starsToString, positionLabel, attributeToneClass, energyTextClass } from '@/services/formatters';
import { PlayerName, ClubName } from '@/components/Common/Names';
import PlayerBox from '@/components/Common/PlayerBox';
import TransferRankingList from '@/components/Transfer/TransferRankingList';
import type {
  SeasonDto,
  TransferSearchFilters,
  TransferSearchResultDto,
  TransferListingDto,
  PlayerClubCareerLineDto,
  TransferInboxDto,
  TransferProposalDto,
  TransferHistoryLineDto,
  Position,
  TransferStatus,
  ClubBalanceDto,
   DivisionRecentTransfersDto,
   TransferRankingsDto,
   TransferRankingEntryDto,
} from '@/types';

const POSITIONS: Position[] = ['GK', 'DEF', 'MID', 'ATT'];
const STATUS_COLOR: Record<TransferStatus, string> = {
  Pending: 'pending',
  Accepted: 'accepted',
  Rejected: 'rejected',
  Completed: 'completed',
  Expired: 'expired'
};
const STATUS_LABELS: Record<TransferStatus, string> = {
  Pending: 'Pendente',
  Accepted: 'Aceita',
  Rejected: 'Recusada',
  Completed: 'Concluída',
  Expired: 'Expirada'
};

/**
 * Money and counts that the answer did not carry, said as absent rather than as zero.
 *
 * <p>
 * A missing market value is not a player worth nothing, a missing asking price is not a free
 * transfer and a missing career block is not a man who never kicked a ball. Each of them was
 * read as <c>?? 0</c>, so a screen that had not been told the price was showing a price — and
 * the per-season card counts beside them are the one place a zero is honest, because a season
 * line that says nothing about a card is a season in which he collected none.
 * </p>
 */
const money = (value?: number | null): string => (value == null ? '—' : formatLimo(value));
const count = (value?: number | null): string => (value == null ? '—' : String(value));

/** One box of the filter row, read as a number the manager typed. */
const numberFilter = (value: string): number | undefined =>
  value === '' ? undefined : Number(value);

/**
 * One man on the market, as a box rather than as a row.
 *
 * The market was the worst of the three tables to read: twenty columns, and the two a manager
 * reads first — his face and his name — were two cells in the middle of it with everything else
 * on either side of them. A market is scanned, not compared down a column, so the shape that
 * suits it is the one a manager browses a shop in.
 */
const PlayerBoxListing: React.FC<{
  player: TransferListingDto;
  onSelect: (player: TransferListingDto) => void;
}> = ({ player, onSelect }) => (
  <div className="squad-box squad-box--target" onClick={() => onSelect(player)}>
    <PlayerBox
      playerId={player.playerId}
      name={player.name}
      face={player.face}
      age={player.age}
      position={player.position}
      stars={player.stars}
      attributes={{
        speed: player.speed,
        accuracy: player.accuracy,
        dribbling: player.dribbling,
        heading: player.heading,
        strength: player.strength,
        goalkeeperPower: player.goalkeeperPower,
        reflexes: player.reflexes
      }}
      keeper={player.position === 'GK'}
      injury={player.injury}
      injuryMatchesRemaining={player.injuryMatchesRemaining}
      retiring={player.retiring}
      energy={player.energy}
      team={
        player.teamId
          ? {
              teamId: player.teamId,
              name: player.teamName,
              primaryColor: player.teamPrimaryColor,
              secondaryColor: player.teamSecondaryColor
            }
          : null
      }
      money={{ value: player.marketValue, price: player.askingPrice, salary: player.salary }}
      className={player.injury !== 'None' ? 'injured' : ''}
    >
      {/* Three facts about the deal rather than about the man: he is on no club, somebody has
          a proposal on him, or his own club has listed him. The table said all three in the
          name cell; they belong beside the club's name, which is the thing they are about. */}
      {player.isFreeAgent && <span className="free-agent">Sem clube</span>}
      {player.hasActiveProposal && (
        <span className="proposal-mark" title="Já há uma proposta na mesa por este jogador">📝</span>
      )}
      {player.onTransferList && (
        <span className="transfer-list-mark" title="Jogador listado para transferência">📋</span>
      )}

      {/* What he has done this season, read from the same season line the profile reads. */}
      <span title="Gols na temporada">
        <span className="player-box__tag">Gols</span> {player.season?.goals ?? 0}
      </span>
      <span title="Defesas na temporada">
        <span className="player-box__tag">Defs</span> {player.season?.saves ?? 0}
      </span>
      <span>
        <span className="player-box__tag">Cartões</span> {player.season?.yellowCards ?? 0}🟨{' '}
        {player.season?.redCards ?? 0}🟥
      </span>
      {/* What the ceiling on his reading is worth to a club buying him: a nineteen-year-old at
          fifty-four is not a signing, and a nineteen-year-old at fifty-four with a potential of
          eighty-eight is a different one. */}
      <span title="Teto que a leitura dele pode alcançar">
        <span className="player-box__tag">Potencial</span> {player.potential}
      </span>
    </PlayerBox>
  </div>
);

/**
 * One proposal, as a box: the same shape the market is a grid of, because it is the same man.
 *
 * The club on the box is the one *selling*, which is the club a manager has to phone before he
 * accepts — the buying club's own name is in the deal line, where the two of them and the fee
 * are read together rather than as two separate facts a box would have to choose between.
 */
const ProposalBox: React.FC<{
  proposal: TransferProposalDto;
  /** The answer, offered only when the deal is waiting for one and the club may give it. */
  onAnswer?: (proposal: TransferProposalDto, accept: boolean) => void;
  answering?: boolean;
}> = ({ proposal, onAnswer, answering = false }) => (
  <PlayerBox
    playerId={proposal.playerId}
    name={proposal.playerName}
    face={proposal.player?.face}
    age={proposal.playerAge}
    position={proposal.playerPosition}
    stars={proposal.player?.stars}
    attributes={{
      speed: proposal.player?.speed ?? 0,
      accuracy: proposal.player?.accuracy ?? 0,
      dribbling: proposal.player?.dribbling ?? 0,
      heading: proposal.player?.heading ?? 0,
      strength: proposal.player?.strength ?? 0,
      goalkeeperPower: proposal.player?.goalkeeperPower ?? 0,
      reflexes: proposal.player?.reflexes ?? 0
    }}
    keeper={proposal.playerPosition === 'GK'}
    energy={proposal.player?.energy ?? 0}
    team={
      proposal.sellingClubId
        ? { teamId: proposal.sellingClubId, name: proposal.sellingClubName }
        : null
    }
    className={`status-${STATUS_COLOR[proposal.status]}`}
  >
    {/* The deal, in the three numbers that make it a deal: who has it, what it costs, and when
        the man walks in. Status and date are beside them because a proposal is a fact in time
        and a status without a date is a mood. */}
    <span className={`status-badge status-${STATUS_COLOR[proposal.status]}`}>
      {STATUS_LABELS[proposal.status]}
    </span>
    <span>
      <span className="player-box__tag">Vendedor</span>{' '}
      {proposal.sellingClubId ? (
        <ClubName teamId={proposal.sellingClubId}>{proposal.sellingClubName}</ClubName>
      ) : (
        <span className="free-agent">{proposal.sellingClubName}</span>
      )}
    </span>
    <span>
      <span className="player-box__tag">Comprador</span>{' '}
      <ClubName teamId={proposal.buyingClubId}>{proposal.buyingClubName}</ClubName>
    </span>
    <span title="O que a proposta oferece">
      <span className="player-box__tag">Oferta</span>{' '}
      {proposal.fee ? formatLimo(proposal.fee) : '\u2014'}
    </span>
    <span title="Temporada e rodada em que o jogador chega">
      <span className="player-box__tag">Chega</span>{' '}
      {proposal.arrivalSeasonNumber}/{proposal.arrivalRoundNumber ?? '\u2014'}
    </span>
    <span>
      <span className="player-box__tag">Proposta</span>{' '}
      {new Date(proposal.proposedAt).toLocaleDateString('pt-BR')}
    </span>

    {/* A refusal the server would make is not offered: a man who is not pending has nothing to
        answer, and a live button on a settled deal is a control that exists to fail. */}
    {onAnswer && proposal.status === 'Pending' && (
      <span className="actions-col">
        <button
          className="ctrl btn-sm accept"
          disabled={answering}
          onClick={() => onAnswer(proposal, true)}
        >
          Aceitar
        </button>
        <button
          className="ctrl btn-sm reject"
          disabled={answering}
          onClick={() => onAnswer(proposal, false)}
        >
          Recusar
        </button>
      </span>
    )}
  </PlayerBox>
);

const HistoryRow: React.FC<{ line: TransferHistoryLineDto }> = ({ line }) => (
  <tr className={`history-row status-${STATUS_COLOR[line.status]}`}>
    <td>
      {line.sellingClubId ? (
        <ClubName teamId={line.sellingClubId}>{line.sellingClubName}</ClubName>
      ) : (
        <span className="free-agent">{line.sellingClubName}</span>
      )}
    </td>
    <td>
      <ClubName teamId={line.buyingClubId}>{line.buyingClubName}</ClubName>
    </td>
    <td className="num money">{line.fee ? formatLimo(line.fee) : '—'}</td>
    <td className="num contract-cell">
      {line.proposalSeasonNumber} → {line.arrivalSeasonNumber}
    </td>
    <td className="num">
      <span className={`status-badge status-${STATUS_COLOR[line.status]}`}>
        {STATUS_LABELS[line.status]}
      </span>
    </td>
    <td className="num">{line.completedAt ? new Date(line.completedAt).toLocaleDateString('pt-BR') : '—'}</td>
  </tr>
);

const ClubCareerRow: React.FC<{ line: PlayerClubCareerLineDto }> = ({ line }) => (
  <tr className="history-row">
    <td>
      <ClubName teamId={line.teamId}>{line.teamName}</ClubName>
    </td>
    <td className="num">{line.seasons}</td>
    <td className="num">{line.total.appearances}</td>
    <td className="num accent">{line.total.goals}</td>
    <td className="num">{line.total.saves}</td>
    <td className="num">{line.total.yellowCards}</td>
    <td className="num">{line.total.redCards}</td>
  </tr>
);

const PlayerDetail: React.FC<{
  player: TransferListingDto;
  history: TransferHistoryLineDto[];
  onClose: () => void;
  onOffer: (player: TransferListingDto) => void;
  onTransferListChange: (player: TransferListingDto, onList: boolean) => void;
}> = ({ player, history, onClose, onOffer, onTransferListChange }) => {
  const isKeeper = player.position === 'GK';
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const isOwnPlayer = !!player.teamId && player.teamId === selectedTeam?.id;

  return (
    <article className="transfer-detail">
      <header className="transfer-detail__head">
        <h3>
          <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
          {player.retiring && (
            <span className="retiring-mark" title="Aposentadoria declarada">🏁</span>
          )}
        </h3>
        <button className="ctrl transfer-detail__close" onClick={onClose} title="Fechar">✕</button>
      </header>

      {/*
        The offer is a window of its own, and the card only opens it.

        The card is three screens long — a man's career by club, every transfer he has ever made
        — and the offer used to sit at the top of it, which meant it was a block of controls
        bolted to a read. A window is the shape the action has everywhere else in the game: the
        same offer is opened from a profile, and it is the same window, not a second copy of the
        form with its own answer to whether he is spoken for. The button is here, under the name,
        because a manager who opened a player to make an offer should not have to read the man
        first.
      */}
      <div className="transfer-detail__actions">
        <button
          className="ctrl transfer-offer__button"
          onClick={() => onOffer(player)}
        >
          {player.isFreeAgent ? `Contratar ${player.name}` : `Fazer proposta por ${player.name}`}
        </button>
        {player.hasActiveProposal && (
          <p className="transfer-offer__hint">
            📝 Já há uma proposta na mesa por este jogador — ela espera resposta em Propostas na
            mesa, no alto da tela.
          </p>
        )}
        {isOwnPlayer && (
          <button
            className={`ctrl transfer-list-button ${player.onTransferList ? 'listed' : 'unlisted'}`}
            onClick={() => onTransferListChange(player, !player.onTransferList)}
            title={player.onTransferList ? 'Tirar da lista de transferência' : 'Listar para transferência'}
          >
            {player.onTransferList ? '📋 Fora da venda' : '📋 Para venda'}
          </button>
        )}
      </div>

      <div className="transfer-detail__grid">
        <div className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Informações</h4>
          <div className="transfer-detail__kv">
            <span className="transfer-detail__k">Posição</span>
            <span className="transfer-detail__v">{positionLabel(player.position)}</span>
            <span className="transfer-detail__k">Idade</span>
            <span className="transfer-detail__v">{player.age} anos</span>
            <span className="transfer-detail__k">Estrelas</span>
            <span className="transfer-detail__v stars-col">{starsToString(player.stars)}</span>
            <span className="transfer-detail__k">Energia</span>
            <span className={`transfer-detail__v ${energyTextClass(player.energy)}`}>
              {player.energy}
            </span>
            <span className="transfer-detail__k">Lesão</span>
            <span className="transfer-detail__v">
              {player.injury === 'None' ? 'Apto' : `${player.injury} (${player.injuryMatchesRemaining})`}
            </span>
            <span className="transfer-detail__k">Clube</span>
            <span className="transfer-detail__v">
              {player.teamId ? (
                <ClubName teamId={player.teamId}>{player.teamName ?? '—'}</ClubName>
              ) : (
                <span className="free-agent">Sem clube</span>
              )}
            </span>
            {player.retiring && (
              <>
                <span className="transfer-detail__k">Aposentadoria</span>
                <span className="transfer-detail__v retiring">Declarada 🏁</span>
              </>
            )}
          </div>
        </div>

        <div className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Atributos</h4>
          <div className="transfer-detail__kv">
            <span className="transfer-detail__k">Velocidade</span>
            <span className={`transfer-detail__v ${attributeToneClass(player.speed)}`}>{player.speed}</span>
            <span className="transfer-detail__k">Finalização</span>
            <span className={`transfer-detail__v ${attributeToneClass(player.accuracy)}`}>{player.accuracy}</span>
            <span className="transfer-detail__k">Drible</span>
            <span className={`transfer-detail__v ${attributeToneClass(player.dribbling)}`}>{player.dribbling}</span>
            <span className="transfer-detail__k">Cabeceio</span>
            <span className={`transfer-detail__v ${attributeToneClass(player.heading)}`}>{player.heading}</span>
            <span className="transfer-detail__k">Força</span>
            <span className={`transfer-detail__v ${attributeToneClass(player.strength)}`}>{player.strength}</span>
            {isKeeper && (
              <>
                <span className="transfer-detail__k">Defesa de gol</span>
                <span className={`transfer-detail__v ${attributeToneClass(player.goalkeeperPower)}`}>
                  {player.goalkeeperPower}
                </span>
                <span className="transfer-detail__k">Reflexos</span>
                <span className={`transfer-detail__v ${attributeToneClass(player.reflexes)}`}>
                  {player.reflexes}
                </span>
              </>
            )}
          </div>
        </div>

        <div className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Financeiro</h4>
          <div className="transfer-detail__kv">
            <span className="transfer-detail__k">Valor de mercado</span>
            <span className="transfer-detail__v">{money(player.marketValue)}</span>
            <span className="transfer-detail__k">Preço de saída</span>
            <span className="transfer-detail__v">
              {player.isFreeAgent
                ? `Contratação — ${money(player.askingPrice)}`
                : money(player.askingPrice)}
            </span>
            <span className="transfer-detail__k">Salário</span>
            <span className="transfer-detail__v">{money(player.salary)}</span>
            <span className="transfer-detail__k">Contrato</span>
            <span className="transfer-detail__v">
              {player.teamId
                ? `${player.seasonsLeft}/${player.contractSeasons} temporadas`
                : '—'}
            </span>
          </div>
        </div>

        <div className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Temporada</h4>
          <div className="transfer-detail__kv">
            <span className="transfer-detail__k">Gols</span>
            <span className="transfer-detail__v accent">{player.season?.goals ?? 0}</span>
            <span className="transfer-detail__k">Defesas</span>
            <span className="transfer-detail__v">{player.season?.saves ?? 0}</span>
            <span className="transfer-detail__k">Amarelos</span>
            <span className="transfer-detail__v">{player.season?.yellowCards ?? 0}</span>
            <span className="transfer-detail__k">Vermelhos</span>
            <span className="transfer-detail__v">{player.season?.redCards ?? 0}</span>
          </div>
        </div>

        <div className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Carreira</h4>
          <div className="transfer-detail__kv">
            <span className="transfer-detail__k">Jogos</span>
            <span className="transfer-detail__v">{count(player.total?.appearances)}</span>
            <span className="transfer-detail__k">Titulares</span>
            <span className="transfer-detail__v">{count(player.total?.started)}</span>
            <span className="transfer-detail__k">Reservas</span>
            <span className="transfer-detail__v">{count(player.total?.cameOn)}</span>
            <span className="transfer-detail__k">Gols</span>
            <span className="transfer-detail__v accent">{count(player.total?.goals)}</span>
            <span className="transfer-detail__k">Amarelos</span>
            <span className="transfer-detail__v">{count(player.total?.yellowCards)}</span>
            <span className="transfer-detail__k">Vermelhos</span>
            <span className="transfer-detail__v">{count(player.total?.redCards)}</span>
          </div>
        </div>
      </div>

      {player.clubs.length > 0 && (
        <section className="transfer-detail__section">
          <h4 className="transfer-detail__section-title">Carreira por clube</h4>
          <table className="transfer-table club-squad-table">
            <thead>
              <tr>
                <th>Clube</th>
                <th className="num">Temp.</th>
                <th className="num">Jogos</th>
                <th className="num accent">Gols</th>
                <th className="num">Defs</th>
                <th className="num">Ama</th>
                <th className="num">Verm</th>
              </tr>
            </thead>
            <tbody>
              {player.clubs.map(line => (
                <ClubCareerRow key={line.teamId} line={line} />
              ))}
            </tbody>
          </table>
        </section>
      )}

      <section className="transfer-detail__section">
        <h4 className="transfer-detail__section-title">Transferências</h4>
        {history.length === 0 ? (
          <p className="transfer-empty">Nenhuma transferência registrada.</p>
        ) : (
          <table className="transfer-table club-squad-table">
            <thead>
              <tr>
                <th>Saiu de</th>
                <th>Foi para</th>
                <th className="num money">Valor</th>
                <th className="num">Temporadas</th>
                <th className="num">Status</th>
                <th className="num">Concluída</th>
              </tr>
            </thead>
            <tbody>
              {history.map((line, i) => (
                <HistoryRow key={`${line.sellingClubId ?? 'livre'}-${line.buyingClubId}-${i}`} line={line} />
              ))}
            </tbody>
          </table>
        )}
      </section>

    </article>
  );
};

/**
 * The four pages the market is read on, and the one a manager lands on.
 *
 * The market was five sections on one page — the search for new men, the club's balance, the
 * division's recent business, the proposals waiting for an answer and four rankings of the
 * division — and a manager opening it had to scroll past two ranking tables to reach the offers
 * addressed to his own club. Those are four different questions with four different urgencies, and
 * stacking them made the least urgent the most visible.
 *
 * The landing page is the search, because "what can I buy" is what a manager came for. The
 * balance travels with it rather than sitting above or below it, because what he can spend is
 * half of that same question.
 *
 * The tab travels in the query string rather than living in the component, for the same reason it
 * does on the table and the cup: a piece of state that exists only inside one screen cannot be
 * pointed at from outside it, so a link to "as propostas" is a link that opens on the proposals.
 */
type TransferTab = 'market' | 'proposals' | 'activity' | 'rankings';

const TAB_PARAM: Record<Exclude<TransferTab, 'market'>, string> = {
  proposals: 'proposals',
  activity: 'activity',
  rankings: 'rankings'
};

/**
 * Whether the `tab` the URL carries is a tab this screen has.
 *
 * A name that is not one of them is not an error and not a blank screen: it is a link somebody
 * typed or an old page whose tab has since been renamed, and it lands on the market like a screen
 * that had never been given one.
 */
const isTransferTab = (value: string | null): value is Exclude<TransferTab, 'market'> =>
  value === 'proposals' || value === 'activity' || value === 'rankings';

const TransferScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const selectedSeason = useGameState((s) => s.selectedSeason);
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedPlayerId = searchParams.get('player') ?? '';
  const [seasonId, setSeasonId] = useState<string>(selectedSeason?.id ?? '');
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);

  const [searchResult, setSearchResult] = useState<TransferSearchResultDto | null>(null);
  const [inbox, setInbox] = useState<TransferInboxDto | null>(null);
  const [answering, setAnswering] = useState<string | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [balance, setBalance] = useState<ClubBalanceDto | null>(null);
  const [recentTransfers, setRecentTransfers] = useState<DivisionRecentTransfersDto | null>(null);
  const [rankings, setRankings] = useState<TransferRankingsDto | null>(null);

  /**
   * Every filter the manager can set, and none of them set by default. An unset filter narrows
   * nothing, which is what makes several of them at once mean "these and these" rather than
   * "whichever the screen felt like".
   */
  const [filters, setFilters] = useState<TransferSearchFilters>({});
  const [page, setPage] = useState(1);

  /**
   * Bumped when something the market shows has changed — a proposal just made, above all — so
   * the list is asked again instead of showing a player as available a second after a club
   * agreed to have him.
   */
  const [searchNonce, setSearchNonce] = useState(0);

  /**
   * The offer window belongs to the game, not to this screen.
   *
   * A manager offers for the same player from here and from the profile he read him on, so the
   * window is mounted once and opened by id — two copies of the form would be two answers to
   * "is he spoken for", and they would stop agreeing the first time one of them was opened
   * before the other was refreshed.
   */
  const { openOffer, proposalNonce } = useOffer();

  /**
   * A proposal sent from the window changes what this screen is showing.
   *
   * The man is spoken for from the moment the offer is accepted by the backend, and the row
   * behind the window, the card and the "Propostas na mesa" table all still say he is available
   * until they are asked again. The nonce is the tell: the first value is the one the screen
   * mounted with, and only a change from it means somebody made an offer.
   */
  const lastProposalNonce = useRef(proposalNonce);

  useEffect(() => {
    if (proposalNonce === lastProposalNonce.current) return;
    lastProposalNonce.current = proposalNonce;
    setSearchNonce(nonce => nonce + 1);
  }, [proposalNonce]);

  const [selectedPlayer, setSelectedPlayer] = useState<TransferListingDto | null>(null);
  const [listing, setListing] = useState<TransferListingDto | null>(null);
  const [history, setHistory] = useState<TransferHistoryLineDto[]>([]);

  const filterCount = Object.values(filters).filter(v => v !== undefined && v !== false).length;

  /**
   * A player named in the url is a player the screen opens on.
   *
   * The card is reached from a profile — a manager reads a striker, decides he is the man the
   * club is missing, and wants to make an offer without hunting for him in a list of eight
   * hundred. The search that finds him is a different question from the offer, so the offer
   * travels in the url and the card is already open when the screen arrives.
   */
  useEffect(() => {
    if (!requestedPlayerId || !seasonId) return;

    let alive = true;

    TransferApi.getListing(requestedPlayerId, seasonId)
      .then(found => { if (alive) setSelectedPlayer(found); })
      .catch(() => { if (alive) setError('Não foi possível abrir este jogador no mercado.'); });

    return () => { alive = false; };
  }, [requestedPlayerId, seasonId]);

  useEffect(() => {
    SeasonApi.list()
      .then(list => {
        setSeasons(list);
        if (!seasonId) {
          const current = list.find(s => s.status === 'InProgress') ?? list[list.length - 1];
          if (current) setSeasonId(current.id);
        }
      })
      .catch(() => {});
  }, [seasonId]);

  useEffect(() => {
    if (!seasonId) return;

    let alive = true;

    const loadSearch = async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await TransferApi.search(seasonId, filters, page, 30);
        if (alive) setSearchResult(result);
      } catch (err) {
        if (alive) {
          setError(err instanceof Error ? err.message : 'Não foi possível carregar o mercado.');
        }
      } finally {
        if (alive) setLoading(false);
      }
    };

    loadSearch();
    return () => { alive = false; };
  }, [seasonId, filters, page, searchNonce]);

  useEffect(() => {
    if (!selectedTeam?.id || !seasonId) {
      setInbox(null);
      return;
    }

    TransferApi.getInbox(selectedTeam.id, seasonId)
      .then(setInbox)
      .catch(() => setInbox(null));
  }, [selectedTeam?.id, seasonId, answering, proposalNonce]);

  useEffect(() => {
    if (!selectedTeam?.id || !seasonId) {
      setBalance(null);
      setRecentTransfers(null);
      setRankings(null);
      return;
    }

    let alive = true;

    Promise.all([
      TeamApi.getBalance(selectedTeam.id),
      TransferApi.getRecent(selectedTeam.id, 3),
      TransferApi.getRankings(selectedTeam.id),
    ])
      .then(([balanceData, recentData, rankingsData]) => {
        if (alive) {
          setBalance(balanceData);
          setRecentTransfers(recentData);
          setRankings(rankingsData);
        }
      })
      .catch(() => {
        if (alive) {
          setBalance(null);
          setRecentTransfers(null);
          setRankings(null);
        }
      });

    return () => { alive = false; };
  }, [selectedTeam?.id, seasonId, searchNonce]);

  /**
   * The card opens on the row the manager clicked and is filled in from the backend: the row is
   * the market's summary and the card is the same player whole, with his career by club and his
   * transfer history. Both come from the same listing service, so the two views of a man cannot
   * disagree about him.
   */
  useEffect(() => {
    if (!selectedPlayer || !seasonId) {
      setListing(null);
      setHistory([]);
      return;
    }

    let alive = true;

    Promise.all([
      TransferApi.getListing(selectedPlayer.playerId, seasonId),
      TransferApi.getHistory(selectedPlayer.playerId)
    ])
      .then(([full, past]) => {
        if (!alive) return;
        setListing(full);
        setHistory(past);
      })
      .catch(() => {
        if (alive) {
          setListing(null);
          setHistory([]);
        }
      });

    return () => { alive = false; };
  }, [selectedPlayer?.playerId, seasonId, proposalNonce]);

  const handleAnswer = async (proposal: TransferProposalDto, accept: boolean) => {
    if (!selectedTeam?.id) return;

    setAnswering(proposal.transferId);
    try {
      await TransferApi.answer(proposal.transferId, selectedTeam.id, accept);
      setInbox(await TransferApi.getInbox(selectedTeam.id, seasonId));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível responder à proposta.');
    } finally {
      setAnswering(null);
    }
  };

  const setFilter = (key: keyof TransferSearchFilters, value: number | boolean | string | undefined) => {
    setPage(1);
    setFilters(current => ({ ...current, [key]: value }));
  };

  const resetFilters = () => {
    setPage(1);
    setFilters({});
  };

  const window = searchResult?.window ?? null;

  const incoming = useMemo(
    () => inbox?.incoming.filter(proposal => proposal.status === 'Pending') ?? [],
    [inbox]
  );

  /**
   * Which of the four pages is open. The market is the one a manager lands on, and a `tab` that
   * names no page of this screen lands there too.
   *
   * Opening a page is a navigation rather than a state change, and `replace` rather than `push`,
   * because a tab is not a place a manager has been: pressing back from the proposals should
   * leave the market, not walk back through the tabs he glanced at on the way there. The `player`
   * in the URL is carried along, because a manager who opened a man from his profile and then
   * went to read his club's offers must not have that man close under him.
   */
  const requestedTab = searchParams.get('tab');
  const activeTab: TransferTab = isTransferTab(requestedTab) ? requestedTab : 'market';

  const setActiveTab = (tab: TransferTab) => {
    setSearchParams(
      current => {
        const next = new URLSearchParams(current);
        if (tab === 'market') next.delete('tab');
        else next.set('tab', TAB_PARAM[tab]);
        return next;
      },
      { replace: true }
    );
  };

  if (!selectedTeam) {
    return (
      <div className="transfer-screen">
        <p className="transfer-empty">Selecione um clube para acessar o mercado de transferências.</p>
      </div>
    );
  }

  const handleTransferListChange = async (player: TransferListingDto, onList: boolean) => {
    if (!selectedTeam) return;
    try {
      const result = onList
      ? await TeamApi.putOnTransferList(selectedTeam.id, player.playerId, seasonId)
      : await TeamApi.takeOffTransferList(selectedTeam.id, player.playerId, seasonId);
      setSearchResult(prev => {
        if (!prev) return prev;
        return {
          ...prev,
          players: prev.players.map(p =>
            p.playerId === result.playerId ? { ...p, onTransferList: result.onTransferList } : p
          )
        };
      });
      setListing(prev =>
        prev && prev.playerId === player.playerId
          ? { ...prev, onTransferList: result.onTransferList }
          : prev
      );
      setSelectedPlayer(prev =>
        prev && prev.playerId === player.playerId
          ? { ...prev, onTransferList: result.onTransferList }
          : prev
      );
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setError(err.message);
      } else {
        setError('Não foi possível atualizar a lista de transferência.');
      }
    }
  };

  return (
    <div className="transfer-screen">
      <header className="transfer-header">
        <h2>Mercado de Transferências</h2>
        <div className="transfer-season">
          <label>Temporada:</label>
          <select
            value={seasonId}
            onChange={e => { setSeasonId(e.target.value); setPage(1); }}
            className="ctrl"
          >
            {seasons.map(s => (
              <option key={s.id} value={s.id}>{s.name} ({s.status})</option>
            ))}
          </select>
        </div>
        {window && (
          <p className="transfer-window-note">
            O mercado está aberto a qualquer momento — a janela decide quando o jogador chega, não
            se a proposta é aceita. O mundo está na rodada {window.currentRound}: uma contratação
            feita agora chega {window.arrivalLabel}.
          </p>
        )}
      </header>

      {/* The tab bar is the club's own: the same control the table and the cup are divided by,
          because a screen with two kinds of tab on it is a screen where the manager has to learn
          where the controls are twice.

          The count on the proposals tab is the whole reason the offers are not simply the first
          page: an offer nobody answers dies at the end of the season, so a page the manager has
          to go and find is a page of unanswered offers. Carrying the count on the tab says the
          same thing without the offers being on screen at all. */}
      <div className="tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'market'}
          className={`tab ${activeTab === 'market' ? 'active' : ''}`}
          onClick={() => setActiveTab('market')}
        >
          Mercado
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'proposals'}
          className={`tab ${activeTab === 'proposals' ? 'active' : ''}`}
          onClick={() => setActiveTab('proposals')}
        >
          Propostas
          {incoming.length > 0 && (
            <span className="tab-badge" title={`${incoming.length} a responder`}>
              {incoming.length}
            </span>
          )}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'activity'}
          className={`tab ${activeTab === 'activity' ? 'active' : ''}`}
          onClick={() => setActiveTab('activity')}
        >
          Movimentação
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'rankings'}
          className={`tab ${activeTab === 'rankings' ? 'active' : ''}`}
          onClick={() => setActiveTab('rankings')}
        >
          Rankings
        </button>
      </div>

      {/* The search that finds new men, and the club's balance above it: what he can spend is
          half of the same question as what he can spend it on, so they are the same page. The
          balance used to sit below a twenty-column table of players while its own comment said it
          belonged at the top, which is the kind of thing that is true in a comment and not on the
          screen. */}
      {activeTab === 'market' && (
        <section className="transfer-search-section">
          {balance && (
            <div className="transfer-balance-section">
              <div className="club-figures">
                <div className="club-figure club-figure--accent">
                  <span className="club-figure__icon">💰</span>
                  <span className="club-figure__value">{formatLimo(balance.balance)}</span>
                  <span className="club-figure__label">Saldo do clube</span>
                </div>
              </div>
            </div>
          )}

          <h3>Filtrar mercado {filterCount > 0 && `(${filterCount})`}</h3>
          <div className="transfer-filters">
            <div className="transfer-filter-row">
              <label>Posição:</label>
              <select
                value={filters.position ?? ''}
                onChange={e => setFilter('position', e.target.value || undefined)}
                className="ctrl"
              >
                <option value="">Todas</option>
                {POSITIONS.map(pos => (
                  <option key={pos} value={pos}>{positionLabel(pos)}</option>
                ))}
              </select>

              <label>Idade:</label>
              <input
                type="number"
                className="ctrl transfer-filter-number"
                min={16}
                max={50}
                placeholder="mín"
                value={filters.minAge ?? ''}
                onChange={e => setFilter('minAge', numberFilter(e.target.value))}
              />
              <span>–</span>
              <input
                type="number"
                className="ctrl transfer-filter-number"
                min={16}
                max={50}
                placeholder="máx"
                value={filters.maxAge ?? ''}
                onChange={e => setFilter('maxAge', numberFilter(e.target.value))}
              />

              <label>Estrelas ≥</label>
              <input
                type="number"
                step={0.5}
                min={0}
                className="ctrl transfer-filter-number"
                placeholder="★"
                value={filters.minStars ?? ''}
                onChange={e => setFilter('minStars', numberFilter(e.target.value))}
              />

              <label>
                <input
                  type="checkbox"
                  checked={filters.freeAgentsOnly ?? false}
                  onChange={e => setFilter('freeAgentsOnly', e.target.checked || undefined)}
                />
                {' '}Só livres
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={filters.withClubOnly ?? false}
                  onChange={e => setFilter('withClubOnly', e.target.checked || undefined)}
                />
                {' '}Com clube
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={filters.retiring ?? false}
                  onChange={e => setFilter('retiring', e.target.checked ? true : undefined)}
                />
                {' '}Aposentando
              </label>

              <button className="ctrl" onClick={resetFilters} disabled={filterCount === 0}>
                Limpar
              </button>
            </div>

            <details className="transfer-filter-row transfer-filter-attributes">
              <summary>Atributos mínimos</summary>
              <div className="transfer-filter-row">
                {([
                  ['minSpeed', 'Vel'],
                  ['minAccuracy', 'Fin'],
                  ['minDribbling', 'Dri'],
                  ['minHeading', 'Cab'],
                  ['minStrength', 'For'],
                  ['minGoalkeeperPower', 'Gol'],
                  ['minReflexes', 'Ref']
                ] as const).map(([key, label]) => (
                  <span key={key} className="transfer-filter-attribute">
                    <label>{label} ≥</label>
                    <input
                      type="number"
                      className="ctrl transfer-filter-number"
                      min={0}
                      max={20}
                      value={filters[key] ?? ''}
                      onChange={e => setFilter(key, numberFilter(e.target.value))}
                    />
                  </span>
                ))}
              </div>
            </details>
          </div>

          {error && <p className="transfer-error">{error}</p>}

          {loading ? (
            <p>Carregando...</p>
          ) : !searchResult?.players?.length ? (
            <p className="transfer-empty">Nenhum jogador encontrado.</p>
          ) : (
            <>
              <div className="player-box-grid">
                {searchResult.players.map(player => (
                  <PlayerBoxListing
                    key={player.playerId}
                    player={player}
                    onSelect={setSelectedPlayer}
                  />
                ))}
              </div>

              <p className="transfer-count">
                {searchResult.total} jogadores — página {searchResult.page} de {searchResult.totalPages}
              </p>

              {searchResult.totalPages > 1 && (
                <div className="pagination">
                  {Array.from({ length: searchResult.totalPages }, (_, i) => i + 1)
                    .filter(p => Math.abs(p - page) < 3 || p === 1 || p === searchResult.totalPages)
                    .map(p => (
                      <button
                        key={p}
                        className={`pagination-btn ${p === page ? 'active' : ''}`}
                        onClick={() => setPage(p)}
                      >
                        {p}
                      </button>
                    ))}
                </div>
              )}
            </>
          )}
        </section>
      )}

      {/* The offers addressed to this club, on a page of their own. They were buried under a
          twenty-column table of players and two ranking sections, and an offer nobody answers
          dies at the end of the season — so burying them was how offers piled up unread. They are
          not the landing page, because a manager opening the market is asking what he can buy;
          they are one click away and their count is on the tab. */}
      {activeTab === 'proposals' && (
        <section className="transfer-inbox-section">
          <h3>
            Propostas na mesa
            {incoming.length > 0 && (
              <span className="transfer-inbox-count">{incoming.length} a responder</span>
            )}
          </h3>

          {/* A tab that opens onto nothing is a broken tab, so the empty case is said rather than
              left blank: "nothing on the table" and "this page is broken" are different facts and
              a blank page would be read as the second one. */}
          {(!inbox || (inbox.incoming.length === 0 && inbox.outgoing.length === 0)) && (
            <p className="transfer-empty">
              Nenhuma proposta na mesa. As ofertas que você fizer aparecem aqui.
            </p>
          )}

          {!!inbox?.outgoing.length && (
            <>
              <h4>Enviadas</h4>
              {/* The same grid the market is, for the same reason: these are the same men, and a
                  proposal the manager made is a man he has already read. */}
              <div className="player-box-grid">
                {inbox.outgoing.map(p => (
                  <ProposalBox key={p.transferId} proposal={p} />
                ))}
              </div>
            </>
          )}

          {!!inbox?.incoming.length && (
            <>
              <h4>Recebidas</h4>
              {/* The answer is offered here and not on the outgoing side, because a proposal sent
                  is not waiting for the sender to answer it. The box carries the club that has to
                  be convinced on its own line, which is the club a manager has to ring. */}
              <div className="player-box-grid">
                {inbox.incoming.map(p => (
                  <ProposalBox
                    key={p.transferId}
                    proposal={p}
                    onAnswer={handleAnswer}
                    answering={answering === p.transferId}
                  />
                ))}
              </div>
            </>
          )}
        </section>
      )}

      {/*
        Recent transfers in the division — what happened in the last three rounds
        across every club in the manager's division. The section is drawn even when the
        list is empty: a heading that vanishes says nothing, and "nobody moved" is a
        different fact from "this screen is broken".
      */}
      {activeTab === 'activity' && (
        <section className="transfer-recent-section">
          <h3>
            Transferências recentes na divisão (últimas {recentTransfers?.windowRounds ?? 3} rodadas)
          </h3>
          {/* The same grid, once more: a division's recent business is a list of men who moved,
              and the men are the same men the market is a grid of. */}
          <div className="player-box-grid">
            {(recentTransfers?.transfers ?? []).map((line, i) => (
              <PlayerBox
                key={`${line.sellingClubId ?? 'livre'}-${line.buyingClubId}-${i}`}
                playerId={line.playerId}
                name={line.playerName}
                face={line.player?.face}
                age={line.playerAge}
                position={line.playerPosition}
                stars={line.player?.stars}
                attributes={{
                  speed: line.player?.speed ?? 0,
                  accuracy: line.player?.accuracy ?? 0,
                  dribbling: line.player?.dribbling ?? 0,
                  heading: line.player?.heading ?? 0,
                  strength: line.player?.strength ?? 0,
                  goalkeeperPower: line.player?.goalkeeperPower ?? 0,
                  reflexes: line.player?.reflexes ?? 0
                }}
                keeper={line.playerPosition === 'GK'}
                energy={line.player?.energy ?? 0}
                team={
                  line.buyingClubId
                    ? { teamId: line.buyingClubId, name: line.buyingClubName }
                    : null
                }
                money={{ price: line.fee }}
                className={`status-${STATUS_COLOR[line.status]}`}
              >
                {/* The move itself, in the order it happened: he left one club and arrived at
                    another, for a fee, on a round. */}
                <span className={`status-badge status-${STATUS_COLOR[line.status]}`}>
                  {STATUS_LABELS[line.status]}
                </span>
                <span>
                  <span className="player-box__tag">Saiu de</span>{' '}
                  {line.sellingClubId ? (
                    <ClubName teamId={line.sellingClubId}>{line.sellingClubName}</ClubName>
                  ) : (
                    <span className="free-agent">{line.sellingClubName}</span>
                  )}
                </span>
                <span>
                  <span className="player-box__tag">Foi para</span>{' '}
                  <ClubName teamId={line.buyingClubId}>{line.buyingClubName}</ClubName>
                </span>
                <span title="Rodada em que ele chegou">
                  <span className="player-box__tag">Rodada</span>{' '}
                  {line.arrivalRoundNumber ? `${line.arrivalRoundNumber}\u00aa` : '\u2014'}
                </span>
              </PlayerBox>
            ))}
          </div>

          {/* The empty case is said rather than left blank: "nobody moved in the division" and
              "this page is broken" are different facts and a blank page reads as the second. */}
          {recentTransfers && recentTransfers.transfers.length === 0 && (
            <p className="transfer-empty">
              Nenhum clube da divisão contratou ou liberou jogador nas últimas {recentTransfers.windowRounds} rodadas.
            </p>
          )}
        </section>
      )}

      {/*
        The four transfer rankings of the division: most players bought, most sold, most
        money spent, and most profit. Profit is net — fees received minus fees paid — so
        a sell-on that covers two losses appears above a club that bought five for peanuts.
        A page of its own, because four tables under a search form is a wall and a wall is
        the reason a manager stopped reading the division's business.
      */}
      {activeTab === 'rankings' && rankings && (
        <section className="transfer-rankings-section">
          <div className="transfer-rankings-grid">
            <TransferRankingList
              title="Mais contratações"
              entries={rankings.mostBought}
              metricLabel="contratações"
              formatMetric={n => String(n)}
            />
            <TransferRankingList
              title="Mais vendidas"
              entries={rankings.mostSold}
              metricLabel="vendas"
              formatMetric={n => String(n)}
            />
            <TransferRankingList
              title="Mais gastos"
              entries={rankings.mostSpent}
              metricLabel="gasto"
              formatMetric={n => formatLimo(n)}
            />
            <TransferRankingList
              title="Mais lucro"
              entries={rankings.mostProfit}
              metricLabel="lucro"
              formatMetric={n => formatLimo(n)}
            />
          </div>
        </section>
      )}


      {selectedPlayer && (
        <PlayerDetail
          player={listing ?? selectedPlayer}
          history={history}
          onClose={() => {
            setSelectedPlayer(null);
            setListing(null);
            setHistory([]);
            // The player leaves the url with the card, or closing it would reopen itself on
            // the next render and the close button would do nothing a manager could see.
            if (searchParams.has('player')) {
              setSearchParams({}, { replace: true });
            }
          }}
          onOffer={player => openOffer({ playerId: player.playerId, listing: player, window, seasonId })}
          onTransferListChange={handleTransferListChange}
        />
      )}
    </div>
  );
};

export default TransferScreen;
