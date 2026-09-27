import type { SponsorBookDto, SponsorOfferDto, StadiumProfileDto, TeamDto } from '@/types';

/**
 * A ground and a sponsor book, drawn from stand-ins while there are no endpoints to read.
 *
 * What the game keeps today, so that the swap is a matter of fetching and not of rewriting:
 * the stadium is a row with a name, a capacity and **one** ticket price; a club has no
 * sponsor, no sponsor contract and no shirt anywhere in the schema.
 *
 * So the honest split is: the ground's name, capacity and colours come from the real club and
 * the real stadium, the **city** is invented (a ground has an address and the game does not
 * keep it), and **both ticket prices are invented** — because a ground has exactly one price
 * today and separating the league from the cup is a change to `AttendanceCalculator` and to
 * the `stadiums` table, not a field a screen may fill in. A control that moved a number
 * nothing read would be a control that lied, so it is marked as one.
 *
 * Everything sponsor-shaped is invented, all of it, and that is the whole of it: a sponsor is
 * the first feature in this game that is about a club's business rather than about its
 * football, and it is the first thing here with no domain behind it at all.
 */

const CITIES = [
  'Recife', 'Curitiba', 'Belo Horizonte', 'Porto Alegre', 'Goiânia', 'Campinas',
  'Salvador', 'Fortaleza', 'Florianópolis', 'Natal', 'Cuiabá', 'Vitória'
];

const STADIUM_NAMES = [
  'Estádio Municipal', 'Arena do Vale', 'Estádio das Palmeiras', 'Estádio Beira-Rio',
  'Estádio do Parque', 'Estádio Serrinha', 'Estádio do Vale do Sol', 'Estádio da Colina'
];

const SPONSOR_NAMES = [
  { name: 'Cia. Energética Paulista', industry: 'Energia' },
  { name: 'Banco do Vale', industry: 'Financeiro' },
  { name: 'Rede Ferrovia do Sul', industry: 'Transportes' },
  { name: 'Construtora Rocha & Filhos', industry: 'Construção' },
  { name: 'Cooperativa Aurora', industry: 'Alimentos' },
  { name: 'Telecom Nordeste', industry: 'Telecomunicações' },
  { name: 'Grupo Martelo', industry: 'Varejo' },
];

const SPONSOR_COLORS = ['#E0A800', '#2E7D32', '#1565C0', '#6A1B9A', '#AD1457', '#00838F'];

/** A number that is the same for a club and different between clubs. */
const stableSeed = (team: TeamDto): number =>
  [...team.id].reduce((sum, character) => sum + character.charCodeAt(0), 0);

/**
 * A ground, in the club's own colours and at a size a club of this division would have.
 *
 * The capacity is not random: a club's ground is one of the two things that decide which
 * division it can afford to be in, so a bigger ground belongs to a stronger club and the seed
 * is drawn from the club's id rather than from the clock. When the real ground is read, the
 * number that arrives is this one's, and this one is thrown away.
 */
export const mockStadium = (team: TeamDto, realStadium?: { name: string; capacity: number; ticketPrice: number }): StadiumProfileDto => {
  const seed = stableSeed(team);

  return {
    teamId: team.id,
    // The name and the capacity are the club's when the game has them: a ground that already
    // exists in the world is not re-invented here.
    name: realStadium?.name ?? STADIUM_NAMES[seed % STADIUM_NAMES.length],
    capacity: realStadium?.capacity ?? 8_000 + (seed % 5) * 6_000,
    // The one number the game does keep is the price a league seat costs, and a mock that
    // contradicted it would put two prices for the same seat on two screens.
    leagueTicketPrice: realStadium?.ticketPrice ?? 10,
    // A cup tie is worth more than a league match and the price says so: the engine's own
    // demand factor charges a dearer seat a fuller ground, so a cup at the league's price is a
    // cup the club is giving away.
    cupTicketPrice: Math.round((realStadium?.ticketPrice ?? 10) * 2.5),
    city: CITIES[seed % CITIES.length],
    primaryColor: team.primaryColor || '#f2d34f',
    secondaryColor: team.secondaryColor || '#f2d34f'
  };
};

/**
 * Five offers on the table and the one already signed.
 *
 * The shortlist is a function of the club so that the same club always shows the same five,
 * and the fees are spread around the current deal: a shortlist where every offer pays more
 * than the sponsor on the shirt is not a decision, it is a formality, and a manager should be
 * able to see at a glance that the deal he has is worth keeping.
 */
const mockSponsors = (team: TeamDto): SponsorOfferDto[] => {
  const seed = stableSeed(team);

  return [0, 1, 2, 3, 4].map(index => {
    const sponsor = SPONSOR_NAMES[(seed + index * 3) % SPONSOR_NAMES.length];
    const length = 6 + ((seed + index) % 3) * 6;

    return {
      id: `${team.id}-sponsor-${index}`,
      name: sponsor.name,
      industry: sponsor.industry,
      perMatchFee: 1_800 + ((seed + index * 613) % 9) * 420,
      contractMatches: length,
      color: SPONSOR_COLORS[(seed + index) % SPONSOR_COLORS.length]
    };
  });
};

/**
 * The sponsor on the shirt and the five waiting to be.
 *
 * The current sponsor is the first of the shortlist, so a manager changing clubs sees a club
 * that has signed one of the five offers rather than a club whose sponsor came from nowhere.
 * How many matches are left is the fact the whole screen turns on: it is the length of the
 * deal the club has sold, less how much of it has been played.
 */
export const mockSponsorBook = (team: TeamDto, matchesPlayed = 7): SponsorBookDto => {
  const candidates = mockSponsors(team);
  const current = candidates[0];
  // Seven matches played of a deal of twelve: the club has five left, which is the state a
  // screen is most often in and the one where the rule bites — the deal cannot be broken.
  const contractMatches = current.contractMatches;

  return {
    teamId: team.id,
    current,
    matchesLeft: Math.max(0, contractMatches - matchesPlayed),
    candidates,
    masterSponsorId: current.id
  };
};
