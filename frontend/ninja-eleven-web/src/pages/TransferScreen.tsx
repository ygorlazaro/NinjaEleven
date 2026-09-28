import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SeasonApi, TransferApi } from '@/api';
import { useGameState } from '@/state';
import { useOffer } from '@/state/OfferProvider';
import { formatLimo } from '@/services/limo';
import { starsToString, positionLabel } from '@/services/formatters';
import { PlayerName, ClubName } from '@/components/Common/Names';
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
  TransferStatus
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
 * The same colour rules the squad table uses, for the same reason: a number a manager reads in
 * two places is one number, and the two places must say the same thing about it.
 */
const attrClass = (value: number): string => {
  if (value < 8) return 'attr-red';
  if (value < 14) return 'attr-yellow';
  return 'attr-green';
};

const energyTextClass = (energy: number): string => {
  if (energy < 35) return 'energy-red-text';
  if (energy < 70) return 'energy-yellow-text';
  return 'energy-green-text';
};

/** One box of the filter row, read as a number the manager typed. */
const numberFilter = (value: string): number | undefined =>
  value === '' ? undefined : Number(value);

const PlayerRow: React.FC<{
  player: TransferListingDto;
  onSelect: (player: TransferListingDto) => void;
}> = ({ player, onSelect }) => {
  const isKeeper = player.position === 'GK';

  return (
    <tr
      className={['history-row', player.injury !== 'None' ? 'injured' : ''].join(' ')}
      onClick={() => onSelect(player)}
    >
      <td>{positionLabel(player.position)}</td>
      <td className="squad-name">
        <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
        {player.retiring && (
          <span className="retiring-mark" title="Aposentadoria declarada">🏁</span>
        )}
        {player.injury !== 'None' && (
          <span className="injury-mark" title={`Lesionado: ${player.injury}`}>🩹</span>
        )}
        {player.hasActiveProposal && (
          <span className="proposal-mark" title="Já há uma proposta na mesa por este jogador">📝</span>
        )}
      </td>
      <td className="num">{player.age}</td>
      <td className={`num ${energyTextClass(player.energy)}`}>{player.energy}</td>
      <td className="num stars-col">{starsToString(player.stars)}</td>
      <td className={`num ${attrClass(player.speed)}`}>{player.speed}</td>
      <td className={`num ${attrClass(player.accuracy)}`}>{player.accuracy}</td>
      <td className={`num ${attrClass(player.dribbling)}`}>{player.dribbling}</td>
      <td className={`num ${attrClass(player.heading)}`}>{player.heading}</td>
      <td className={`num ${attrClass(player.strength)}`}>{player.strength}</td>
      <td className={`num ${isKeeper ? attrClass(player.goalkeeperPower) : ''}`}>
        {isKeeper ? player.goalkeeperPower : '—'}
      </td>
      <td className={`num ${isKeeper ? attrClass(player.reflexes) : ''}`}>
        {isKeeper ? player.reflexes : '—'}
      </td>
      <td className="num accent">{player.season?.goals ?? 0}</td>
      <td className="num">{player.season?.saves ?? 0}</td>
      <td className="num">{player.season?.yellowCards ?? 0}</td>
      <td className="num">{player.season?.redCards ?? 0}</td>
      <td>
        {player.teamId ? (
          <ClubName teamId={player.teamId}>{player.teamName ?? '—'}</ClubName>
        ) : (
          <span className="free-agent">Livre</span>
        )}
      </td>
      <td className="num money">{player.marketValue ? formatLimo(player.marketValue) : '—'}</td>
      <td className="num money">
        {player.askingPrice ? formatLimo(player.askingPrice) : '—'}
      </td>
      <td className="num money">{player.salary ? formatLimo(player.salary) : '—'}</td>
    </tr>
  );
};

const ProposalRow: React.FC<{ proposal: TransferProposalDto }> = ({ proposal }) => (
  <tr className={`proposal-row status-${STATUS_COLOR[proposal.status]}`}>
    <td className="squad-name">
      <PlayerName playerId={proposal.playerId}>{proposal.playerName}</PlayerName>
    </td>
    <td className="num">{positionLabel(proposal.playerPosition)}</td>
    <td className="num">{proposal.playerAge}</td>
    <td>
      {proposal.sellingClubId ? (
        <ClubName teamId={proposal.sellingClubId}>{proposal.sellingClubName}</ClubName>
      ) : (
        <span className="free-agent">{proposal.sellingClubName}</span>
      )}
    </td>
    <td>
      <ClubName teamId={proposal.buyingClubId}>{proposal.buyingClubName}</ClubName>
    </td>
    <td className="num money">{proposal.fee ? formatLimo(proposal.fee) : '—'}</td>
    {/* The arrival is the whole point of a proposal: when the man walks through the door. */}
    <td className="num contract-cell">
      {proposal.arrivalSeasonNumber}/{proposal.arrivalRoundNumber ?? '—'}
    </td>
    <td className="num">
      <span className={`status-badge status-${STATUS_COLOR[proposal.status]}`}>
        {STATUS_LABELS[proposal.status]}
      </span>
    </td>
    <td className="num">{new Date(proposal.proposedAt).toLocaleDateString('pt-BR')}</td>
  </tr>
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
}> = ({ player, history, onClose, onOffer }) => {
  const isKeeper = player.position === 'GK';

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
            <span className={`transfer-detail__v ${attrClass(player.speed)}`}>{player.speed}</span>
            <span className="transfer-detail__k">Finalização</span>
            <span className={`transfer-detail__v ${attrClass(player.accuracy)}`}>{player.accuracy}</span>
            <span className="transfer-detail__k">Drible</span>
            <span className={`transfer-detail__v ${attrClass(player.dribbling)}`}>{player.dribbling}</span>
            <span className="transfer-detail__k">Cabeceio</span>
            <span className={`transfer-detail__v ${attrClass(player.heading)}`}>{player.heading}</span>
            <span className="transfer-detail__k">Força</span>
            <span className={`transfer-detail__v ${attrClass(player.strength)}`}>{player.strength}</span>
            {isKeeper && (
              <>
                <span className="transfer-detail__k">Defesa de gol</span>
                <span className={`transfer-detail__v ${attrClass(player.goalkeeperPower)}`}>
                  {player.goalkeeperPower}
                </span>
                <span className="transfer-detail__k">Reflexos</span>
                <span className={`transfer-detail__v ${attrClass(player.reflexes)}`}>
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
            <span className="transfer-detail__v">{formatLimo(player.marketValue ?? 0)}</span>
            <span className="transfer-detail__k">Preço de saída</span>
            <span className="transfer-detail__v">
              {player.isFreeAgent
                ? `Contratação — ${formatLimo(player.askingPrice ?? 0)}`
                : formatLimo(player.askingPrice ?? 0)}
            </span>
            <span className="transfer-detail__k">Salário</span>
            <span className="transfer-detail__v">{formatLimo(player.salary ?? 0)}</span>
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
            <span className="transfer-detail__v">{player.total?.appearances ?? 0}</span>
            <span className="transfer-detail__k">Titulares</span>
            <span className="transfer-detail__v">{player.total?.started ?? 0}</span>
            <span className="transfer-detail__k">Reservas</span>
            <span className="transfer-detail__v">{player.total?.cameOn ?? 0}</span>
            <span className="transfer-detail__k">Gols</span>
            <span className="transfer-detail__v accent">{player.total?.goals ?? 0}</span>
            <span className="transfer-detail__k">Amarelos</span>
            <span className="transfer-detail__v">{player.total?.yellowCards ?? 0}</span>
            <span className="transfer-detail__k">Vermelhos</span>
            <span className="transfer-detail__v">{player.total?.redCards ?? 0}</span>
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

  if (!selectedTeam) {
    return (
      <div className="transfer-screen">
        <p className="transfer-empty">Selecione um clube para acessar o mercado de transferências.</p>
      </div>
    );
  }

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
      {/*
        The offers addressed to this club are the first thing on the screen, above the search
        that finds new men. A manager opening "Mercado" is asking one of two questions — what
        can I buy, and what do I have to answer for — and the second is the one with a clock on
        it: an offer nobody answers dies at the season's end. Burying it under a twenty-column
        table is how twenty offers pile up unread.
      */}
      {inbox && (inbox.incoming.length > 0 || inbox.outgoing.length > 0) && (
        <section className="transfer-inbox-section">
          <h3>
            Propostas na mesa
            {incoming.length > 0 && (
              <span className="transfer-inbox-count">{incoming.length} a responder</span>
            )}
          </h3>

          {inbox.outgoing.length > 0 && (
            <>
              <h4>Enviadas</h4>
              {/*
                Every column a row prints is a column the head names, in the same order. A row
                that prints one number the head does not name shifts every value after it: the
                seller ends up under "Comprador" and the status under "Data", and a table read
                that way is not a table that is nearly right, it is a table saying the opposite
                of what it means.
              */}
              <table className="transfer-table club-squad-table">
                <thead>
                  <tr>
                    <th className="squad-name">Jogador</th>
                    <th>Posição</th>
                    <th className="num">Idade</th>
                    <th>Vendedor</th>
                    <th>Comprador</th>
                    <th className="num money">Oferta</th>
                    <th className="num">Chega</th>
                    <th>Status</th>
                    <th className="num">Data</th>
                  </tr>
                </thead>
                <tbody>
                  {inbox.outgoing.map(p => (
                    <ProposalRow key={p.transferId} proposal={p} />
                  ))}
                </tbody>
              </table>
            </>
          )}

          {inbox.incoming.length > 0 && (
            <>
              <h4>Recebidas</h4>
              <table className="transfer-table club-squad-table">
                <thead>
                  <tr>
                    <th className="squad-name">Jogador</th>
                    <th>Posição</th>
                    <th className="num">Idade</th>
                    <th>Vendedor</th>
                    <th>Comprador</th>
                    <th className="num money">Oferta</th>
                    <th className="num">Chega</th>
                    <th>Status</th>
                    <th>Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {inbox.incoming.map(p => (
                    <tr key={p.transferId} className={`proposal-row status-${STATUS_COLOR[p.status]}`}>
                      <td className="squad-name">
                        <PlayerName playerId={p.playerId}>{p.playerName}</PlayerName>
                      </td>
                      <td>{positionLabel(p.playerPosition)}</td>
                      <td className="num">{p.playerAge}</td>
                      <td>
                        {p.sellingClubId ? (
                          <ClubName teamId={p.sellingClubId}>{p.sellingClubName}</ClubName>
                        ) : (
                          <span className="free-agent">{p.sellingClubName}</span>
                        )}
                      </td>
                      <td>
                        <ClubName teamId={p.buyingClubId}>{p.buyingClubName}</ClubName>
                      </td>
                      <td className="num money">{p.fee ? formatLimo(p.fee) : '—'}</td>
                      <td className="num">
                        {p.arrivalSeasonNumber}/{p.arrivalRoundNumber ?? '—'}
                      </td>
                      <td>
                        <span className={`status-badge status-${STATUS_COLOR[p.status]}`}>
                          {STATUS_LABELS[p.status]}
                        </span>
                      </td>
                      <td>
                        {p.status === 'Pending' && (
                          <>
                            <button
                              className="ctrl btn-sm accept"
                              disabled={answering === p.transferId}
                              onClick={() => handleAnswer(p, true)}
                            >
                              Aceitar
                            </button>
                            <button
                              className="ctrl btn-sm reject"
                              disabled={answering === p.transferId}
                              onClick={() => handleAnswer(p, false)}
                            >
                              Recusar
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </section>
      )}

      <section className="transfer-search-section">
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
            <table className="transfer-table club-squad-table">
              <thead>
                <tr>
                  <th>Pos</th>
                  <th className="squad-name">Jogador</th>
                  <th className="num">Idade</th>
                  <th className="num">Energia</th>
                  <th className="num">★</th>
                  <th className="num">Vel</th>
                  <th className="num">Fin</th>
                  <th className="num">Dri</th>
                  <th className="num">Cab</th>
                  <th className="num">For</th>
                  <th className="num">Gol</th>
                  <th className="num">Ref</th>
                  <th className="num accent">Gols</th>
                  <th className="num">Defs</th>
                  <th className="num">Ama</th>
                  <th className="num">Verm</th>
                  <th>Clube</th>
                  <th className="num">Valor</th>
                  <th className="num">Preço</th>
                  <th className="num">Salário</th>
                </tr>
              </thead>
              <tbody>
                {searchResult.players.map(player => (
                  <PlayerRow
                    key={player.playerId}
                    player={player}
                    onSelect={setSelectedPlayer}
                  />
                ))}
              </tbody>
            </table>

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
        />
      )}
    </div>
  );
};

export default TransferScreen;
