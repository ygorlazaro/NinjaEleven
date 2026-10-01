import React, { useMemo, useState } from 'react';
import type { KitDto, KitPattern, TeamDto } from '@/types';
import { TeamApi } from '@/api';
import KitShirt, { KIT_PATTERN_LABELS, KIT_PATTERNS } from '@/components/Club/KitShirt';

/**
 * How much light a colour gives back, and how two of them compare.
 *
 * It is the WCAG contrast rather than a distance in RGB, and for the same reason the domain
 * measures it that way: on a colour wheel a dark blue and a dark green are far apart, and on a
 * pitch they are two dark shirts. The editor says so before a manager saves one, because a kit
 * is a decision made in a room and a clash is only discovered at the whistle.
 */
const luminanceOf = (colour: string): number => {
  const value = (colour || '').replace('#', '');
  const full = value.length === 3 ? value.replace(/./g, character => character + character) : value;

  if (full.length !== 6) return 0;

  const [red, green, blue] = [0, 2, 4].map(offset => {
    const channel = parseInt(full.slice(offset, offset + 2), 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
};

const contrastOf = (first: string, second: string): number => {
  const a = luminanceOf(first);
  const b = luminanceOf(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
};

/** The same threshold the backend draws its change of shirt with. */
const CLASH = 1.6;

const readOf = (colour: string): string => (luminanceOf(colour) < 0.35 ? '#ffffff' : '#111111');

const draftFrom = (team: TeamDto): { home: KitDto; away: KitDto | null } => ({
  home: team.homeKit ?? {
    primaryColor: team.primaryColor,
    secondaryColor: team.secondaryColor,
    pattern: 'Solid',
    trimColor: readOf(team.primaryColor),
  },
  away: team.awayKit ?? {
    primaryColor: team.secondaryColor,
    secondaryColor: team.primaryColor,
    pattern: 'SolidSeparateSleeves',
    trimColor: readOf(team.secondaryColor),
  },
});

const ColourField: React.FC<{
  label: string;
  value: string;
  onChange: (colour: string) => void;
  hint?: string;
}> = ({ label, value, onChange, hint }) => (
  <label className="identity-colour">
    <span className="identity-colour__label">{label}</span>
    <span className="identity-colour__row">
      <input type="color" value={value} onChange={event => onChange(event.target.value)} />
      <input
        className="ctrl identity-colour__hex"
        value={value}
        maxLength={7}
        onChange={event => {
          const typed = event.target.value.trim();

          if (/^#?[0-9a-f]{6}$/i.test(typed)) {
            onChange(typed.startsWith('#') ? typed.toLowerCase() : `#${typed.toLowerCase()}`);
          }
        }}
      />
    </span>
    {hint && <span className="identity-colour__hint">{hint}</span>}
  </label>
);

interface KitEditorProps {
  team: TeamDto;
  onClose: () => void;
  onSaved: (team: TeamDto) => void;
}

/**
 * The kit editor: two colours, one of eight cuts, and two shirts.
 *
 * <para>
 * The second shirt exists for exactly one reason — to be changed into when the first one clashes
 * with somebody else's — so the editor asks for both in one place. It also says, before
 * anything is saved, when the two shirts are too close to each other: that is the case the
 * second shirt is for, and a manager who finds it out at the design stage has it fixed before a
 * fixture rather than after one.
 * </para>
 *
 * <para>
 * Which of the two a match uses is not chosen here. The backend draws it from the fixture's own
 * seed, so the same match is always played in the same two shirts and the manager's own players
 * are recognisable in the one he is watching.
 * </para>
 */
const KitEditor: React.FC<KitEditorProps> = ({ team, onClose, onSaved }) => {
  const initial = useMemo(() => draftFrom(team), [team]);
  const [home, setHome] = useState<KitDto>(initial.home);
  const [away, setAway] = useState<KitDto | null>(initial.away);
  const [which, setWhich] = useState<'home' | 'away'>('home');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const draft = which === 'home' ? home : away;
  const setDraft = (next: KitDto) => (which === 'home' ? setHome(next) : setAway(next));
  const patch = (part: Partial<KitDto>) => setDraft({ ...(draft as KitDto), ...part });

  const clash = away && contrastOf(home.primaryColor, away.primaryColor) < CLASH;

  const save = async () => {
    setBusy(true);
    setError(null);

    try {
      onSaved(await TeamApi.updateKits(team.id, home, away));
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || 'Não foi possível salvar o uniforme.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal" role="dialog" aria-label="Editor de uniforme">
      <div className="modal-card kit-editor">
        <h2>Uniformes do {team.name}</h2>

        <div className="kit-editor__tabs">
          <button
            type="button"
            className={`ctrl${which === 'home' ? ' is-picked' : ''}`}
            onClick={() => setWhich('home')}
          >
            Principal
          </button>
          <button
            type="button"
            className={`ctrl${which === 'away' ? ' is-picked' : ''}`}
            onClick={() => setWhich('away')}
          >
            Reserva
          </button>
        </div>

        <div className="kit-editor__body">
          <div className="kit-editor__stage">
            {draft ? (
              <KitShirt
                kit={draft}
                number={7}
                caption={which === 'home' ? 'Casa' : 'Fora'}
                label={`Uniforme ${which === 'home' ? 'principal' : 'reserva'} do ${team.name}`}
              />
            ) : (
              <p className="kit-editor__empty">
                O clube não tem uniforme de reserva. Desenhe um para poder mudar de camisa
                quando as duas cores se confundirem.
              </p>
            )}
            {clash && (
              <p className="kit-editor__warning">
                As duas cores principais são muito parecidas. O jogo vai trocar uma das camisas
                sozinho nos jogos em que isso acontecer — mas vale a pena deixar mais diferença.
              </p>
            )}
          </div>

          <div className="kit-editor__controls">
            {draft ? (
              <>
                <div className="crest-editor__colours">
                  <ColourField
                    label="Cor principal"
                    value={draft.primaryColor}
                    onChange={colour => patch({ primaryColor: colour })}
                    hint="O corpo da camisa."
                  />
                  <ColourField
                    label="Cor secundária"
                    value={draft.secondaryColor}
                    onChange={colour => patch({ secondaryColor: colour })}
                    hint="Listras, faixas e mangas."
                  />
                </div>

                <label className="identity-check">
                  <input
                    type="checkbox"
                    checked={!!draft.trimColor}
                    onChange={event =>
                      patch({ trimColor: event.target.checked ? readOf(draft.primaryColor) : null })
                    }
                  />
                  <span>Escolher a cor do número</span>
                </label>

                {draft.trimColor && (
                  <ColourField
                    label="Cor do número"
                    value={draft.trimColor}
                    onChange={colour => patch({ trimColor: colour })}
                    hint="Uma cor que não é do clube."
                  />
                )}

                <fieldset className="identity-group">
                  <legend>Padrão</legend>
                  <div className="identity-patterns">
                    {KIT_PATTERNS.map(pattern => (
                      <button
                        key={pattern}
                        type="button"
                        className={`identity-pattern${draft.pattern === pattern ? ' is-picked' : ''}`}
                        onClick={() => patch({ pattern: pattern as KitPattern })}
                        title={KIT_PATTERN_LABELS[pattern]}
                      >
                        <KitShirt kit={{ ...draft, pattern }} label={KIT_PATTERN_LABELS[pattern]} />
                        <span>{KIT_PATTERN_LABELS[pattern]}</span>
                      </button>
                    ))}
                  </div>
                </fieldset>
              </>
            ) : (
              <button className="ctrl primary" onClick={() => setAway({ ...home })}>
                Desenhar uniforme de reserva
              </button>
            )}
          </div>
        </div>

        {error && <div className="error">{error}</div>}

        <div className="modal-actions">
          {away && (
            <button className="ctrl" onClick={() => setAway(null)} disabled={busy}>
              Sem reserva
            </button>
          )}
          <button className="ctrl" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button className="ctrl primary" onClick={save} disabled={busy}>
            {busy ? 'Salvando…' : 'Salvar uniformes'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default KitEditor;
