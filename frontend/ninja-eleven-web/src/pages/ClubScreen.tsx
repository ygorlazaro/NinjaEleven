import { SeasonApi, TeamApi, SponsorApi, ManagerApi } from '@/api';
import KitShirt from '@/components/Club/KitShirt';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import { mockClubProfile } from '@/mock/clubProfile';
import { useClubWindow } from '@/services/clubColors';
import { formatLimo } from '@/services/limo';
import { useGameState } from '@/state';
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
  StadiumUpgrade: { label: 'Estádio', icon: '🏟' },
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
 * A club's shield, drawn as a placeholder in the club's own colours.
 *
 * The shape and the two colours are all there is: the real badge is a piece of artwork the
 * game does not have yet, and a blank space would leave the page looking broken rather than
 * unfinished. So the shield is a shield, wearing the colours the club wears, and when the
 * artwork arrives it replaces this and nothing else on the page moves.
 */
const ClubCrest: React.FC<{ primary: string; secondary: string; name: string }> = ({
  primary,
  secondary,
  name
}) => (
  <span
    className="club-crest"
    style={{ background: primary, borderColor: secondary, color: secondary }}
    role="img"
    aria-label={`Escudo do ${name}`}
    title="Escudo ainda não existe: um placeholder nas cores do clube"
  >
    {name
      .split(' ')
      .filter(word => word.length > 2)
      .slice(0, 2)
      .map(word => word[0])
      .join('')}
  </span>
);

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
 * **This page reads a stand-in.** The screen, the layout and the numbers are the ones the
 * endpoint will serve, and the mock lives in `mock/clubProfile` — one file, one swap.
 *
 * Most of what it says is already a fact the game keeps: the size of the roster, the balance,
 * the divisions the club has moved between, and the trophies on the shelf. Two are not, and
 * both are the two a page is *for*. **A club has no manager anywhere in the world** — there is
 * no coach entity, no column, nothing — and a club has no record of having been renamed or of
 * having rebuilt its stadium, because no event has ever been written down. The history is the
 * bigger of the two pieces of work behind this page, and the one that makes it worth having.
 */
const ClubScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);
  const [seasonId, setSeasonId] = useState<string>('');
  const [balance, setBalance] = useState<number | null>(null);
  const [squadSize, setSquadSize] = useState<number | null>(null);
  const [sponsorName, setSponsorName] = useState<string | null>(null);
  const [sponsorIndustry, setSponsorIndustry] = useState<string | null>(null);
  const [sponsorMatchesLeft, setSponsorMatchesLeft] = useState<number>(0);
  const [coachName, setCoachName] = useState<string | null>(null);
  const [editingCoach, setEditingCoach] = useState(false);
  const [coachInput, setCoachInput] = useState('');

  useEffect(() => {
    let alive = true;

    SeasonApi.current()
      .then(season => {
        if (alive) setSeasonId(season.id);
      })
      .catch(() => {});

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (!selectedTeam || !seasonId) return;

    let cancelled = false;

    const load = async () => {
      try {
        const [finance, squad, book] = await Promise.all([
          TeamApi.getFinance(selectedTeam.id, seasonId),
          TeamApi.getSquad(selectedTeam.id, seasonId),
          SponsorApi.getBook(selectedTeam.id, seasonId),
        ]);

        if (cancelled) return;

        setBalance(finance.balance);
        setSquadSize(squad.length);
        setSponsorName(book.current?.name ?? null);
        setSponsorIndustry(book.current?.industry ?? null);
        setSponsorMatchesLeft(book.matchesLeft);
        ManagerApi.getByTeam(selectedTeam.id)
          .then(manager => { if (!cancelled) setCoachName(manager.name); })
          .catch(() => {});
      } catch {
        if (!cancelled) {
          setBalance(0);
          setSquadSize(23);
        }
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [selectedTeam, seasonId]);

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
      setEditingCoach(false);
    } catch (err) {
      // If the rename fails the local value is not changed, so the name on the screen
      // stays honest: a name that did not save is a name the screen should not claim.
    }
  };

  // The stand-in fills the parts of the page the backend has not grown into yet — the titles
  // on the shelf, the career's arc — and the screen overrides the numbers the backend now owns:
  // the balance, the squad size, the shirt sponsor, and the manager's name.
  const club: ClubProfileDto = { ...mockClubProfile(selectedTeam) };
  club.balance = balance ?? club.balance;
  club.squadSize = squadSize ?? club.squadSize;
  if (coachName) club.coachName = coachName;

  return (
    <div className="app">
      <div className="card team-view-card club-modal club-page" style={clubWindow}>
        {/* The shield and the name, side by side: the badge is how a manager recognises a club
            at a glance, so it goes where the eye lands first and the name comes with it. */}
        <header className="club-page__head">
          <ClubCrest
            primary={club.primaryColor}
            secondary={club.secondaryColor}
            name={club.name}
          />
          <div className="club-page__identity">
            <h2 className="profile-name">{club.name}</h2>
            <p className="club-page__tag">
              {club.shortName} • {club.coachName}
            </p>
          </div>
          <div className="club-page__actions">
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
            <span className="club-figure__value">
              {editingCoach ? (
                <span className="coach-edit">
                  <input
                    className="ctrl coach-edit__input"
                    value={coachInput}
                    onChange={e => setCoachInput(e.target.value)}
                    placeholder={club.coachName}
                    autoFocus
                  />
                  <button className="ctrl coach-edit__save" onClick={handleRenameCoach}>Salvar</button>
                  <button className="ctrl" onClick={() => { setEditingCoach(false); setCoachInput(''); }}>Cancelar</button>
                </span>
              ) : (
                club.coachName
              )}
            </span>
            <span className="club-figure__label">
              Técnico
              {coachName && !editingCoach && (
                <button
                  className="coach-edit__icon"
                  title="Renomear técnico"
                  onClick={() => { setEditingCoach(true); setCoachInput(coachName); }}
                >
                  ✏️
                </button>
              )}
            </span>
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

        <KitWall team={selectedTeam} />

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
  );
};

/**
 * The two shirts, side by side, in the club's colours.
 *
 * A club's colours are the game's own (`teams.primary_color` and `secondary_color`) and a
 * manager recognises his club by them long before he reads its name, which is why they are
 * the one thing on this page that is not a stand-in. Everything else about a kit is invented:
 * the game keeps no shirt, no pattern and no squad number, so the cut is drawn from the club's
 * id and the number is too. The home shirt wears the master sponsor's name because that is
 * what a shirt is for, and the away shirt does not, because a strip that is only there to be a
 * different shape is a real thing too.
 */
const KitWall: React.FC<{ team: TeamDto }> = ({ team }) => {
  const seed = [...team.id].reduce((sum, character) => sum + character.charCodeAt(0), 0);
  const [sponsorName, setSponsorName] = useState('');

  useEffect(() => {
    let alive = true;

    SeasonApi.current()
      .then(season => SponsorApi.getBook(team.id, season.id))
      .then(book => {
        if (alive) {
          setSponsorName(book.current?.name ?? '');
        }
      })
      .catch(() => {
        if (alive) setSponsorName('');
      });

    return () => {
      alive = false;
    };
  }, [team]);

  const { primaryColor, secondaryColor } = team;

  return (
    <section className="kit-wall">
      <h3 className="club-section-title">Uniformes</h3>
      <div className="kit-wall__row">
        <KitShirt
          variant="home"
          primary={primaryColor}
          secondary={secondaryColor}
          seed={team.id}
          number={(seed % 20) + 1}
          sponsor={sponsorName}
        />
        <KitShirt
          variant="away"
          primary={secondaryColor}
          secondary={primaryColor}
          seed={team.id}
          number={((seed * 7) % 20) + 1}
        />
      </div>
    </section>
  );
};

const TimelineEvent: React.FC<{ event: ClubHistoryEventDto }> = ({ event }) => {  const known = EVENTS[event.kind];

  return (
    <li className="club-timeline__item">
      <span className="club-timeline__icon">{known?.icon ?? '•'}</span>
      <div className="club-timeline__body">
        <span className="club-timeline__label">{known?.label ?? event.kind}</span>
        <span className="club-timeline__text">{event.description}</span>
      </div>
      <span className="club-timeline__season">{event.seasonName}</span>
    </li>
  );
};

export default ClubScreen;
