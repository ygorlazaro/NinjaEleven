import React from 'react';
import { Link } from 'react-router-dom';
import { formatLimo } from '@/services/limo';
import { useClubWindow } from '@/services/clubColors';
import { useGameState } from '@/state';

/**
 * The ground: its name, how many it holds, and what a seat costs.
 *
 * **Every number here is the world's.** The screen used to fill in what the game does not
 * keep — a city out of a list of twelve, a cup price worked out by multiplying the league one
 * by two and a half, and a capacity invented from the club's id when the ground had none — and
 * it drew two sliders over the top that moved numbers nothing read. A ground with a name and a
 * capacity and a league price is a fact; the rest was a design being discussed on a screen
 * that looked like a settings page.
 *
 * So the screen says what it has and names what it does not. There is one price in the world
 * (`stadiums.ticket_price`), the attendance model reads that one number for every match, and a
 * cup night costing more than a league round is a change to `AttendanceCalculator` and to the
 * table — a decision for the domain, not a field a screen may fill in. A control that moved a
 * number nothing read was a control that lied, and there is nothing left to lie with.
 */
const StadiumScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);

  // A ground that does not exist is a club with no stadium, not a club with an invented one:
  // the screen says so and offers nothing to configure, because there is nothing to configure.
  const stadium = selectedTeam?.stadium ?? null;

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Estádio</h2>
          <p className="competition">Escolha um clube para ver o estádio dele.</p>
        </div>
      </div>
    );
  }

  if (!stadium) {
    return (
      <div className="app">
        <div className="card team-view-card club-modal" style={clubWindow}>
          <header className="stadium-head">
            <div>
              <h2 className="profile-name">Estádio</h2>
              <p className="club-page__tag">{selectedTeam.name}</p>
            </div>
            <Link className="ctrl" to="/club">
              Voltar ao clube
            </Link>
          </header>
          <p className="league-empty">Este clube ainda não tem estádio.</p>
        </div>
      </div>
    );
  }

  const price = stadium.ticketPrice;

  return (
    <div className="app">
      <div className="card team-view-card club-modal" style={clubWindow}>
        <header className="stadium-head">
          <div>
            <h2 className="profile-name">{stadium.name}</h2>
            <p className="club-page__tag">{stadium.capacity.toLocaleString('pt-BR')} lugares</p>
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
          style={
            {
              '--pitch-a': selectedTeam.primaryColor,
              '--pitch-b': selectedTeam.secondaryColor,
            } as React.CSSProperties
          }
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
          <div className="club-figure club-figure--accent">
            <span className="club-figure__icon">🎫</span>
            <span className="club-figure__value">
              {price == null ? '—' : formatLimo(price)}
            </span>
            <span className="club-figure__label">Ingresso</span>
          </div>
        </section>

        <section className="stadium-prices">
          <h3 className="club-section-title">Preço dos ingressos</h3>
          <p className="stadium-prices__note">
            O mundo guarda <strong>um</strong> preço por estádio, e é esse número que o modelo
            de público lê em toda partida. Separar campeonato de copa é mudar o domínio e a
            tabela, e não um campo que a tela preenche — por isso não há nada para mover aqui.
            A cidade do estádio também não é uma coisa que o jogo guarda: um estádio tem
            endereço, e nenhum lugar do mundo guarda este.
          </p>
        </section>
      </div>
    </div>
  );
};

export default StadiumScreen;
