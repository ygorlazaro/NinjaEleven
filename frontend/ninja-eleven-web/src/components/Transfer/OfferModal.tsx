import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SeasonApi, TransferApi } from '@/api';
import { useGameState } from '@/state';
import { formatLimo } from '@/services/limo';
import { positionLabel, starsToString } from '@/services/formatters';
import { PlayerName, ClubName } from '@/components/Common/Names';
import type { TransferListingDto, TransferWindowStateDto } from '@/types';

/** What the caller already has, so the modal opens on it instead of asking again. */
export interface OfferRequest {
  playerId: string;
  /** The row the manager clicked, already read by the market. */
  listing?: TransferListingDto | null;
  /** The calendar the row belongs to, which is the only thing the offer needs about the world. */
  window?: TransferWindowStateDto | null;
  /** The season the row was read against, when the caller has one and no window. */
  seasonId?: string | null;
}

interface OfferModalProps {
  request: OfferRequest | null;
  onClose: () => void;
  /** Called after a proposal the backend accepted, so a list behind the modal can ask again. */
  onProposed: () => void;
}

/**
 * The offer, on its own, as a window over whatever the manager is reading.
 *
 * A manager makes an offer in two places — the market, and the profile of a striker he has just
 * read — and the offer is the same offer in both. So it is one screen mounted once and opened by
 * player id, rather than a block of controls copied into two pages that would then answer the
 * same question two different ways. The modal is given the row when there is one, so a manager who
 * clicks a row sees the same numbers the row said, and is asked for the season when there is
 * not, so a manager who arrives from a profile is not a manager without an offer.
 */
const OfferModal: React.FC<OfferModalProps> = ({ request, onClose, onProposed }) => {
  const navigate = useNavigate();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [listing, setListing] = useState<TransferListingDto | null>(request?.listing ?? null);
  const [window, setWindow] = useState<TransferWindowStateDto | null>(request?.window ?? null);
  const [feeInput, setFeeInput] = useState<string>(
    request?.listing?.askingPrice ? String(request.listing.askingPrice) : ''
  );

  const [loading, setLoading] = useState(false);
  const [proposing, setProposing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (!request) return;

    // Everything the caller already had is used as it is: a manager who clicked a row is not
    // made to wait for the row to come back from the backend to be able to offer for it.
    if (request.listing && request.window) {
      setListing(request.listing);
      setWindow(request.window);
      setFeeInput(request.listing.askingPrice ? String(request.listing.askingPrice) : '');
      return;
    }

    let alive = true;

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const seasonId = request.seasonId ?? (await SeasonApi.current()).id;
        if (!alive) return;

        const [found, market] = await Promise.all([
          request.listing ?? TransferApi.getListing(request.playerId, seasonId),
          request.window ?? TransferApi.search(seasonId, {}, 1, 1).then(result => result.window)
        ]);

        if (!alive) return;
        setListing(found);
        setWindow(market);
        setFeeInput(found.askingPrice ? String(found.askingPrice) : '');
      } catch (err) {
        if (alive) {
          setError(err instanceof Error ? err.message : 'Não foi possível abrir a proposta.');
        }
      } finally {
        if (alive) setLoading(false);
      }
    };

    load();
    return () => { alive = false; };
  }, [request?.playerId, request?.listing, request?.window, request?.seasonId]);

  if (!request) return null;

  const player = listing;
  const spokenFor = Boolean(player?.hasActiveProposal);

  // A free agent is signed, not bought: there is nobody to charge, so the offer is a formality
  // and the button is not gated on a price that does not exist.
  const canOffer = player ? (player.isFreeAgent || player.askingPrice !== null) : false;

  const handlePropose = async () => {
    if (!player || !selectedTeam?.id) return;

    setProposing(true);
    setError(null);
    setSuccess(null);

    try {
      const fee = feeInput ? Number(feeInput) : 0;
      const proposal = await TransferApi.propose(
        player.playerId,
        selectedTeam.id,
        player.isFreeAgent ? undefined : fee || undefined
      );

      setSuccess(
        proposal.arrivalSeasonNumber > (window?.seasonNumber ?? 0)
          ? 'Proposta enviada. Ele chega na próxima temporada, após a Supercopa.'
          : `Proposta enviada. Ele chega após a ${proposal.arrivalRoundNumber}ª rodada.`
      );

      // The row the manager read is no longer true — he is spoken for from this moment, and a
      // market that still shows him as available invites the same offer a second time.
      setListing(current => (current ? { ...current, hasActiveProposal: true } : current));
      onProposed();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível enviar a proposta.');
    } finally {
      setProposing(false);
    }
  };

  const goToTheMarket = () => {
    onClose();
    navigate(`/transfer?player=${request.playerId}`);
  };

  return (
    <div className="modal">
      <div className="modal-card offer-modal">
        <header className="transfer-detail__head">
          <h3>
            {player ? (
              <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
            ) : (
              'Proposta'
            )}
          </h3>
          <button className="ctrl transfer-detail__close" onClick={onClose} title="Fechar">✕</button>
        </header>

        {loading && <p className="transfer-empty">Carregando...</p>}

        {error && <p className="transfer-error">{error}</p>}
        {success && <p className="transfer-success">{success}</p>}

        {player && (
          <>
            <p className="offer-modal__who">
              {positionLabel(player.position)} · {player.age} anos ·{' '}
              <span className="stars-col">{starsToString(player.stars)}</span>
            </p>
            <p className="offer-modal__club">
              {player.teamId ? (
                <ClubName teamId={player.teamId}>{player.teamName ?? '—'}</ClubName>
              ) : (
                <span className="free-agent">Sem clube</span>
              )}
              {player.askingPrice != null && (
                <span className="offer-modal__asking">
                  {player.isFreeAgent ? 'Contratação' : 'Pedido do clube'}:{' '}
                  {formatLimo(player.askingPrice)}
                </span>
              )}
            </p>

            <div className="transfer-offer-bar">
              {window && (
                <p className="transfer-window-note">
                  {player.isFreeAgent ? 'Contratação' : 'Proposta'} com chegada {window.arrivalLabel}.
                </p>
              )}
              {spokenFor && (
                <p className="transfer-offer__hint">
                  Já há uma proposta na mesa por este jogador. Ela espera resposta em Propostas na
                  mesa, no mercado.
                </p>
              )}
              {player.isFreeAgent ? (
                <button
                  className="ctrl transfer-offer__button"
                  disabled={proposing || spokenFor}
                  onClick={handlePropose}
                >
                  {proposing ? 'Enviando...' : `Contratar ${player.name}`}
                </button>
              ) : (
                <div className="transfer-offer">
                  <label className="transfer-offer__label">Oferta:</label>
                  <input
                    type="number"
                    className="ctrl transfer-offer__input"
                    min={0}
                    step={1000}
                    value={feeInput}
                    onChange={e => setFeeInput(e.target.value)}
                    disabled={proposing || spokenFor}
                  />
                  <span className="transfer-offer__currency">L$</span>
                  <button
                    className="ctrl transfer-offer__button"
                    disabled={proposing || !canOffer || spokenFor}
                    onClick={handlePropose}
                  >
                    {proposing ? 'Enviando...' : `Propor por ${player.name}`}
                  </button>
                </div>
              )}
            </div>
          </>
        )}

        {!selectedTeam && (
          <p className="transfer-offer__hint">Selecione um clube para fazer uma proposta.</p>
        )}

        <div className="modal-actions">
          <button className="ctrl" onClick={goToTheMarket}>Ver no mercado</button>
          <button className="ctrl" onClick={onClose}>Fechar</button>
        </div>
      </div>
    </div>
  );
};

export default OfferModal;
