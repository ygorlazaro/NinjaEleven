import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { formatLimo } from '@/services/limo';
import { useClubWindow } from '@/services/clubColors';
import { useGameState } from '@/state';
import { CrowdApi } from '@/api';
import { ClubName } from '@/components/Common/Names';
import type {
  CrowdModuleDto,
  RivalDto,
  StadiumProjectDto,
  StadiumWorkStartedDto,
} from '@/types';

/**
 * The ground, the crowd behind it, the works on it and the four clubs it is a rival of.
 *
 * **Every number here is the world's.** One call, because the screen is one page: the crowd, the
 * ground, the building site and the rivals are four reads in the backend and one thing to a
 * manager, and a page that draws each as it lands is a page showing a crowd from one year next
 * to a ground from another.
 *
 * The two figures the expansion decision rests on are both on this page and they are not
 * interchangeable. `averageAttendance` is what came through the turnstiles and is capped by the
 * capacity, so a sold-out ground of five thousand reads exactly like a comfortably large one.
 * `averageDemand` is what wanted to come, is the number the ceiling was applied to, and is the
 * only one of the two that can say a club is being turned away. A screen showing just the first
 * would tell every manager with a full ground that his ground is the right size.
 *
 * So a ground that is full and a ground that is turning people away are drawn differently, and
 * a manager whose pressure has never been measured is told so with a dash rather than with a
 * zero — a zero here would be an answer to a question nobody took.
 */
const StadiumScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);

  const [module, setModule] = useState<CrowdModuleDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [starting, setStarting] = useState(false);
  const [started, setStarted] = useState<StadiumWorkStartedDto | null>(null);
  const [startError, setStartError] = useState<string | null>(null);

  const teamId = selectedTeam?.id;

  const load = useCallback(() => {
    if (!teamId) return;

    let alive = true;

    CrowdApi.module(teamId)
      .then(data => {
        if (!alive) return;
        setModule(data);
        setFailed(false);
      })
      .catch(() => {
        // No stand-in. A crowd page that failed to load says it failed, because the alternative
        // is a page that looks like a memory and is not one.
        if (!alive) return;
        setModule(null);
        setFailed(true);
      });

    return () => {
      alive = false;
    };
  }, [teamId]);

  useEffect(load, [load]);

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

  if (failed) {
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
          <p className="league-empty">Não foi possível carregar a torcida e o estádio.</p>
        </div>
      </div>
    );
  }

  if (!module) {
    // The request is in flight. Not a zero and not an invented ground: there is a difference
    // between a club whose ground holds nobody and a club whose ground has not been asked for.
    return (
      <div className="app">
        <div className="card team-view-card club-modal" style={clubWindow}>
          <p className="league-empty">Carregando estádio e torcida…</p>
        </div>
      </div>
    );
  }

  const { crowd, stadium, rivals, catalogue } = module;
  const pressure = crowd.pressure ?? null;
  const work = stadium.work ?? null;

  // A ground nobody has opened has no average crowd, and a ground whose pressure has never
  // been measured has no share turned away. Both are dashes, and the difference between the
  // two is on the wire as `demandIsMeasured`.
  const hasGate = crowd.averageAttendance != null;
  const demandKnown = pressure?.demandIsMeasured === true;

  const asPercent = (value: number | null | undefined) =>
    value == null ? '—' : `${Math.round(value * 100)}%`;

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
            <span className="club-figure__icon">👥</span>
            <span className="club-figure__value">{crowd.supporters.toLocaleString('pt-BR')}</span>
            <span className="club-figure__label">Torcida</span>
          </div>
          <div className="club-figure">
            <span className="club-figure__icon">🎫</span>
            <span className="club-figure__value">{formatLimo(stadium.ticketPrice)}</span>
            <span className="club-figure__label">Ingresso</span>
          </div>
        </section>

        {/* What came through the turnstiles, and what wanted to. The bar's width is the
            occupancy the backend worked out — a bar only has a width, so reading one off the
            number it was sent is not a second opinion about the number. */}
        <section className="crowd-pressure">
          <h3 className="club-section-title">Pressão da torcida</h3>

          {!hasGate ? (
            <p className="stadium-prices__note">
              Este clube ainda não abriu o estádio nesta temporada, então não há média de
              público para falar. Um clube sem jogo em casa não é um clube com estádio vazio.
            </p>
          ) : (
            <>
              <div className="crowd-pressure__bar" aria-hidden="true">
                <div style={{ width: `${Math.min(100, (pressure?.occupancy ?? 0) * 100)}%` }} />
              </div>

              <div className="crowd-pressure__figures">
                <div className="crowd-pressure__figure">
                  <span className="crowd-pressure__value">
                    {crowd.averageAttendance!.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}
                  </span>
                  <span className="crowd-pressure__label">Público médio</span>
                </div>
                <div className="crowd-pressure__figure">
                  <span className="crowd-pressure__value">
                    {pressure?.averageDemand != null
                      ? pressure.averageDemand.toLocaleString('pt-BR', { maximumFractionDigits: 0 })
                      : '—'}
                  </span>
                  <span className="crowd-pressure__label">Torcida que queria ir</span>
                </div>
                <div className="crowd-pressure__figure">
                  <span className="crowd-pressure__value">{asPercent(pressure?.occupancy)}</span>
                  <span className="crowd-pressure__label">Ocupação</span>
                </div>
                <div
                  className={`crowd-pressure__figure ${
                    pressure?.oversubscribed ? 'crowd-pressure__figure--hot' : ''
                  }`}
                >
                  <span className="crowd-pressure__value">
                    {asPercent(pressure?.turnedAwayShare)}
                  </span>
                  <span className="crowd-pressure__label">Deixados de fora</span>
                </div>
              </div>

              <p className="stadium-prices__note">
                {demandKnown
                  ? pressure?.oversubscribed
                    ? 'A torcida que quer entrar é maior do que as cadeiras comportam. É esta a diferença que uma obra resolve: quem ficou de fora é o público que a próxima arquibancada comporta.'
                    : 'A torcida cabe. Este estádio é do tamanho certo para ela, e não há pressão para construir.'
                  : 'A pressão da torcida ainda não foi medida neste clube: as partidas desta temporada foram jogadas antes de o mundo guardar o que a torcida queria. Não é zero — é falta de medida.'}
              </p>
            </>
          )}
        </section>

        {/* The works and the catalogue. A manager picks one of three and it is charged to the
            book in the same operation that writes the project, so a club can never be holding a
            building site it did not pay for. */}
        <section className="stadium-works">
          <h3 className="club-section-title">Obras</h3>

          {work ? (
            <div className="stadium-work stadium-work--open">
              <div className="stadium-work__head">
                <strong>{work.seats.toLocaleString('pt-BR')} lugares</strong>
                <span className="stadium-work__cost">{formatLimo(work.cost)}</span>
              </div>
              <p className="club-page__tag">
                Começou depois da rodada {work.startedAfterRound} e leva {work.rounds}{' '}
                {work.rounds === 1 ? 'rodada' : 'rodadas'}. Enquanto a obra corre o estádio
                recebe 75% da torcida que um estádio pronto receberia — uma arquibancada em
                obra é menor, e não uma obra esquecida.
              </p>
            </div>
          ) : catalogue.length === 0 ? (
            <p className="league-empty">Não há projeto de expansão além do tamanho atual.</p>
          ) : (
            <>
              <div className="stadium-catalogue">
                {catalogue.map(project => (
                  <ProjectCard
                    key={project.seats}
                    project={project}
                    busy={starting}
                    onStart={() => {
                      setStarting(true);
                      setStartError(null);

                      CrowdApi.startExpansion(selectedTeam.id, project.seats, 0)
                        .then(result => {
                          setStarted(result);
                          // The screen reloads rather than patching itself: a project changes the
                          // ground, the works panel and what the next season starts from, and a
                          // screen that updated two of the three by hand is a screen that will
                          // eventually be showing a building site nobody paid for.
                          load();
                        })
                        .catch(() => setStartError('Não foi possível iniciar a obra.'))
                        .finally(() => setStarting(false));
                    }}
                  />
                ))}
              </div>

              {started && started.kind === 'Started' && (
                <p className="club-page__tag">
                  Obra iniciada: {started.seats.toLocaleString('pt-BR')} lugares por{' '}
                  {formatLimo(started.cost)}, entregues em {started.rounds}{' '}
                  {started.rounds === 1 ? 'rodada' : 'rodadas'}.
                </p>
              )}

              {startError && <p className="league-empty">{startError}</p>}
            </>
          )}
        </section>

        {/* The four clubs this one is a rival of. The five bands are on the wire because the
            sum of five numbers is not something a manager can act on and "you have met them six
            times and split them" is. */}
        <section className="club-rivals">
          <h3 className="club-section-title">Rivais</h3>

          {rivals.length === 0 ? (
            <p className="league-empty">Este clube ainda não tem história contra ninguém.</p>
          ) : (
            rivals.map(rival => <RivalLine key={rival.opponentTeamId} rival={rival} />)
          )}
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

/**
 * One project on offer.
 *
 * The button sends the number of seats and nothing else. The price on the card came from the
 * catalogue, so a client that could name a price would be naming a number the book had never
 * agreed to — and a manager budgeting his season against a price the screen made up would find
 * out at the moment he pressed the button.
 */
const ProjectCard: React.FC<{
  project: StadiumProjectDto;
  busy: boolean;
  onStart: () => void;
}> = ({ project, busy, onStart }) => (
  <div className="stadium-project">
    <span className="stadium-project__seats">
      {project.seats.toLocaleString('pt-BR')} lugares
    </span>
    <span className="stadium-project__cost">{formatLimo(project.cost)}</span>
    <span className="stadium-project__rounds">
      {project.rounds} {project.rounds === 1 ? 'rodada' : 'rodadas'}
    </span>
    <button type="button" className="ctrl" disabled={busy} onClick={onStart}>
      {busy ? 'Assinando…' : 'Construir'}
    </button>
  </div>
);

/**
 * One rival, and the games that made it one.
 *
 * A name here is a door, like every other club name in the game, so clicking it opens the
 * opponent's profile rather than doing nothing.
 */
const RivalLine: React.FC<{ rival: RivalDto }> = ({ rival }) => {
  const bands: Array<[string, number]> = [
    ['Recorrência', rival.why.recurrence],
    ['Decisivos', rival.why.decisiveness],
    ['Equilíbrio', rival.why.results],
    ['Sequências', rival.why.streaks],
    ['Recente', rival.why.recentForm],
  ];

  return (
    <div className="rival">
      <div className="rival__head">
        <ClubName teamId={rival.opponentTeamId} className="rival__name">
          {rival.opponentName}
        </ClubName>
        <span className="rival__score" title="Quão grande é esta rivalidade">
          {rival.score.toFixed(2)}
        </span>
      </div>

      <p className="rival__record">
        {rival.meetings} jogos · {rival.wins}V {rival.draws}E {rival.defeats}D · {rival.goalsFor}-
        {rival.goalsAgainst}
      </p>

      <div className="rival__bands">
        {bands.map(([label, value]) => (
          <div key={label} className="rival__band" title={`${label}: ${value.toFixed(2)}`}>
            <span className="rival__band-label">{label}</span>
            <span className="attr-bar">
              <span className="attr-bar-fill" style={{ width: `${value * 100}%` }} />
            </span>
          </div>
        ))}
      </div>
    </div>
  );
};

export default StadiumScreen;