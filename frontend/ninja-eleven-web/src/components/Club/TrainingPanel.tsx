import React from 'react';
import type { SquadTrainingQuotesDto, TrainingQuoteDto, PlayerAttribute, TeamDto } from '@/types';
import { positionLabel, energyTextClass } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { PlayerName } from '@/components/Common/Names';
import KitChip from '@/components/Club/KitChip';

/**
 * The eight, in the order the squad table reads them, with the short labels it already uses.
 *
 * A training screen that spelled the attributes out in full would be a second vocabulary for
 * the same eight numbers, and a manager who has read "Cab" in the squad table all season would
 * have to learn "Cabeceio" to make a decision here. The label travels with the wire name, so
 * the backend can be renamed without the screen being a second thing to keep in step.
 */
const ATTRIBUTES: ReadonlyArray<{ key: PlayerAttribute; label: string }> = [
  { key: 'Speed', label: 'Vel' },
  { key: 'Accuracy', label: 'Fin' },
  { key: 'Dribbling', label: 'Dri' },
  { key: 'Heading', label: 'Cab' },
  { key: 'Strength', label: 'For' },
  { key: 'GoalkeeperPower', label: 'Gol' },
  { key: 'Reflexes', label: 'Ref' },
  { key: 'Stamina', label: 'Est' }
];

interface TrainingPanelProps {
  /** The club these men belong to, so the shirt sits beside the name. */
  team?: TeamDto | null;
  quotes: SquadTrainingQuotesDto | null;
  loading: boolean;
  error: string | null;

  /** The man being trained right now, so his row is the only one that stops answering. */
  busyPlayerId: string | null;

  /**
   * Runs one session. The panel does not call the API itself: the screen above owns the
   * squad as well as the sheet, and a session that raised an attribute and left the squad
   * table showing the old number would be a game with two answers to "how good is he".
   */
  onTrain: (playerId: string, attribute: PlayerAttribute) => void;
}

/**
 * A manager's week, spent.
 *
 * <para>
 * The point of this screen is the price. Every cell is an attribute and what a session on it
 * costs, because the decision a manager is making is not "should I train him" but "should I
 * train him <i>on this</i> instead of something cheaper", and a screen that showed only the
 * attribute would have answered the first question and left the second one to arithmetic.
 * </para>
 *
 * <para>
 * A cell with no price is a cell with nothing to offer, and it says so by being flat rather
 * than by being hidden: a goalkeeper attribute on a centre-back and an attribute already at
 * the man's ceiling are both <i>refusals</i> rather than absences, and a screen that dropped
 * them would leave a manager wondering whether the backend had an attribute he did not. The
 * reason is on the cell, so the rule behind it is never the manager's to guess at.
 * </para>
 */
const TrainingPanel: React.FC<TrainingPanelProps> = ({
  team,
  quotes,
  loading,
  error,
  busyPlayerId,
  onTrain
}) => {
  if (loading) {
    return <p className="league-empty">Lendo o elenco…</p>;
  }

  if (error) {
    return <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>;
  }

  if (!quotes || quotes.players.length === 0) {
    return <p className="league-empty">Nenhum jogador para treinar.</p>;
  }

  return (
    <section className="training">
      <div className="training__head">
        <h4 className="club-form__title">Treino</h4>
        <p className="training__budget">
          Energia do elenco: <strong>{quotes.squadEnergy}</strong>
          {' '}({quotes.playsToday ? 'dia de jogo' : 'sem jogo'})
        </p>
      </div>

      <p className="training__hint">
        Cada sessão sobe um atributo e custa energia ao jogador e dinheiro ao clube. O preço em
        energia sobe conforme o atributo se aproxima do potencial — treinar um jovem é barato,
        treinar o último ponto de um veterano custa a temporada. O preço em dinheiro é 15% do
        salário de quem treina, e é o mesmo para todo o clube: as duas sessões do dia não são
        uma por jogador.
      </p>

      <ul className="training__list">
        {quotes.players.map(player => (
          <PlayerSheet
            key={player.playerId}
            player={player}
            busy={busyPlayerId === player.playerId}
            anyBusy={busyPlayerId !== null}
            onTrain={onTrain}
            team={team}
          />
        ))}
      </ul>
    </section>
  );
};

/**
 * One man's eight cells.
 *
 * It is its own component so the busy state is a row's own business: a session in progress
 * greys one man's eight cells rather than the whole screen, because the other twenty-two
 * sheets are still perfectly valid prices and there is no reason to make a manager wait for
 * them.
 */
const PlayerSheet: React.FC<{
  player: TrainingQuoteDto;
  busy: boolean;
  anyBusy: boolean;
  onTrain: (playerId: string, attribute: PlayerAttribute) => void;
  /** The club the man belongs to, so the shirt sits beside his name. */
  team?: TeamDto | null;
}> = ({ player, busy, anyBusy, onTrain, team }) => (
  <li
    className={[
      'training__player',
      player.isAvailable ? '' : 'training__player--out',
      busy ? 'training__player--busy' : ''
    ]
      .filter(Boolean)
      .join(' ')}
  >
    <div className="training__who">
      <span className="squad-preview__pos">{positionLabel(player.position)}</span>
      <span className="training__name">
        <KitChip team={team} />
        <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
      </span>
      <span className="training__meta">{player.age} anos</span>
      <span className="training__meta" title="O teto do jogador: a melhor leitura que ele deve alcançar">
        Pot {player.potential}
      </span>
      <span className="training__meta" title="O que uma sessão custa ao clube: 15% do salário dele">
        {formatLimo(player.sessionFee)}
      </span>
      <span className={`training__meta ${energyTextClass(player.energy)}`} title="Energia">
        {player.energy}
      </span>
      {player.injury !== 'None' && (
        <span className="injury-mark" title={`Lesionado: ${player.injury}`}>🩹</span>
      )}
    </div>

    <div className="training__cells">
      {ATTRIBUTES.map(({ key, label }) => {
        const cell = player.attributes.find(attribute => attribute.attribute === key);

        if (!cell) return null;

        // Three refusals, and they are different facts so they are said differently: the man
        // cannot train at all, this attribute is not his, or he is already at the top.
        const out = !player.isAvailable;
        const notHis = cell.cost === null;
        const tooDear = cell.cost !== null && cell.cost > player.energy;
        const disabled = notHis || tooDear || out || anyBusy;

        const reason = out
          ? 'Indisponível'
          : notHis
            ? (cell.value > 0 ? 'No potencial' : 'Não é um atributo dele')
            : tooDear
              ? `Precisa de ${cell.cost} de energia`
              : `Treinar por ${cell.cost} de energia e ${formatLimo(player.sessionFee)} do clube`;

        return (
          <button
            key={key}
            type="button"
            className={`training__cell ${disabled ? 'training__cell--off' : ''}`}
            disabled={disabled}
            title={reason}
            onClick={() => onTrain(player.playerId, key)}
          >
            <span className="training__cell-label">{label}</span>
            <span className="training__cell-value">{cell.value}</span>
            <span className="training__cell-cost">{cell.cost ?? '—'}</span>
          </button>
        );
      })}
    </div>
  </li>
);

export default TrainingPanel;
