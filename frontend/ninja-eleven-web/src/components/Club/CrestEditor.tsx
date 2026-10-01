import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { CrestDto, CrestFigureKind, CrestShape, TeamDto } from '@/types';
import { TeamApi } from '@/api';
import CrestSvg, {
  CREST_ELEMENT_RANGE,
  CREST_FIGURES,
  CREST_SHAPES,
  crestPositionFromPercent,
  crestTopPercent,
} from '@/components/Club/CrestSvg';

/** What the manager calls each of the ten shields. */
const SHAPE_LABELS: Record<CrestShape, string> = {
  Round: 'Redondo',
  Oval: 'Oval',
  Shield: 'Escudo',
  EaredShield: 'Escudo com orelhas',
  Hexagon: 'Hexágono',
  Squircle: 'Quadrado arredondado',
  Pennant: 'Pena',
  Banner: 'Bandeira',
  Diamond: 'Losango',
  Star: 'Estrela',
};

const FIGURE_LABELS: Record<CrestFigureKind, string> = {
  None: 'Nenhuma',
  Ball: 'Bola',
  Star: 'Estrela',
  Flame: 'Chama',
  Bolt: 'Raio',
  Crown: 'Coroa',
  Wave: 'Onda',
  Sword: 'Espada',
  Anchor: 'Âncora',
};

/**
 * What the editor is holding while it is open.
 *
 * It is a flat draft rather than the crest itself because the crest cannot hold the two states
 * the editor offers: an element that is switched off but still has a colour and a height it was
 * dragged to. Turning the lettering back on has to bring back the lettering as it was, and a
 * crest that had thrown its position away would have to be dragged again.
 */
type Draft = {
  shape: CrestShape;
  primaryColor: string;
  secondaryColor: string;
  hasText: boolean;
  textContent: string;
  textColor: string;
  textPosition: number;
  hasEmblem: boolean;
  emblem: CrestFigureKind;
  emblemColor: string;
  emblemPosition: number;
};

const blackOrWhite = (colour: string): string => {
  const value = colour.replace('#', '');
  const full = value.length === 3 ? value.replace(/./g, character => character + character) : value;

  if (full.length !== 6) return '#ffffff';

  const [red, green, blue] = [0, 2, 4].map(offset => {
    const channel = parseInt(full.slice(offset, offset + 2), 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue < 0.35 ? '#ffffff' : '#111111';
};

const draftFrom = (team: TeamDto): Draft => {
  const crest = team.crest;

  if (!crest) {
    const ink = blackOrWhite(team.secondaryColor);

    return {
      shape: 'Shield',
      primaryColor: team.primaryColor,
      secondaryColor: team.secondaryColor,
      hasText: true,
      textContent: team.shortName,
      textColor: ink,
      textPosition: 0.6,
      hasEmblem: true,
      emblem: 'Ball',
      emblemColor: ink,
      emblemPosition: 0.3,
    };
  }

  return {
    shape: crest.shape,
    primaryColor: crest.primaryColor,
    secondaryColor: crest.secondaryColor,
    hasText: !!crest.text,
    textContent: crest.text?.content ?? team.shortName,
    textColor: crest.text?.color ?? team.primaryColor,
    textPosition: crest.text?.verticalPosition ?? 0.6,
    hasEmblem: !!crest.emblem,
    emblem: crest.emblem?.kind ?? 'Ball',
    emblemColor: crest.emblem?.color ?? team.primaryColor,
    emblemPosition: crest.emblem?.verticalPosition ?? 0.3,
  };
};

const crestFrom = (draft: Draft): CrestDto => ({
  shape: draft.shape,
  primaryColor: draft.primaryColor,
  secondaryColor: draft.secondaryColor,
  text: draft.hasText
    ? {
        content: draft.textContent.trim().slice(0, 12) || '?',
        color: draft.textColor,
        verticalPosition: draft.textPosition,
      }
    : null,
  emblem: draft.hasEmblem ? { kind: draft.emblem, color: draft.emblemColor, verticalPosition: draft.emblemPosition } : null,
});

/**
 * Which element the pointer is holding, and a way of moving it.
 *
 * The drag is by pointer rather than by the HTML5 drag and drop a row of players uses, because
 * this one is about a fraction of a drawing and the pointer is already saying where it is. A
 * dragged card is a thing being carried between two lists; a dragged letter is a thing being
 * placed inside a shape, and the browser's drag image — a ghost of the whole card — is not a
 * thing anybody wants to watch while choosing a height.
 */
function useVerticalDrag(onDrag: (which: 'text' | 'emblem', position: number) => void) {
  const canvas = useRef<HTMLDivElement | null>(null);
  const [holding, setHolding] = useState<'text' | 'emblem' | null>(null);

  useEffect(() => {
    if (!holding) return;

    const move = (event: PointerEvent) => {
      const box = canvas.current?.getBoundingClientRect();

      if (!box || box.height === 0) return;

      onDrag(holding, crestPositionFromPercent((event.clientY - box.top) / box.height));
    };

    const release = () => setHolding(null);

    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', release);
    window.addEventListener('pointercancel', release);

    return () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', release);
      window.removeEventListener('pointercancel', release);
    };
  }, [holding, onDrag]);

  return { canvas, holding, grab: setHolding };
}

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

interface CrestEditorProps {
  team: TeamDto;
  onClose: () => void;
  onSaved: (team: TeamDto) => void;
}

/**
 * The crest editor: two colours, one of ten shapes, and whatever the club wants on it.
 *
 * <para>
 * The colours are the club's and they are not free: the primary is everything in the
 * foreground, the secondary is the field the elements sit on. That split is why the editor
 * never offers a third colour for the shield itself, and does offer one for each element — a
 * red and black club writing itself in white is the ordinary case, and the white belongs to
 * the lettering rather than to the club.
 * </para>
 *
 * <para>
 * The two elements are dragged onto the shield rather than placed at a number, because "a bit
 * higher" is a thing a manager can see and "0.62 of the way down" is not.
 * </para>
 */
const CrestEditor: React.FC<CrestEditorProps> = ({ team, onClose, onSaved }) => {
  const [draft, setDraft] = useState<Draft>(() => draftFrom(team));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = <K extends keyof Draft>(key: K, value: Draft[K]) =>
    setDraft(current => ({ ...current, [key]: value }));

  const onDrag = useCallback((which: 'text' | 'emblem', position: number) => {
    const range = CREST_ELEMENT_RANGE[which];

    setDraft(current =>
      which === 'text'
        ? { ...current, textPosition: Math.min(range.to, Math.max(range.from, position)) }
        : { ...current, emblemPosition: Math.min(range.to, Math.max(range.from, position)) },
    );
  }, []);

  const { canvas, holding, grab } = useVerticalDrag(onDrag);
  const preview = useMemo(() => crestFrom(draft), [draft]);

  const save = async () => {
    if (!draft.hasText && !draft.hasEmblem) {
      setError('Um escudo precisa de pelo menos uma letra ou uma figura.');
      return;
    }

    setBusy(true);
    setError(null);

    try {
      onSaved(await TeamApi.updateCrest(team.id, preview));
      onClose();
    } catch (err: any) {
      const code = err?.response?.data?.message || err?.message;
      setError(code || 'Não foi possível salvar o escudo.');
    } finally {
      setBusy(false);
    }
  };

  const clear = async () => {
    setBusy(true);
    setError(null);

    try {
      onSaved(await TeamApi.updateCrest(team.id, null));
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Não foi possível remover o escudo.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal" role="dialog" aria-label="Editor de escudo">
      <div className="modal-card crest-editor">
        <h2>Escudo do {team.name}</h2>

        <div className="crest-editor__body">
          <div className="crest-editor__stage">
            <div className="crest-editor__canvas" ref={canvas}>
              <CrestSvg crest={preview} name={team.name} />
              {draft.hasEmblem && (
                <button
                  type="button"
                  className={`crest-editor__handle${holding === 'emblem' ? ' is-held' : ''}`}
                  style={{ top: `${crestTopPercent(draft.emblemPosition)}%` }}
                  onPointerDown={() => grab('emblem')}
                  title="Arraste para escolher a altura da figura"
                >
                  {FIGURE_LABELS[draft.emblem]}
                </button>
              )}
              {draft.hasText && (
                <button
                  type="button"
                  className={`crest-editor__handle${holding === 'text' ? ' is-held' : ''}`}
                  style={{ top: `${crestTopPercent(draft.textPosition)}%` }}
                  onPointerDown={() => grab('text')}
                  title="Arraste para escolher a altura da letra"
                >
                  {draft.textContent.trim().slice(0, 12) || 'letra'}
                </button>
              )}
            </div>
            <p className="crest-editor__hint">
              Arraste uma letra ou figura para escolher a altura em que ela fica no escudo.
            </p>
          </div>

          <div className="crest-editor__controls">
            <div className="crest-editor__colours">
              <ColourField
                label="Cor primária"
                value={draft.primaryColor}
                onChange={colour => set('primaryColor', colour)}
                hint="Borda do escudo e letras."
              />
              <ColourField
                label="Cor secundária"
                value={draft.secondaryColor}
                onChange={colour => set('secondaryColor', colour)}
                hint="Fundo do escudo."
              />
            </div>

            <fieldset className="identity-group">
              <legend>Formato</legend>
              <div className="identity-shapes">
                {CREST_SHAPES.map(shape => (
                  <button
                    key={shape}
                    type="button"
                    className={`identity-shape${draft.shape === shape ? ' is-picked' : ''}`}
                    onClick={() => set('shape', shape)}
                    title={SHAPE_LABELS[shape]}
                  >
                    <CrestSvg
                      crest={{ shape, primaryColor: draft.primaryColor, secondaryColor: draft.secondaryColor }}
                      name={SHAPE_LABELS[shape]}
                      withoutElements
                    />
                    <span>{SHAPE_LABELS[shape]}</span>
                  </button>
                ))}
              </div>
            </fieldset>

            <fieldset className="identity-group">
              <legend>Elementos</legend>

              <label className="identity-check">
                <input
                  type="checkbox"
                  checked={draft.hasText}
                  onChange={event => set('hasText', event.target.checked)}
                />
                <span>Letra</span>
              </label>

              {draft.hasText && (
                <div className="identity-element">
                  <input
                    className="ctrl"
                    value={draft.textContent}
                    maxLength={12}
                    placeholder={team.shortName}
                    onChange={event => set('textContent', event.target.value)}
                  />
                  <ColourField
                    label="Cor da letra"
                    value={draft.textColor}
                    onChange={colour => set('textColor', colour)}
                    hint="Uma cor que não é do clube."
                  />
                </div>
              )}

              <label className="identity-check">
                <input
                  type="checkbox"
                  checked={draft.hasEmblem}
                  onChange={event => set('hasEmblem', event.target.checked)}
                />
                <span>Figura</span>
              </label>

              {draft.hasEmblem && (
                <div className="identity-element">
                  <div className="identity-figures">
                    {CREST_FIGURES.map(figure => (
                      <button
                        key={figure}
                        type="button"
                        className={`identity-figure${draft.emblem === figure ? ' is-picked' : ''}`}
                        onClick={() => set('emblem', figure)}
                        title={FIGURE_LABELS[figure]}
                      >
                        {FIGURE_LABELS[figure]}
                      </button>
                    ))}
                  </div>
                  <ColourField
                    label="Cor da figura"
                    value={draft.emblemColor}
                    onChange={colour => set('emblemColor', colour)}
                    hint="Uma cor que não é do clube."
                  />
                </div>
              )}
            </fieldset>
          </div>
        </div>

        {error && <div className="error">{error}</div>}

        <div className="modal-actions">
          {team.crest && (
            <button className="ctrl" onClick={clear} disabled={busy}>
              Sem escudo
            </button>
          )}
          <button className="ctrl" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button className="ctrl primary" onClick={save} disabled={busy}>
            {busy ? 'Salvando…' : 'Salvar escudo'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default CrestEditor;
