import React, { useMemo, useState } from 'react';
import type { ScorerDto } from '@/types';
import { ClubName, PlayerName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';

interface ScorersListProps {
  scorers: ScorerDto[];
  /** The club the manager commands: the list is about his own goalscorers by default. */
  userTeamId?: string;
  userTeamName?: string;
}

type Scope = 'mine' | 'all';

/** How many names each view shows. The league list is a digest, not the whole season. */
const LIMIT = 15;

/**
 * The scoring chart, opened on the manager's own club. A manager cares first about who is
 * scoring for him, so his players lead; the whole league is one click away for the rest,
 * and the toggle is remembered for as long as the screen is mounted.
 */
const ScorersList: React.FC<ScorersListProps> = ({ scorers, userTeamId, userTeamName }) => {
  const [scope, setScope] = useState<Scope>('mine');

  const mine = useMemo(
    () => (userTeamId ? scorers.filter(s => s.teamId === userTeamId) : []),
    [scorers, userTeamId]
  );

  // The ranking the API sent is global, so the club's own players are renumbered inside it
  // rather than inheriting a position that belongs to another team's forward.
  const rows = useMemo(() => {
    const source = scope === 'mine' ? mine : scorers;

    return source
      .slice()
      .sort((a, b) => b.goals - a.goals || a.age - b.age || a.playerName.localeCompare(b.playerName, 'pt-BR'))
      .slice(0, LIMIT);
  }, [scope, mine, scorers]);

  const hasClub = !!userTeamId;

  if (scorers.length === 0) {
    return <div className="league-empty">Ainda não há gols no campeonato.</div>;
  }

  return (
    <>
      {hasClub && (
        <div className="player-team-tabs scorer-tabs">
          <button
            className={`player-team-tab ${scope === 'mine' ? 'active' : ''}`}
            onClick={() => setScope('mine')}
          >
            Meu time ({mine.length})
          </button>
          <button
            className={`player-team-tab ${scope === 'all' ? 'active' : ''}`}
            onClick={() => setScope('all')}
          >
            Campeonato ({scorers.length})
          </button>
        </div>
      )}

      {rows.length === 0 ? (
        <div className="league-empty">
          {scope === 'mine'
            ? `${userTeamName ?? 'Seu time'} ainda não marcou nenhum gol.`
            : 'Ainda não há gols no campeonato.'}
        </div>
      ) : (
        <table className="scorers">
          <thead>
            <tr>
              <th>#</th>
              <th>Jogador</th>
              <th>Idade</th>
              <th>Gols</th>
              {scope === 'all' && <th>Time</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map((s, i) => (
              <tr key={s.playerId} className={s.teamId === userTeamId ? 'user-row' : undefined}>
                <td>{i + 1}</td>
                <td>
                  {/* The club beside the man, not only in the last column: on the manager's own
                      club there is no club column, and a name on its own says who scored
                      without saying for whom. */}
                  <span className="scorer-player">
                    {s.teamPrimaryColor && s.teamSecondaryColor && (
                      <ClubCrest
                        primary={s.teamPrimaryColor}
                        secondary={s.teamSecondaryColor}
                        name={s.teamName}
                        className="mini-crest"
                      />
                    )}
                    <PlayerName playerId={s.playerId}><b>{s.playerName}</b></PlayerName>
                  </span>
                </td>
                <td>{s.age}</td>
                <td><b>{s.goals}</b></td>
                {scope === 'all' && (
                  <td>
                    <ClubName teamId={s.teamId}>{s.teamName}</ClubName>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
};

export default ScorersList;
