import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import type { InboxBoxDto, InboxMessageDto } from '@/types';
import { InboxApi } from '@/api';
import { useGameState } from '@/state';
import NarrativeText, { buildNameIndex } from '@/components/Match/NarrativeText';
import type { NameIndex } from '@/components/Match/NarrativeText';

/**
 * How many messages a page of the box holds.
 *
 * The box is a column and a reading pane, and a column that shows twenty lines is a column a
 * manager can find something in: past that it stops being a list of subjects and becomes a
 * wall, and a wall is a thing a manager gives up on.
 */
const PAGE_SIZE = 20;

/**
 * How often a box that is already open asks the server whether anything has arrived.
 *
 * The same thirty seconds the navbar's badge polls on, and for the same reason: the box is
 * written from the match loop, the season close and the market, so a manager who is *reading*
 * the box while a match finishes is exactly the manager a one-shot read leaves behind. It is
 * polled rather than pushed because the badge is polled rather than pushed, and a second live
 * channel kept alive for one screen is a channel nobody maintains.
 */
const POLL_MS = 30_000;

/**
 * The mark a category is read by, and the words beside it.
 *
 * The words matter as much as the mark. A box of forty lines is told apart at a glance by the
 * subject, and "Ninja Eleven em alta" is a subject a manager can act on; an emoji on its own
 * is a symbol he has to decode before he can read the line.
 */
const CATEGORIES: Record<string, { label: string; icon: string }> = {
  Finance: { label: 'Financeiro', icon: '💰' },
  MatchReport: { label: 'Partida', icon: '⚽' },
  TransferOffer: { label: 'Mercado', icon: '🔄' },
  Title: { label: 'Título', icon: '🏆' },
  // The two the whole country is told, and they are told apart from a title because neither of
  // them is one: a round of the cup decides who is still in it, and a season's end moves four
  // tables at once. Filed under "Título" an elimination would sit next to a championship.
  CupRound: { label: 'Copa', icon: '🥇' },
  SeasonSummary: { label: 'Temporada', icon: '📋' },
  // Rodada is NOT a Title because a matchday decides nothing and awards nothing: a season's
  // end moves four tables and settles who the champions are; this is the other thing — a
  // Wednesday in October, thirty-two results, and a table that shuffles underneath it. Filed
  // under a title, every manager would have to open the same mark to find out which of its
  // lines is the end of the year and which is last Tuesday. It is also NOT MatchReport because
  // that is one match and this is thirty-two.
  RoundSummary: { label: 'Rodada', icon: '📰' },
  Club: { label: 'Clube', icon: '📣' }
};

/**
 * A message, split into its paragraphs.
 *
 * The blank line is the only structure a body has, and the screen splits on it rather than
 * inventing paragraphs of its own: a stored document that a client re-laid-out differently
 * each time it was opened would be a document that reads as a different message every time.
 */
const paragraphsOf = (body: string): string[] =>
  body
    .split(/\n\s*\n/)
    .map(paragraph => paragraph.trim())
    .filter(paragraph => paragraph.length > 0);

/**
 * The lookup of the names a message uses, so the names in it become doors.
 *
 * It is built from the message's own mentions rather than from the world, and it is built
 * through the same `buildNameIndex` the match feed uses — which means a name two things
 * answer to is left as plain text, in the box exactly as on the scoreboard. A message that
 * linked the wrong player half the time would be worse than a message that links none.
 */
const indexOf = (message: InboxMessageDto | null): NameIndex | null => {
  if (!message) return null;

  const teams = message.mentions
    .filter(person => person.kind === 'team')
    .map(person => ({ id: person.id, name: person.name }));
  const players = message.mentions
    .filter(person => person.kind === 'player')
    .map(person => ({ id: person.id, name: person.name }));

  return buildNameIndex(teams, players);
};

/**
 * When a message arrived, said the way a reader says it.
 *
 * A box is read by what is newest at the top of it, and the eye reaches for how long ago a
 * thing happened before it reads what the thing is. The day is the season's day rather than a
 * calendar date for the same reason the calendar counts days: a message has no date on it, it
 * arrived when the engine said it, and inventing a clock for it would be a number the game
 * does not have.
 */
const arrivedAt = (message: InboxMessageDto): string => {
  const when = new Date(message.createdAt);
  if (Number.isNaN(when.getTime())) return '';

  const now = new Date();
  const sameDay =
    when.getFullYear() === now.getFullYear() &&
    when.getMonth() === now.getMonth() &&
    when.getDate() === now.getDate();

  return sameDay
    ? when.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    : when.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: '2-digit' });
};

/**
 * The manager's box: a column of what has arrived and the message being read beside it.
 *
 * It reads the way a mailbox reads. The column is the list of subjects, newest first, with
 * the unread ones marked; the pane is the message itself, with the names in it opening the
 * profile they belong to, and a single door out to the place the message is about.
 *
 * **Nothing is written from here.** There is no compose, because the messages are written by
 * the engine — the whistle, the ledger, the market — and a manager who could type into his own
 * box would be writing into the place the game's facts live. The only thing this screen does
 * to a message is open it, which is what makes the mark on the column true rather than a
 * guess about what has been seen.
 */
const InboxScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const [box, setBox] = useState<InboxBoxDto | null>(null);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);
  // Which kind of message the column is showing, or null for all of them. It is a question
  // about the box and not about the twenty lines on the screen, so it is asked of the
  // backend: the filter, the count, the pages and the buttons are all one answer.
  const [category, setCategory] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  /**
   * How many "mark as read" requests are on the wire.
   *
   * A poll that lands while one of these is in flight would put the unread mark back on a
   * line the manager has just opened, and the badge above the column would count a message he
   * is reading right now. The count is a ref rather than state because it is read by the poll
   * and written by the click, and a re-render in between must not be what decides it.
   */
  const readsInFlight = useRef(0);

  useEffect(() => {
    if (!selectedTeam) {
      setBox(null);
      setLoading(false);
      return;
    }

    let alive = true;
    let asking = false;
    setLoading(true);

    const load = (first: boolean) => {
      if (asking) return;
      asking = true;

      InboxApi.getBox(selectedTeam.id, page, PAGE_SIZE, category)
        .then(answer => {
          if (!alive) return;
          // A read that has not been answered yet wins over a poll: the server's answer to
          // "open this line" is newer than the page it was read from.
          if (!first && readsInFlight.current > 0) return;
          setBox(answer);
          setError(null);
          // The message open is whichever of the page's lines is asked for, and it is opened
          // again when the page changes: landing on page three with page two's message still
          // open is a reading pane showing a line that is not on the screen.
          setOpenId(current => (current && answer.messages.some(m => m.id === current) ? current : null));
        })
        .catch(() => {
          // A poll that fails leaves the lines already on the screen alone and says nothing:
          // a box that emptied itself because one request failed is a box that lost mail.
          if (alive && first) setError('Não foi possível carregar a caixa de entrada do clube.');
        })
        .finally(() => {
          asking = false;
          if (alive && first) setLoading(false);
        });
    };

    load(true);

    const timer = window.setInterval(() => load(false), POLL_MS);

    return () => {
      alive = false;
      window.clearInterval(timer);
    };
  }, [selectedTeam?.id, page, category]);

  const open = useMemo(
    () => box?.messages.find(message => message.id === openId) ?? null,
    [box, openId]
  );

  const nameIndex = useMemo(() => indexOf(open), [open]);

  /**
   * Opening a message is the only thing this screen does to one, and the mark on the column
   * is moved here rather than optimistically: the backend is what knows whether the line
   * belongs to this club, and a badge that cleared itself before the server had agreed would
   * be a badge that could be wrong.
   */
  const read = (message: InboxMessageDto) => {
    setOpenId(message.id);

    if (message.isRead || !selectedTeam) return;

    readsInFlight.current += 1;

    InboxApi.markRead(selectedTeam.id, message.id)
      .then(marked => {
        setBox(current => current
          ? {
              ...current,
              unreadCount: Math.max(0, current.unreadCount - 1),
              messages: current.messages.map(line => (line.id === marked.id ? marked : line))
            }
          : current);
      })
      // The badge stays until the box is read again: better late than wrong.
      .catch(() => { /* nothing to undo here: the mark was never moved */ })
      .finally(() => { readsInFlight.current = Math.max(0, readsInFlight.current - 1); });
  };

  const catchUp = () => {
    if (!selectedTeam || !box) return;

    readsInFlight.current += 1;

    InboxApi.markAllRead(selectedTeam.id)
      .then(() => {
        setBox(current => current
          ? {
              ...current,
              unreadCount: 0,
              messages: current.messages.map(line => ({ ...line, isRead: true }))
            }
          : current);
      })
      // The same: a badge that clears itself and was not cleared is a lie.
      .catch(() => { /* nothing to undo here: the mark was never moved */ })
      .finally(() => { readsInFlight.current = Math.max(0, readsInFlight.current - 1); });
  };

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header">
          <div className="club-modal__content">
            <h2 className="profile-name">Caixa de Entrada</h2>
            <p className="competition">Escolha um clube para ver a correspondência dele.</p>
          </div>
        </div>
      </div>
    );
  }

  /**
   * The mark a category is read by. It is the same table the row's own mark is drawn from, so
   * a filter and the line it filters are labelled in the same words — a button called
   * "Financeiro" above a column whose lines are filed under "Partida" is a column nobody can
   * navigate.
   */
  const markOf = (kind: string) => CATEGORIES[kind] ?? { label: 'Notícia', icon: '📰' };

  /**
   * The filters, out of the tally the backend sent with the page.
   *
   * Only the kinds this club actually has lines of are offered, and they come in the order the
   * backend sent them rather than by how many of each there are: a column that reorders itself
   * after every matchday is a column a manager has to find again every time the whistle goes.
   */
  const filters = (box?.categories ?? []).map(tally => ({
    kind: tally.category,
    count: tally.count,
    mark: markOf(tally.category)
  }));

  /**
   * How much mail there is in all of it, summed out of the backend's own per-kind counts.
   *
   * It is the sum of the tally rather than `totalItems`, because `totalItems` is the count of
   * the page being read — the number *under the filter* — and a filter labelled "Todas" with
   * the filtered count beside it is a button that lies about what removing it would show.
   */
  const everything = filters.reduce((total, tally) => total + tally.count, 0);

  /**
   * Choosing what to read. It returns to the first page, because page four of a category the
   * manager has just chosen is not where he is: a filter pressed over a paged box that kept
   * the page is a filter that can land on an empty page and look like an empty box.
   */
  const filter = (kind: string | null) => {
    setCategory(kind);
    setPage(1);
  };

  return (
    <div className="app">
      {/* The card is the box and it takes the width it is given. It used to borrow the club
          card's `club-modal`, which caps itself at 1100px for a club's own page — a mail list
          with a filter beside it and a page beside that is three columns, and three columns
          that stop at 1100px leave the right-hand side of a wide desktop empty. */}
      <div className="card match-header">
        <div className="inbox-head">
          <div>
            <h2 className="profile-name">Caixa de Entrada</h2>
            <p className="competition">
              {selectedTeam.name}
              {box && box.unreadCount > 0 && (
                <span className="inbox-head__unread">
                  {box.unreadCount} {box.unreadCount === 1 ? 'mensagem nova' : 'mensagens novas'}
                </span>
              )}
            </p>
          </div>

          {/* A manager who has caught up says so once, rather than clicking twenty lines to
              clear a badge he has already dealt with. */}
          {box && box.unreadCount > 0 && (
            <button type="button" className="ctrl" onClick={catchUp}>
              Marcar tudo como lido
            </button>
          )}
        </div>

        <div className="inbox">
          {/* The filters, down the left. Which one is lit is read off the page the backend
              sent rather than off the button's own state, so a filter that was refused or
              dropped cannot leave a column that says it is filtered and is not. */}
          <nav className="inbox-filters" aria-label="Filtrar mensagens por categoria">
            <button
              type="button"
              className={`inbox-filter${!category ? ' inbox-filter--on' : ''}`}
              onClick={() => filter(null)}
            >
              <span className="inbox-filter__label">
                <span aria-hidden="true">📥</span> Todas
              </span>
              <span className="inbox-filter__count">{everything}</span>
            </button>

            {filters.map(({ kind, count, mark }) => (
              <button
                type="button"
                key={kind}
                className={`inbox-filter${category === kind ? ' inbox-filter--on' : ''}`}
                onClick={() => filter(kind)}
              >
                <span className="inbox-filter__label">
                  <span aria-hidden="true">{mark.icon}</span> {mark.label}
                </span>
                <span className="inbox-filter__count">{count}</span>
              </button>
            ))}
          </nav>

          {/* The column: the subjects, newest first. The row is the message's own fact about
              whether it has been opened, and it is drawn from `isRead` rather than from
              anything the screen remembers. */}
          <div className="inbox__list">
            {loading && !box ? (
              <p className="league-empty">Abrindo a caixa de entrada…</p>
            ) : error ? (
              <p className="league-empty">{error}</p>
            ) : !box || box.messages.length === 0 ? (
              /* The two are not the same sentence: a box with nothing in it is a game that has
                 not written yet, and a filtered box with nothing on this page is a manager who
                 has reached the end of what he asked for. */
              category ? (
                <p className="league-empty">
                  Nenhuma mensagem de {markOf(category).label.toLowerCase()} nesta página.
                </p>
              ) : (
                <p className="league-empty">
                  Nenhuma mensagem por enquanto. Assim que o jogo tiver algo a dizer, ele aparece aqui.
                </p>
              )
            ) : (
              box.messages.map(message => {
                const mark = markOf(message.category);
                const isOpen = message.id === openId;

                return (
                  <button
                    type="button"
                    key={message.id}
                    className={`inbox-row${message.isRead ? '' : ' inbox-row--unread'}${
                      isOpen ? ' inbox-row--active' : ''
                    }`}
                    onClick={() => read(message)}
                  >
                    <span className="inbox-row__top">
                      <span className="inbox-row__sender">{message.senderName}</span>
                      <span className="inbox-row__date">{arrivedAt(message)}</span>
                    </span>
                    <span className="inbox-row__subject">{message.subject}</span>
                    <span className="inbox-row__mark">
                      {mark.icon} {mark.label}
                    </span>
                  </button>
                );
              })
            )}

            {/* A box of a whole season is thousands of lines long — a wage bill every matchday
                and a gate receipt on top of it — so it is paged rather than read whole. */}
            {box && box.totalPages > 1 && (
              <div className="inbox__pages">
                <button
                  type="button"
                  className="ctrl"
                  disabled={box.page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  Anterior
                </button>
                <span className="inbox__page-label">
                  {box.page} de {box.totalPages}
                </span>
                <button
                  type="button"
                  className="ctrl"
                  disabled={box.page >= box.totalPages}
                  onClick={() => setPage(page + 1)}
                >
                  Próxima
                </button>
              </div>
            )}
          </div>

          {/* The pane: the message, in the words the engine wrote it. */}
          <div className="inbox__reader">
            {!open ? (
              <p className="league-empty">Escolha uma mensagem para ler.</p>
            ) : (
              <article className="inbox-message">
                <header className="inbox-message__head">
                  <h3 className="inbox-message__subject">{open.subject}</h3>
                  <p className="inbox-message__meta">
                    <span>{open.senderName}</span>
                    <span aria-hidden="true">·</span>
                    <span>{arrivedAt(open)}</span>
                  </p>
                </header>

                {paragraphsOf(open.body).map((paragraph, position) => (
                  <p className="inbox-message__paragraph" key={position}>
                    <NarrativeText text={paragraph} index={nameIndex} />
                  </p>
                ))}

                {/* The one door out of the message. It is a route the game has, so a message
                    can never send a manager to a page that is not there. */}
                {open.linkRoute && open.linkLabel && (
                  <Link className="ctrl inbox-message__link" to={open.linkRoute}>
                    {open.linkLabel} →
                  </Link>
                )}
              </article>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default InboxScreen;
