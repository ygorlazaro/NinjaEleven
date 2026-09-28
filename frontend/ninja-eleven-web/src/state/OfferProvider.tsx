import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import OfferModal, { type OfferRequest } from '@/components/Transfer/OfferModal';

interface OfferContextValue {
  /**
   * Opens the offer for a player.
   *
   * What the caller already holds is passed along: the market has the row it was clicked from
   * and the calendar that row belongs to, and a player arriving with both is a player whose
   * numbers the manager has already read. A player arriving with neither — a name followed
   * from a profile — is asked of the backend by id, which is why this takes a player id and not
   * a listing.
   */
  openOffer: (request: OfferRequest) => void;
  closeOffer: () => void;
  /**
   * Bumped every time a proposal is actually sent, so a list of players behind the window knows
   * that the man it is showing has just been spoken for and asks the market again.
   */
  proposalNonce: number;
}

const OfferContext = createContext<OfferContextValue | null>(null);

/**
 * The offer window, owned by the game rather than by a screen.
 *
 * A manager offers for a player from the market and from the player's profile, and both offers
 * are the same offer — so there is one window, mounted once, and every screen asks for it by
 * player id instead of carrying its own copy of the form. Two copies of a form are two answers
 * to "is he spoken for", and they would not agree with each other for long.
 */
export const OfferProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [request, setRequest] = useState<OfferRequest | null>(null);
  const [proposalNonce, setProposalNonce] = useState(0);

  const openOffer = useCallback((next: OfferRequest) => setRequest(next), []);
  const closeOffer = useCallback(() => setRequest(null), []);

  const value = useMemo(
    () => ({ openOffer, closeOffer, proposalNonce }),
    [openOffer, closeOffer, proposalNonce]
  );

  return (
    <OfferContext.Provider value={value}>
      {children}
      <OfferModal
        key={request?.playerId ?? 'no-offer'}
        request={request}
        onClose={closeOffer}
        onProposed={() => setProposalNonce(nonce => nonce + 1)}
      />
    </OfferContext.Provider>
  );
};

export const useOffer = (): OfferContextValue => {
  const context = useContext(OfferContext);

  if (!context) {
    throw new Error('useOffer has to be used inside an OfferProvider.');
  }

  return context;
};
