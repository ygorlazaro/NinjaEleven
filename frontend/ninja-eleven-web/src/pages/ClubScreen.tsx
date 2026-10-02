import { ManagerApi, SeasonApi, SponsorApi, TeamApi } from '@/api';
import ClubCrest from '@/components/Club/ClubCrest';
import CrestEditor from '@/components/Club/CrestEditor';
import KitEditor from '@/components/Club/KitEditor';
import KitShirt from '@/components/Club/KitShirt';
import ClubOwnershipIcon from '@/components/Common/ClubOwnershipIcon';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import { useClubWindow } from '@/services/clubColors';
import { kitOf } from '@/services/clubKits';
import { formatLimo } from '@/services/limo';
import { useGameState } from '@/state';
import { useAuthStore } from '@/state/auth';
import type { ClubHistoryEventDto, ClubProfileDto, ClubTrophyDto, TeamDto } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

/**
 * What a kind of event is said as, and what it is marked with.
 *
 * The key is the contract with the backend and the words and the mark are the screen's,
 * exactly as the match feed's event types are. A kind this screen has never seen still gets a
 * line in the history and a mark beside it: a manager should be able to read a moment of his
 * club's past that the screen did not have a name for, rather than find it missing.
 */
const EVENTS: Record<string, { label: string; icon: string }> = {
  FirstSeason: { label: 'Fundação', icon: '🏁' },
  NameChange: { label: 'Mudança de nome', icon: '✍️' },
  CrestChange: { label: 'Novo escudo', icon: '🛡' },
  ColorsChange: { label: 'Novas cores', icon: '🎨' },
  TopScorer: { label: 'Artilharia', icon: '🎯' },
  Title: { label: 'Título', icon: '🏆' },
  Promotion: { label: 'Acesso', icon: '⬆️' },
  Relegation: { label: 'Rebaixamento', icon: '⬇️' }
};

const TROPHIES: Record<ClubTrophyDto['kind'], { icon: string; label: string }> = {
  Champion: { icon: '🥇', label: 'Campeão' },
  RunnerUp: { icon: '🥈', label: 'Vice' },
  Third: { icon: '🥉', label: 'Terceiro' }
};

/**
 * One number the page says about the club, as a pair of words and a figure.
 *
 * Money is said in the game's own currency and through the game's own formatter, because a
 * balance in one symbol and a wage in another is a club whose statement does not add up to
 * itself — the same rule the ledger and the player card are held to.
 */
const Figure: React.FC<{ label: string; value: string; icon: string; accent?: boolean }> = ({
  label,
  value,
  icon,
  accent
}) => (
  <div className={`club-figure${accent ? ' club-figure--accent' : ''}`}>
    <span className="club-figure__icon">{icon}</span>
    <span className="club-figure__value">{value}</span>
    <span className="club-figure__label">{label}</span>
  </div>
);

/**
 * The shelf: what the club has won, standing where a club keeps it.
 *
 * Trophies are grouped by competition and drawn as the medal of the place, and the division a
 * shelf belongs to carries that division's own cup in front of its name — because "champion of
 * the 1st division" and "champion of the 3rd" are different claims and a row of identical gold
 * discs throws that away. The shelf draws at most a dozen medals of a kind and counts the rest: a
 * club with thirty titles would otherwise push the page sideways, and a manager wants to know
 * there are thirty, not to count them.
 */
const TrophyShelf: React.FC<{ trophies: ClubTrophyDto[] }> = ({ trophies }) => {
  const shelves = useMemo(() => {
    const byCompetition = new Map<string, ClubTrophyDto[]>();

    trophies.forEach(trophy => {
      const shelf = byCompetition.get(trophy.competition) ?? [];
      shelf.push(trophy);
      byCompetition.set(trophy.competition, shelf);
    });

    return [...byCompetition.entries()].map(([competition, onShelf]) => ({
      competition,
      division: onShelf.find(trophy => trophy.divisionName)?.divisionName ?? null,
      // The tier comes off the first record that has one, because it is a property of the shelf
      // rather than of a season: a club's titles in one division were all won in that division.
      tier: onShelf.find(trophy => trophy.divisionTier != null)?.divisionTier ?? null,
      onShelf
    }));
  }, [trophies]);

  if (trophies.length === 0) {
    return (
      <section className="club-shelf club-shelf--empty">
        <h3 className="club-section-title">Galeria</h3>
        <p className="league-empty">A prateleira está vazia. Nenhum título ainda.</p>
      </section>
    );
  }

  return (
    <section className="club-shelf">
      <h3 className="club-section-title">Galeria</h3>
      {shelves.map(shelf => (
        <div className="club-shelf__row" key={shelf.competition}>
          <div className="club-shelf__name">
            <span className="club-shelf__competition">{shelf.competition}</span>
            {shelf.division && (
              <span className="club-shelf__division">
                {shelf.tier != null && <DivisionTrophy tier={shelf.tier} size={16} />}
                {shelf.division}
              </span>
            )}
          </div>
          <div className="club-shelf__plank">
            {shelf.onShelf.slice(0, 12).map(trophy => (
              <span
                className="club-medal"
                key={trophy.id}
                title={`${TROPHIES[trophy.kind]?.label ?? trophy.kind} — ${trophy.seasonName}`}
              >
                {TROPHIES[trophy.kind]?.icon ?? '🏆'}
              </span>
            ))}
            {shelf.onShelf.length > 12 && (
              <span className="club-medal club-medal--more">+{shelf.onShelf.length - 12}</span>
            )}
            {shelf.onShelf.length === 0 && <span className="club-shelf__none">—</span>}
          </div>
          <span className="club-shelf__count">
            {shelf.onShelf.length} {shelf.onShelf.length === 1 ? 'título' : 'títulos'}
          </span>
        </div>
      ))}
    </section>
  );
};

/**
 * The club's page: who it is, who runs it, what it costs, what it has won and what has
 * happened to it.
 *
 * It is the manager's own club and not a club looked up by id, because the page is where a
 * career starts: the screen the game opens on is a club, and a club that is a row in a
 * dropdown is not a thing anybody manages.
 *
 * **Every number on this page is the backend's.** It used to read a stand-in that drew its own
 * history out of the club's id, which was stable per club and therefore looked right forever
 * while being wrong from the first season — a manager had no way to tell it from a page that
 * worked. It is now `GET /team/{id}/profile`, which derives what the world already knows (the
 * founding, the movements up and down the pyramid, the titles, the artilharias) and reads what
 * was recorded when it was decided (a rename, a crest, a change of colours).
 *
 * The one thing the page asks for separately is the shirt sponsor, because a sponsorship is
 * paid per match and its length in games is a live fact of the current deal rather than part
 * of what the club *is* — and because it is also wanted twice on this page, by the panel and
 * by the shirt it is printed on.
 */
const ClubScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const yourClubId = useAuthStore((s) => s.teamId);
  const returnedAfterDismissal = useAuthStore((s) => s.returnedAfterDismissal);
  const clearReturnedAfterDismissal = useAuthStore((s) => s.clearReturnedAfterDismissal);
  const clubWindow = useClubWindow(selectedTeam);
  const [club, setClub] = useState<ClubProfileDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [sponsorName, setSponsorName] = useState<string | null>(null);
  const [sponsorIndustry, setSponsorIndustry] = useState<string | null>(null);
  const [sponsorMatchesLeft, setSponsorMatchesLeft] = useState<number>(0);
  const [coachName, setCoachName] = useState<string | null>(null);
  const [editingCoach, setEditingCoach] = useState(false);
  const [coachInput, setCoachInput] = useState('');
  const [coachError, setCoachError] = useState<string | null>(null);
  /**
   * Which of the two editors is open, if one is.
   *
   * It is screen state and not a route because neither editor is a place: both are a dialog
   * over the club's own page, opened from it and closed back onto it. A route would make the
   * editor something a manager could be linked to, and a badge that cannot be reached by a
   * link is a badge nobody has drawn.
   */
  const [editor, setEditor] = useState<'crest' | 'kits' | null>(null);

  useEffect(() => {
    if (!selectedTeam) return;

    let alive = true;

    // The club and its sponsor are two questions about two different things, so they are asked
    // together rather than in sequence: the page is not drawn until both have answered, and a
    // page that waited for one to show the other would be a page with a hole in it.
    TeamApi.getProfile(selectedTeam.id)
      .then(profile => {
        if (!alive) return;
        setClub(profile);
        setFailed(false);
        // The manager's own name, which the endpoint carries, and which is empty for a club
        // nobody is running. An empty one is not an error: an NPC club has no manager and the
        // screen says so rather than inviting a manager to rename a man who does not exist.
        setCoachName(profile.coachName || null);
      })
      .catch(() => {
        // No stand-in. A club's page that failed to load says it failed, because the alternative
        // is the page this one used to draw — a history that looks like a memory and is not one,
        // which a manager cannot tell from a real one and would be lied to by.
        if (!alive) return;
        setClub(null);
        setFailed(true);
      });

    SeasonApi.current()
      .then(season => SponsorApi.getBook(selectedTeam.id, season.id))
      .then(book => {
        if (!alive) return;
        setSponsorName(book.current?.name ?? null);
        setSponsorIndustry(book.current?.industry ?? null);
        setSponsorMatchesLeft(book.matchesLeft);
      })
      .catch(() => {
        if (!alive) return;
        setSponsorName(null);
        setSponsorIndustry(null);
      });

    return () => {
      alive = false;
    };
  }, [selectedTeam]);

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Meu clube</h2>
          <p className="competition">Escolha um clube para ver a página dele.</p>
        </div>
      </div>
    );
  }

  const handleRenameCoach = async () => {
    if (!selectedTeam || !coachInput.trim()) return;

    try {
      const manager = await ManagerApi.rename(selectedTeam.id, coachInput.trim());
      setCoachName(manager.name);
      // The rename is also a moment of the club's history now, and the page has to show it
      // without being reloaded: the manager has just watched his club change its name, and a
      // history that only gained the line after a refresh would be a history that arrives late.
      setClub(current =>
        current
          ? {
            ...current,
            history: [
              {
                id: `rename-${manager.name}`,
                kind: 'NameChange',
                seasonId: null,
                seasonNumber: null,
                seasonName: null,
                description: `O clube passou a chamar-se ${manager.name}.`
              },
              ...current.history.filter(entry => entry.kind !== 'NameChange')
            ]
          }
          : current
      );
      setEditingCoach(false);
      setCoachInput('');
      setCoachError(null);
    } catch (err: any) {
      const code = err?.response?.data?.code || err?.response?.data?.message || err?.message;
      setCoachError(code || 'Não foi possível renomear o técnico.');
    }
  };

  const startCoachEdit = (currentName: string) => {
    setCoachInput(currentName || '');
    setEditingCoach(true);
    setCoachError(null);
  };

  const cancelCoachEdit = () => {
    setEditingCoach(false);
    setCoachInput('');
    setCoachError(null);
  };

  if (failed) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">{selectedTeam.name}</h2>
          <p className="competition">Não foi possível carregar a página do clube.</p>
        </div>
      </div>
    );
  }

  if (!club) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">{selectedTeam.name}</h2>
          <p className="competition">Carregando…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="app">
      <div className="card team-view-card club-modal club-page" style={clubWindow}>
        {returnedAfterDismissal && (
          <div className="returned-note" role="status">
            <p>
              Você ficou mais de 30 dias sem entrar e foi demitido. Ao voltar, ganhou um clube
              novo — este aqui é <strong>{club.name}</strong>.
            </p>
            <button
              className="ctrl returned-note__close"
              onClick={clearReturnedAfterDismissal}
              title="Entendi"
            >
              ✕
            </button>
          </div>
        )}

        {/* The shield and the name, side by side: the badge is how a manager recognises a club
            at a glance, so it goes where the eye lands first and the name comes with it. */}
        <header className="club-page__head">
          <ClubCrest
            crest={selectedTeam?.crest}
            primary={club.primaryColor}
            secondary={club.secondaryColor}
            name={club.name}
          />
          <div className="club-page__identity">
            <h2 className="profile-name">{club.name}</h2>
            <ClubOwnershipIcon
              clubId={club.teamId}
              controlledBy={selectedTeam?.controlledBy}
              yourClubId={yourClubId}
            />
            <p className="club-page__tag">
              {club.shortName} •
              <span className="coach-inline">
                {editingCoach ? (
                  <span className="coach-edit">
                    <input
                      className="ctrl coach-edit__input"
                      value={coachInput}
                      onChange={e => setCoachInput(e.target.value)}
                      autoFocus
                    onKeyDown={e => {
                      if (e.key === 'Enter') { e.preventDefault(); handleRenameCoach(); }
                      if (e.key === 'Escape') { e.preventDefault(); cancelCoachEdit(); }
                    }}
                  />
                  {coachError && <div className="error coach-edit__error">{coachError}</div>}
                  <button className="ctrl coach-edit__save" onClick={handleRenameCoach}>Salvar</button>
                    <button className="ctrl coach-edit__cancel" onClick={cancelCoachEdit}>Cancelar</button>
                  </span>
                ) : (
                  <>
                      {/* No manager and no button to rename one. An NPC club has no manager, and
                        offering a rename on a man who does not exist is a control that can only
                        ever fail — the backend has nobody to attach the name to. */}
                      {(coachName ?? club.coachName) || 'Sem técnico'}
                      {yourClubId === club.teamId && (
                        <button
                          className="coach-edit__icon"
                          title="Renomear técnico"
                          onClick={() => startCoachEdit(coachName ?? club.coachName)}
                        >
                          ✏️
                        </button>
                      )}
                  </>
                )}
              </span>
            </p>
          </div>
          <div className="club-page__actions">
            <button className="ctrl" onClick={() => setEditor('crest')}>Escudo</button>
            <button className="ctrl" onClick={() => setEditor('kits')}>Uniformes</button>
            <Link className="ctrl" to={`/team/${club.teamId}`}>
              Elenco
            </Link>
            <Link className="ctrl" to="/financeiro">
              Financeiro
            </Link>
            <Link className="ctrl" to="/estadio">
              Estádio
            </Link>
            <Link className="ctrl" to="/patrocinadores">
              Patrocinadores
            </Link>
          </div>
        </header>

        {/* Four figures: who runs the club, how many men it keeps, what it has in the bank and
            where it has been. The balance is the one that answers a question worth asking —
            a club is a manager's problem the day he cannot pay a wage. */}
        <figure className="club-figures">
          <span className="club-figure">
            <span className="club-figure__icon">🧑‍💼</span>
            <span className="club-figure__value">{(coachName ?? club.coachName) || '—'}</span>
            <span className="club-figure__label">Técnico</span>
          </span>
          <Figure label="Jogadores" value={String(club.squadSize)} icon="👥" />
          <Figure label="Saldo em caixa" value={formatLimo(club.balance)} icon="💰" accent />
          <Figure
            label="Acessos e descensos"
            value={`⬆️ ${club.promotions}  ⬇️ ${club.relegations}`}
            icon="🔁"
          />
        </figure>

        {/* The shirt sponsor, and how many games the deal still runs. A sponsorship is paid per
            match, so the deal's length in games is what the manager reads here: a club whose
            sponsor still has matches to pay is one that cannot be shopped around. */}
        <section className="club-sponsor">
          <h3 className="club-section-title">Patrocinador do momento</h3>
          {sponsorName ? (
            <p className="club-sponsor__line">
              <span className="club-sponsor__name">{sponsorName}</span>
              {sponsorIndustry && <span className="club-sponsor__industry">• {sponsorIndustry}</span>}
              <span className="club-sponsor__left">
                {sponsorMatchesLeft} {sponsorMatchesLeft === 1 ? 'jogo' : 'jogos'} restantes
              </span>
            </p>
          ) : (
            <p className="club-sponsor__line club-sponsor__none">
              Sem patrocinador — assine um para começar a faturar.
            </p>
          )}
        </section>

        <KitWall team={selectedTeam} sponsorName={sponsorName ?? ''} onEdit={() => setEditor('kits')} />

        {editor === 'crest' && (
          <CrestEditor
            team={selectedTeam}
            onClose={() => setEditor(null)}
            onSaved={setSelectedTeam}
          />
        )}

        {editor === 'kits' && (
          <KitEditor team={selectedTeam} onClose={() => setEditor(null)} onSaved={setSelectedTeam} />
        )}

        {/* The two halves of the same question — what the club has won and what has been
            done to it — wrapped in one element so a wide desktop can put them side by
            side. Below that width it is not a grid at all, and the two are one column
            again: the wrapper exists to be divided, not to be a row of its own. */}
        <div className="club-page__lower">
          <TrophyShelf trophies={club.trophies} />

          {/* The history, newest first. A club's history is a career and a career is read from
              now backwards, so the moment a manager is living through is the first line he sees
              and the season the club was founded is the last. */}
          <section className="club-history">
            <h3 className="club-section-title">Grandes momentos</h3>
            {club.history.length === 0 ? (
              <p className="league-empty">Nada aconteceu ainda. A história começa no próximo jogo.</p>
            ) : (
              <ol className="club-timeline">
                {club.history.map(event => (
                  <TimelineEvent event={event} key={event.id} />
                ))}
              </ol>
            )}
          </section>
        </div>
      </div>
    </div>
  );
};

/**
 * The two shirts, side by side, in the club's own colours and cuts.
 *
 * A manager recognises his club by its colours long before he reads its name, which is why they
 * are the one thing on this page that is not a stand-in — and the cut is the other half of that
 * recognition: a club in four thin white stripes is not the club in a solid white. Both shirts
 * are shown because both exist: the second one is there to be changed into when the first one
 * clashes with somebody else's, and a manager who has never been told that has no idea why his
 * visitors came out in yellow.
 *
 * The home shirt wears the master sponsor's name because that is what a shirt is for, and the
 * away shirt does not, because a strip that is only there to be a different shape is a real
 * thing too.
 */
const KitWall: React.FC<{ team: TeamDto; sponsorName: string; onEdit: () => void }> = ({ team, sponsorName, onEdit }) => {
  /**
   * The two numbers printed on the shirts, which are two men's own numbers and not a guess.
   *
   * This used to be worked out of the club's id so that every club had a different pair and the
   * same pair for ever. That is the mock this page came to be without: a shirt is a thing a
   * player wears, and a number drawn out of a guid is a number nobody wears. So it is read off
   * the squad — two distinct men of the current season's list — and a club whose squad could
   * not be read is drawn without a number rather than with one invented.
   */
  const [shirtNumbers, setShirtNumbers] = useState<number[]>([]);

  useEffect(() => {
    let alive = true;

    SeasonApi.current()
      .then(season => TeamApi.getSquad(team.id, season.id))
      .then(squad => {
        if (!alive) return;
        // A squad's shirt numbers are unique inside a club, so taking the first two distinct
        // ones gives two different men rather than the same number printed twice.
        setShirtNumbers(
          squad
            .map(player => player.shirtNumber)
            .filter((number): number is number => number != null && number > 0)
            .filter((number, index, all) => all.indexOf(number) === index)
            .slice(0, 2)
        );
      })
      .catch(() => {
        if (alive) setShirtNumbers([]);
      });

    return () => {
      alive = false;
    };
  }, [team]);

  const home = kitOf(team, 'Home');
  const away = team.awayKit ?? null;

  return (
    <section className="kit-wall">
      <h3 className="club-section-title">
        Uniformes
        <button className="ctrl kit-wall__edit" onClick={onEdit}>
          Editar
        </button>
      </h3>
      <div className="kit-wall__row">
        {home && (
          <KitShirt
            kit={home}
            number={shirtNumbers[0]}
            sponsor={sponsorName}
            caption="Casa"
            label={`Uniforme principal do ${team.name}`}
          />
        )}
        {away ? (
          <KitShirt
            kit={away}
            number={shirtNumbers[1]}
            sponsor={sponsorName}
            caption="Fora"
            label={`Uniforme reserva do ${team.name}`}
          />
        ) : (
          <p className="kit-wall__none">
            Sem uniforme de reserva — é o que o jogo troca quando as duas cores do confronto se
            confundem.
          </p>
        )}
      </div>
    </section>
  );
};

const TimelineEvent: React.FC<{ event: ClubHistoryEventDto }> = ({ event }) => {
  const known = EVENTS[event.kind];

  return (
    <li className="club-timeline__item">
      <span className="club-timeline__icon">{known?.icon ?? '•'}</span>
      <div className="club-timeline__body">
        <span className="club-timeline__label">{known?.label ?? event.kind}</span>
        <span className="club-timeline__text">{event.description}</span>
      </div>
      {/* A moment that belongs to no season says so, rather than being handed the nearest one.
          A manager renames a club in the winter quite often, and dating that decision by a
          football season that had nothing to do with it is a small lie in the one place on
          this page a manager is reading a record of what actually happened to his club. */}
      <span className="club-timeline__season">{event.seasonName ?? '—'}</span>
    </li>
  );
};

export default ClubScreen;
