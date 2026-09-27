import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import type { SponsorBookDto, SponsorOfferDto } from '@/types';
import { formatLimo } from '@/services/limo';
import { useClubWindow } from '@/services/clubColors';
import { mockSponsorBook } from '@/mock/clubBusiness';
import { useGameState } from '@/state';

/**
 * One offer, as a card: who they are, what they pay and for how long.
 *
 * The three numbers are kept apart because a manager compares them in different directions:
 * what a sponsor pays per match is what the season is worth, and how many matches the deal
 * runs for is what the club is committing to. A card that showed only the fee would make a
 * long deal at a low fee look like the same offer as a short one at a high fee, and they are
 * not the same money.
 */
const OfferCard: React.FC<{
  offer: SponsorOfferDto;
  /** The sponsor the club is carrying, which is never offered back as a candidate. */
  isCurrent: boolean;
  /** Whether a change is allowed at all, which is a fact about the deal and not about the club. */
  canChange: boolean;
  onSelect: (offer: SponsorOfferDto) => void;
}> = ({ offer, isCurrent, canChange, onSelect }) => {
  const perSeason = offer.perMatchFee * offer.contractMatches;

  return (
    <article className={`sponsor-card${isCurrent ? ' sponsor-card--current' : ''}`}>
      <header className="sponsor-card__head" style={{ borderColor: offer.color }}>
        <span className="sponsor-card__name">{offer.name}</span>
        <span className="sponsor-card__industry">{offer.industry}</span>
      </header>

      <div className="sponsor-card__figures">
        <div className="sponsor-card__figure">
          <span className="sponsor-card__value">{formatLimo(offer.perMatchFee)}</span>
          <span className="sponsor-card__label">por jogo</span>
        </div>
        <div className="sponsor-card__figure">
          <span className="sponsor-card__value">{offer.contractMatches}</span>
          <span className="sponsor-card__label">jogos no contrato</span>
        </div>
        <div className="sponsor-card__figure">
          <span className="sponsor-card__value">{formatLimo(perSeason)}</span>
          <span className="sponsor-card__label">no contrato inteiro</span>
        </div>
      </div>

      {isCurrent ? (
        <p className="sponsor-card__status">No ar agora</p>
      ) : (
        <button
          className="ctrl sponsor-card__button"
          disabled={!canChange}
          title={
            canChange
              ? `Assinar com a ${offer.name}`
              : 'O contrato atual ainda tem jogos a pagar'
          }
          onClick={() => onSelect(offer)}
        >
          {canChange ? 'Selecionar' : 'Contrato em andamento'}
        </button>
      )}
    </article>
  );
};

/**
 * The sponsor book: who is on the shirt, what the deal is worth, and the five waiting for it.
 *
 * **The whole screen turns on one number.** A club sells its shirt for a number of games, and
 * the money for those games has been taken already. So a club with games left on its deal
 * cannot change sponsor: the name it sold is the name on the shirt for the rest of the deal,
 * and a manager who broke it would be keeping money he had already been given for matches he
 * has not yet played. The deal has to run out, and the day it does is the day the club is
 * free — which is why the button is not greyed out for being unimplemented but because the
 * rule says so.
 *
 * **All of it is invented.** There is no sponsor in this game: no entity, no contract, no
 * column, and no money in the club's book that is not a gate or a wage. The screen is a design
 * for a feature that has no domain behind it yet, and the numbers on it are drawn so the shape
 * of the decision can be argued about before anything is built.
 */
const SponsorsScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);

  const book: SponsorBookDto | null = selectedTeam ? mockSponsorBook(selectedTeam) : null;
  const [signedWith, setSignedWith] = useState<string | null>(null);

  if (!selectedTeam || !book) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Patrocinadores</h2>
          <p className="competition">Escolha um clube para ver os patrocinadores dele.</p>
        </div>
      </div>
    );
  }

  // Changing sponsor takes the deal with it, so a manager who signs someone new starts their
  // run with a full contract: the number of matches left is then the new deal's length.
  const current = signedWith
    ? book.candidates.find(offer => offer.id === signedWith) ?? book.current
    : book.current;
  const matchesLeft = signedWith ? current.contractMatches : book.matchesLeft;
  const canChange = matchesLeft === 0;

  return (
    <div className="app">
      <div className="card team-view-card club-modal" style={clubWindow}>
        <header className="stadium-head">
          <div>
            <h2 className="profile-name">Patrocinadores</h2>
            <p className="club-page__tag">
              {selectedTeam.name} • {current.industry}
            </p>
          </div>
          <Link className="ctrl" to="/club">
            Voltar ao clube
          </Link>
        </header>

        {/* The master sponsor, and the deal it was signed on. A sponsorship is paid per match
            and not per season because that is how one is sold: a club takes a sponsor's money
            against the games it plays, and a name that is on a shirt for a season that never
            happened was a promise to a season that does not exist. */}
        <section className="sponsor-master" style={{ '--sponsor': current.color } as React.CSSProperties}>
          <div className="sponsor-master__mark" aria-hidden="true" />
          <div className="sponsor-master__body">
            <span className="sponsor-master__label">Patrocinador master</span>
            <span className="sponsor-master__name">{current.name}</span>
            <span className="sponsor-master__deal">
              {formatLimo(current.perMatchFee)} por jogo • {current.contractMatches} jogos no
              contrato • {formatLimo(current.perMatchFee * current.contractMatches)} no total
            </span>
          </div>
          <div className="sponsor-master__left">
            <span className="sponsor-master__left-value">{matchesLeft}</span>
            <span className="sponsor-master__left-label">
              {matchesLeft === 1 ? 'jogo restante' : 'jogos restantes'}
            </span>
          </div>
        </section>

        {!canChange && (
          <p className="sponsor-lock">
            O contrato com a {current.name} tem {matchesLeft}{' '}
            {matchesLeft === 1 ? 'jogo' : 'jogos'} a pagar. Só é possível trocar de patrocinador
            quando ele acaba — o nome na camisa já foi vendido para essas partidas.
          </p>
        )}

        {canChange && (
          <p className="sponsor-lock sponsor-lock--open">
            O contrato terminou. Qualquer um dos cinco abaixo pode ser assinado, e a escolha
            vale a partir do próximo jogo.
          </p>
        )}

        {/* Five offers, a shortlist and not a market: enough to be a decision and few enough
            that a manager reads all of them. */}
        <section className="sponsor-shortlist">
          <h3 className="club-section-title">Na mesa</h3>
          <div className="sponsor-grid">
            {book.candidates
              .filter(offer => offer.id !== current.id)
              .map(offer => (
                <OfferCard
                  key={offer.id}
                  offer={offer}
                  isCurrent={offer.id === current.id}
                  canChange={canChange}
                  onSelect={chosen => setSignedWith(chosen.id)}
                />
              ))}
          </div>
        </section>
      </div>
    </div>
  );
};

export default SponsorsScreen;
