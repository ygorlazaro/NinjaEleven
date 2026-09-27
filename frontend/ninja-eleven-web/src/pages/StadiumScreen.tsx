import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import type { StadiumProfileDto } from '@/types';
import { formatLimo } from '@/services/limo';
import { useClubWindow } from '@/services/clubColors';
import { mockStadium } from '@/mock/clubBusiness';
import { useGameState } from '@/state';

/**
 * What a price the manager is choosing looks like, and why it cannot be saved yet.
 *
 * The control is live and the save is refused, and both of those are the honest answer: a
 * ground has **one** price in the world today (`stadiums.ticket_price`), and the engine's
 * attendance model reads that one number for every match. Moving a control that writes nowhere
 * would be a screen that takes a manager's decision and forgets it, which is worse than a
 * screen that says the decision cannot be taken yet.
 */
const TicketPriceControl: React.FC<{
  label: string;
  hint: string;
  value: number;
  onChange: (value: number) => void;
}> = ({ label, hint, value, onChange }) => (
  <div className="price-control">
    <div className="price-control__head">
      <span className="price-control__label">{label}</span>
      <span className="price-control__value">{formatLimo(value)}</span>
    </div>
    <input
      type="range"
      min={0}
      max={500}
      step={1}
      value={value}
      onChange={event => onChange(Number(event.target.value))}
      aria-label={label}
    />
    <p className="price-control__hint">{hint}</p>
  </div>
);

/**
 * The ground: its name, where it is, how many it holds, and what a seat costs.
 *
 * A manager prices his own stadium twice, because a league match and a cup tie are not the
 * same evening: a season ticket is worth nothing to a club whose opponent is a second
 * division side, and a cup night is worth a deal. One price for both prices the league and the
 * cup as the same thing, and it is the club's own ground that has to carry the difference.
 *
 * **The control is a stand-in.** The world keeps one ticket price per ground, and splitting
 * it means changing `AttendanceCalculator` and the `stadiums` table — so the two prices on
 * this screen are a design to be agreed and not a setting. The city is invented too: a ground
 * has an address and nothing in the schema has ever had to keep it.
 */
const StadiumScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);

  // A ground that already exists in the world is not re-invented: its name, its capacity and
  // its league price are the club's own, and only what the game does not keep is filled in.
  // It is read on every render rather than held in state, so a manager who changes clubs
  // arrives at the new club's ground instead of the one he was looking at.
  const stadium: StadiumProfileDto | null = selectedTeam
    ? mockStadium(selectedTeam, selectedTeam.stadium ?? undefined)
    : null;
  // Only the two prices the manager is moving are state, and they are reset with the club:
  // a price chosen for one ground is not a price for another one.
  const [prices, setPrices] = useState<{ league: number; cup: number } | null>(null);

  // A price chosen for one ground is not a price for another one, so the controls go back to
  // the club's own numbers when the club changes under them.
  useEffect(() => {
    setPrices(null);
  }, [selectedTeam?.id]);

  if (!selectedTeam || !stadium) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Estádio</h2>
          <p className="competition">Escolha um clube para ver o estádio dele.</p>
        </div>
      </div>
    );
  }

  const league = prices?.league ?? stadium.leagueTicketPrice;
  const cup = prices?.cup ?? stadium.cupTicketPrice;

  return (
    <div className="app">
      <div className="card team-view-card club-modal" style={clubWindow}>
        <header className="stadium-head">
          <div>
            <h2 className="profile-name">{stadium.name}</h2>
            <p className="club-page__tag">
              {stadium.city} • {stadium.capacity.toLocaleString('pt-BR')} lugares
            </p>
          </div>
          <Link className="ctrl" to="/club">
            Voltar ao clube
          </Link>
        </header>

        {/* The ground in the club's colours: a stand, a pitch and the two colours. A ground is
            a shape before it is a number, and a manager reads a stadium as a bowl long before
            he reads a capacity. */}
        <div
          className="stadium-drawing"
          style={{ '--pitch-a': stadium.primaryColor, '--pitch-b': stadium.secondaryColor } as React.CSSProperties}
          role="img"
          aria-label={`${stadium.name}, ${stadium.capacity} lugares`}
        >
          <span className="stadium-drawing__stand stadium-drawing__stand--top" />
          <span className="stadium-drawing__pitch" />
          <span className="stadium-drawing__stand stadium-drawing__stand--bottom" />
        </div>

        <section className="stadium-figures">
          <div className="club-figure">
            <span className="club-figure__icon">🏟</span>
            <span className="club-figure__value">{stadium.capacity.toLocaleString('pt-BR')}</span>
            <span className="club-figure__label">Lotação</span>
          </div>
          <div className="club-figure">
            <span className="club-figure__icon">🎫</span>
            <span className="club-figure__value">{formatLimo(league)}</span>
            <span className="club-figure__label">Ingresso de campeonato</span>
          </div>
          <div className="club-figure club-figure--accent">
            <span className="club-figure__icon">🏆</span>
            <span className="club-figure__value">{formatLimo(cup)}</span>
            <span className="club-figure__label">Ingresso de copa</span>
          </div>
        </section>

        <section className="stadium-prices">
          <h3 className="club-section-title">Preço dos ingressos</h3>
          <TicketPriceControl
            label="Campeonato"
            hint="O que a arquibancada paga numa rodada do championship."
            value={league}
            onChange={value => setPrices({ league: value, cup })}
          />
          <TicketPriceControl
            label="Copa"
            hint="Uma noite de copa vale mais que uma rodada: é jogo de ida e volta, com torcida de fora."
            value={cup}
            onChange={value => setPrices({ league, cup: value })}
          />
          <p className="stadium-prices__note">
            <strong>Ainda não é uma configuração.</strong> O mundo guarda um único preço por
            estádio e o modelo de público lê esse número em toda partida; separar campeonato de
            copa é mudar o domínio e a tabela, e não um campo que a tela preenche. A copa sai
            mais cara porque é o desenho que se recomenda, não porque o motor já faça isso.
          </p>
        </section>
      </div>
    </div>
  );
};

export default StadiumScreen;
