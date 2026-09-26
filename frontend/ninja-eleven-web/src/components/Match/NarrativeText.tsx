import React, { useMemo } from 'react';
import { useProfiles } from '@/state/ProfileProvider';

/**
 * One name the narration used, and who it belongs to.
 */
export interface NameRef {
  /** The name exactly as it appears in the sentence. */
  text: string;
  kind: 'player' | 'team';
  id: string;
}

export interface NameIndex {
  players: Map<string, NameRef>;
  teams: Map<string, NameRef>;
}

/**
 * Builds the lookup of every name a sentence about this match could use: the two clubs and
 * the twenty-two men on the pitch, plus the bench.
 *
 * A name that two people share is left out on purpose. The feed says "Balançou a rede! X
 * finalizou", and if the squad has two men called X there is no honest way to say which one
 * the sentence means — so the name stays plain text rather than becoming a link that opens
 * the wrong profile half the time.
 */
export const buildNameIndex = (
  teams: { id: string; name: string; shortName?: string | null }[],
  players: { id: string; name: string }[]
): NameIndex => {
  const playersByName = new Map<string, NameRef[]>();
  const teamsByName = new Map<string, NameRef[]>();

  const add = (map: Map<string, NameRef[]>, text: string, ref: NameRef) => {
    const key = text.trim();
    if (!key) return;
    map.set(key, [...(map.get(key) ?? []), ref]);
  };

  for (const team of teams) {
    add(teamsByName, team.name, { text: team.name, kind: 'team', id: team.id });
    if (team.shortName) {
      add(teamsByName, team.shortName, { text: team.shortName, kind: 'team', id: team.id });
    }
  }

  for (const player of players) {
    add(playersByName, player.name, { text: player.name, kind: 'player', id: player.id });
  }

  const unique = (map: Map<string, NameRef[]>) => {
    const result = new Map<string, NameRef>();
    for (const [name, refs] of map) {
      if (refs.length === 1) result.set(name, refs[0]);
    }
    return result;
  };

  return { players: unique(playersByName), teams: unique(teamsByName) };
};

const escapeForRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * A sentence of the feed with every name in it turned into a door.
 *
 * The names are matched as whole words against the eleven (plus the bench) of this very
 * match, longest first so "Pedro Alves" is never half-matched by "Pedro". The screen
 * decides only where the links go; the sentence itself is the one the engine wrote, and
 * nothing about it is recomputed here.
 */
const NarrativeText: React.FC<{ text: string; index: NameIndex | null }> = ({ text, index }) => {
  const { openPlayer, openTeam } = useProfiles();

  const parts = useMemo(() => {
    if (!index) return [{ text, ref: null }];

    const names = [...index.players.values(), ...index.teams.values()].sort(
      (a, b) => b.text.length - a.text.length
    );

    if (names.length === 0) return [{ text, ref: null }];

    // One pass over the sentence, and every name claimed at once, so the output is the
    // original text with names replaced by links — never a re-ordered or re-worded
    // sentence.
    const pattern = new RegExp(`\\b(${names.map(name => escapeForRegExp(name.text)).join('|')})\\b`, 'g');
    const found: { text: string; ref: NameRef | null }[] = [];
    let cursor = 0;
    let match: RegExpExecArray | null;

    while ((match = pattern.exec(text)) !== null) {
      if (match.index > cursor) {
        found.push({ text: text.slice(cursor, match.index), ref: null });
      }

      const name = match[1];
      const ref = index.players.get(name) ?? index.teams.get(name) ?? null;
      found.push({ text: name, ref });
      cursor = match.index + name.length;
    }

    if (cursor < text.length) {
      found.push({ text: text.slice(cursor), ref: null });
    }

    return found;
  }, [text, index]);

  return (
    <>
      {parts.map((part, index) => {
        if (!part.ref) return <React.Fragment key={index}>{part.text}</React.Fragment>;

        return (
          <button
            key={index}
            type="button"
            className={`name-link narrative-link ${part.ref.kind}`}
            onClick={event => {
              event.stopPropagation();
              if (part.ref!.kind === 'player') openPlayer(part.ref!.id);
              else openTeam(part.ref!.id);
            }}
          >
            {part.text}
          </button>
        );
      })}
    </>
  );
};

export default NarrativeText;
