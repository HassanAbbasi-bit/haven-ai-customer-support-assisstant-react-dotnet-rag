import { useCallback, useEffect, useRef, useState } from 'react';
import {
  api,
  bootSession,
  clearToken,
  policyDate,
  json,
  message,
  resetBoot,
} from './api';
import type { Conversation, History, Identity, Message, Source } from './api';
import { Dialog, ErrorNotice, Icon } from './Controls';
import { Login, TicketDraft, TicketList } from './CustomerFlows';
import { Dashboard, Knowledge } from './AdminFlows';
export function SupportApp() {
  const [identity, setIdentity] = useState<Identity | null>(null);
  const [booting, setBooting] = useState(true);
  const [error, setError] = useState('');
  const [view, setView] = useState('chat');
  const [login, setLogin] = useState(false);
  const [ticket, setTicket] = useState(false);
  const [ticketRefresh, setTicketRefresh] = useState(0);
  const [source, setSource] = useState<Source | null>(null);
  const [feedbackTarget, setFeedbackTarget] = useState<Message | null>(null);
  const [feedbackBusy, setFeedbackBusy] = useState(false);
  const [feedbackError, setFeedbackError] = useState('');
  const [history, setHistory] = useState<History[]>([]);
  const [conversation, setConversation] = useState<Conversation | null>(null);
  const [conversationId, setConversationId] = useState<string | null>(() => {
    try {
      return sessionStorage.getItem('haven-conversation');
    } catch {
      return null;
    }
  });
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const [menu, setMenu] = useState(false);
  const [copyNotice, setCopyNotice] = useState('');
  const composer = useRef<HTMLTextAreaElement>(null);
  const menuRef = useRef<HTMLElement>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const wasOpen = useRef(false);
  const openedDrafts = useRef(new Set<string>());
  const selectedId = useRef(conversationId);
  const sendIntent = useRef<{
    conversationId: string;
    text: string;
    key: string;
  } | null>(null);
  const scrollRegion = useRef<HTMLDivElement>(null);
  const nearBottom = useRef(true);
  const [newResponse, setNewResponse] = useState(false);
  const active = conversation?.requests.find(
    (r) => r.state === 'queued' || r.state === 'processing',
  );
  const latestUserMessage =
    conversation?.messages.filter((item) => item.role === 'user').at(-1)
      ?.text ?? '';
  const lastMessage = conversation?.messages.at(-1);
  useEffect(() => {
    if (lastMessage?.role === 'assistant')
      setCopyNotice('Your answer is ready.');
    if (
      lastMessage?.kind === 'human_help' &&
      /\bdraft\b.{0,30}\b(ticket|case)\b/i.test(latestUserMessage) &&
      !/\b(don't|do not|never)\s+draft\b/i.test(latestUserMessage) &&
      !openedDrafts.current.has(lastMessage.id)
    ) {
      openedDrafts.current.add(lastMessage.id);
      setTicket(true);
    }
  }, [
    lastMessage?.id,
    lastMessage?.kind,
    lastMessage?.role,
    latestUserMessage,
  ]);
  const refreshHistory = useCallback(
    async () => setHistory(await api<History[]>('/conversations')),
    [],
  );
  const loadConversation = useCallback(async (id: string) => {
    try {
      const result = await api<Conversation>(`/conversations/${id}`);
      if (selectedId.current === id) setConversation(result);
    } catch (e) {
      if (selectedId.current === id) setError(message(e));
    }
  }, []);
  useEffect(() => {
    let live = true;
    void bootSession()
      .then(async (value) => {
        if (!live) return;
        setIdentity(value);
        await refreshHistory();
      })
      .catch((e) => {
        if (live) setError(message(e));
      })
      .finally(() => {
        if (live) setBooting(false);
      });
    return () => {
      live = false;
    };
  }, [refreshHistory]);
  useEffect(() => {
    selectedId.current = conversationId;
    try {
      if (conversationId)
        sessionStorage.setItem('haven-conversation', conversationId);
      else sessionStorage.removeItem('haven-conversation');
    } catch {
      /* History is kept on the server. */
    }
    if (identity && conversationId) void loadConversation(conversationId);
  }, [identity, conversationId, loadConversation]);
  useEffect(() => {
    if (!active || !conversationId) return;
    let pending = false;
    const timer = setInterval(() => {
      if (!pending && document.visibilityState === 'visible') {
        pending = true;
        void loadConversation(conversationId).finally(() => {
          pending = false;
        });
      }
    }, 1500);
    return () => clearInterval(timer);
  }, [active?.id, conversationId, loadConversation]);
  useEffect(() => {
    const reconnect = () => {
      if (selectedId.current) void loadConversation(selectedId.current);
      void refreshHistory().catch((error) => setError(message(error)));
    };
    window.addEventListener('online', reconnect);
    return () => window.removeEventListener('online', reconnect);
  }, [loadConversation, refreshHistory]);
  useEffect(() => {
    const region = scrollRegion.current;
    if (!region) return;
    if (nearBottom.current) {
      region.scrollTop = region.scrollHeight;
      setNewResponse(false);
    } else setNewResponse(true);
  }, [conversation?.messages.length, active?.id]);
  useEffect(() => {
    if (!menu) {
      if (wasOpen.current) menuButton.current?.focus();
      wasOpen.current = false;
      return;
    }
    wasOpen.current = true;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    menuRef.current?.querySelector<HTMLElement>('button')?.focus();
    const key = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMenu(false);
      if (event.key !== 'Tab') return;
      const items = menuRef.current?.querySelectorAll<HTMLElement>('button,a');
      const first = items?.[0];
      const last = items?.[items.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    };
    document.addEventListener('keydown', key);
    return () => {
      document.body.style.overflow = previous;
      document.removeEventListener('keydown', key);
    };
  }, [menu]);
  function navigate(next: string) {
    setView(next);
    setMenu(false);
    setError('');
  }
  function choose(id: string | null) {
    selectedId.current = id;
    setConversation(null);
    setConversationId(id);
    sendIntent.current = null;
    nearBottom.current = true;
    setNewResponse(false);
    navigate('chat');
  }
  async function send(text = draft) {
    if (!identity || !text.trim() || active || sending) return;
    setSending(true);
    setError('');
    try {
      let id = conversationId;
      if (!id) {
        const created = await api<{ id: string }>('/conversations', {
          method: 'POST',
        });
        id = created.id;
        selectedId.current = id;
        setConversation(null);
        setConversationId(id);
      }
      if (
        sendIntent.current?.conversationId !== id ||
        sendIntent.current.text !== text.trim()
      )
        sendIntent.current = {
          conversationId: id,
          text: text.trim(),
          key: crypto.randomUUID(),
        };
      await api(`/conversations/${id}/requests`, {
        method: 'POST',
        body: json({
          clientRequestId: sendIntent.current.key,
          message: text.trim(),
        }),
      });
      sendIntent.current = null;
      nearBottom.current = true;
      setDraft((current) => (current.trim() === text.trim() ? '' : current));
      await loadConversation(id);
      await refreshHistory();
    } catch (e) {
      setDraft((current) => current || text);
      setError(message(e));
      if (selectedId.current) await loadConversation(selectedId.current);
      void refreshHistory().catch(() => {
        /* The saved request can recover on reconnect. */
      });
    } finally {
      setSending(false);
      composer.current?.focus();
    }
  }
  async function stop() {
    if (!active || !conversationId) return;
    try {
      await api(
        `/conversations/${conversationId}/requests/${active.id}/cancel`,
        { method: 'POST' },
      );
      await loadConversation(conversationId);
    } catch (e) {
      setError(message(e));
    }
  }
  async function feedback(item: Message, helpful: boolean) {
    if (feedbackBusy) return;
    setFeedbackBusy(true);
    try {
      await api(`/messages/${item.id}/feedback`, {
        method: 'PUT',
        body: json({ helpful, comment: null, reason: null }),
      });
      if (conversationId) await loadConversation(conversationId);
      setCopyNotice('Feedback saved.');
    } catch (e) {
      setError(message(e));
    } finally {
      setFeedbackBusy(false);
    }
  }
  async function inspect(item: Source) {
    try {
      setSource(
        await api<Source>(`/messages/${item.messageId}/citations/${item.id}`),
      );
    } catch (e) {
      setError(message(e));
    }
  }
  async function logout() {
    try {
      await api('/auth/logout', { method: 'POST' });
      clearToken();
      resetBoot();
      setIdentity(await bootSession());
      choose(null);
      await refreshHistory();
    } catch (e) {
      setError(message(e));
    }
  }
  const context =
    conversation?.messages
      .slice(-4)
      .map((m) => `${m.role === 'user' ? 'Customer' : 'Assistant'}: ${m.text}`)
      .join('\n\n') ?? '';
  return (
    <div className="app-shell support-app">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      {menu && (
        <button
          className="sidebar-scrim"
          onClick={() => setMenu(false)}
          aria-label="Close navigation"
        />
      )}
      <aside
        ref={menuRef}
        className={`sidebar ${menu ? 'is-open' : ''}`}
        aria-label="Support navigation"
      >
        <button className="brand" onClick={() => navigate('chat')}>
          <span className="brand-mark">
            h<span>·</span>
          </span>
          <span>
            haven<span className="brand-subtitle">SUPPORT</span>
          </span>
        </button>
        <button className="new-chat" onClick={() => choose(null)}>
          <Icon name="plus" />
          New conversation
        </button>
        <nav aria-label="Main navigation">
          <span className="nav-heading">YOUR SUPPORT</span>
          {[
            { id: 'chat', name: 'Ask Haven', icon: 'chat' },
            { id: 'tickets', name: 'My tickets', icon: 'ticket' },
            ...(identity?.role === 'Admin'
              ? [
                  { id: 'knowledge', name: 'Knowledge', icon: 'book' },
                  { id: 'inbox', name: 'Support inbox', icon: 'ticket' },
                  {
                    id: 'dashboard',
                    name: 'Overview & feedback',
                    icon: 'chart',
                  },
                ]
              : []),
          ].map((item) => (
            <button
              key={item.id}
              className={`nav-item ${view === item.id ? 'active' : ''}`}
              aria-current={view === item.id ? 'page' : undefined}
              onClick={() => navigate(item.id)}
            >
              <Icon name={item.icon} />
              {item.name}
            </button>
          ))}
        </nav>
        <div className="history-list">
          <span className="nav-heading">RECENT CONVERSATIONS</span>
          {history.length === 0 && <p>Your conversations will appear here.</p>}
          {history.map((item) => (
            <button
              className={conversationId === item.id ? 'selected-history' : ''}
              key={item.id}
              onClick={() => choose(item.id)}
              title={item.title}
            >
              {item.title}
            </button>
          ))}
        </div>
        <div className="session-card">
          <Icon name="user" />
          <div>
            <strong>{identity?.email ?? 'Guest session'}</strong>
            <small>
              {identity?.role === 'Guest'
                ? 'History available for 24 hours'
                : (identity?.role ?? 'Connecting…')}
            </small>
          </div>
        </div>
        {identity?.role === 'Guest' || !identity ? (
          <button
            className="secondary-button"
            onClick={() => {
              setMenu(false);
              setLogin(true);
            }}
          >
            Sign in
          </button>
        ) : (
          <button className="text-button" onClick={() => void logout()}>
            Sign out
          </button>
        )}
        <p className="sidebar-foot">Local demonstration · Haven Goods</p>
      </aside>
      <div className="main-shell" inert={menu}>
        <header className="topbar">
          <div className="breadcrumb">
            <button
              ref={menuButton}
              className="icon-button mobile-menu"
              aria-expanded={menu}
              aria-label="Open navigation"
              onClick={() => setMenu(!menu)}
            >
              <Icon name="menu" />
            </button>
            <span>Haven Goods</span>
            <span>/</span>
            <strong>
              {view === 'chat'
                ? 'Ask Haven'
                : view === 'knowledge'
                  ? 'Knowledge'
                  : view === 'dashboard'
                    ? 'Overview'
                    : 'Support tickets'}
            </strong>
          </div>
          <button
            className="human-help"
            onClick={() => setTicket(true)}
            disabled={!identity}
          >
            <Icon name="ticket" />
            Get human help
          </button>
        </header>
        <main
          id="main"
          className={view === 'chat' ? 'chat-main' : 'admin-main'}
          tabIndex={-1}
        >
          {booting ? (
            <div className="connecting-state" role="status">
              Connecting to your support session…
            </div>
          ) : view === 'chat' ? (
            <>
              <div
                ref={scrollRegion}
                className="chat-scroll"
                onScroll={(event) => {
                  const region = event.currentTarget;
                  nearBottom.current =
                    region.scrollHeight -
                      region.scrollTop -
                      region.clientHeight <
                    80;
                  if (nearBottom.current) setNewResponse(false);
                }}
              >
                <ErrorNotice error={error} />
                {!identity && error && (
                  <button
                    className="secondary-button"
                    onClick={() => location.reload()}
                  >
                    Reconnect
                  </button>
                )}
                {!conversation?.messages.length ? (
                  <section className="chat-welcome">
                    <span className="assistant-emblem">
                      h<span>·</span>
                    </span>
                    <span className="eyebrow">
                      ANSWERS FROM HAVEN GOODS POLICIES
                    </span>
                    <h1>
                      A little clarity.
                      <br />A lot less hassle.
                    </h1>
                    <p>
                      Ask about returns, delivery, warranties, or product care.
                      <br />
                      We’ll show you the policy passages behind the answer.
                    </p>
                    <div className="prompt-grid">
                      {[
                        'Can I return an opened item?',
                        'What does standard delivery cost?',
                        'What does the warranty cover?',
                        'How should I clean my Trail Bottle?',
                      ].map((q) => (
                        <button
                          key={q}
                          disabled={!identity || sending}
                          onClick={() => void send(q)}
                        >
                          {q}
                          <span>↗</span>
                        </button>
                      ))}
                    </div>
                    <p className="welcome-note">
                      <Icon name="book" />
                      Answers use published policies. Live orders and refunds
                      need human support.
                    </p>
                  </section>
                ) : (
                  <div className="conversation-thread">
                    <div className="thread-heading">
                      <span className="eyebrow">YOUR CONVERSATION</span>
                      <button
                        className="text-button danger"
                        onClick={() => {
                          if (
                            conversationId &&
                            confirm(
                              'Delete this conversation? It will immediately disappear from your history.',
                            )
                          )
                            void api(`/conversations/${conversationId}`, {
                              method: 'DELETE',
                            })
                              .then(async () => {
                                choose(null);
                                await refreshHistory();
                              })
                              .catch((e) => setError(message(e)));
                        }}
                      >
                        Delete
                      </button>
                    </div>
                    {conversation.messages.map((item) => (
                      <article
                        className={`chat-message ${item.role}`}
                        key={item.id}
                      >
                        <span className="message-label">
                          {item.role === 'user' ? 'You' : 'Haven assistant'}
                        </span>
                        <p className="message-text">{item.text}</p>
                        {item.role === 'assistant' && (
                          <>
                            <div className="citation-chips">
                              {item.citations.map((c) => (
                                <button
                                  key={c.id}
                                  onClick={() => void inspect(c)}
                                >
                                  <Icon name="book" />
                                  {c.documentTitle}
                                  {c.page
                                    ? ` · p. ${c.page}`
                                    : ` · ${c.sourceKey}`}
                                  {!c.available && ' · withdrawn'}
                                </button>
                              ))}
                            </div>
                            <div className="message-actions">
                              <button
                                aria-pressed={item.feedback?.helpful === true}
                                disabled={feedbackBusy}
                                onClick={() => void feedback(item, true)}
                              >
                                Helpful
                              </button>
                              <button
                                aria-pressed={item.feedback?.helpful === false}
                                disabled={feedbackBusy}
                                onClick={() => {
                                  setFeedbackError('');
                                  setFeedbackTarget(item);
                                }}
                              >
                                Not helpful
                              </button>
                              <button
                                onClick={() => {
                                  void navigator.clipboard
                                    .writeText(item.text)
                                    .then(() => setCopyNotice('Answer copied.'))
                                    .catch(() =>
                                      setCopyNotice(
                                        'Copy unavailable. Select the answer text to copy it.',
                                      ),
                                    );
                                }}
                              >
                                Copy
                              </button>
                              <button onClick={() => setTicket(true)}>
                                Get human help
                              </button>
                            </div>
                          </>
                        )}
                      </article>
                    ))}
                    {active && (
                      <div className="answer-progress" role="status">
                        <span className="loading-dot" />
                        {active.stage}
                        <small>
                          Local AI can take a few minutes. You can stop or
                          return to this conversation later.
                        </small>
                      </div>
                    )}
                    {conversation.requests
                      .filter((r) => ['failed', 'canceled'].includes(r.state))
                      .map((r) => (
                        <div className="request-error" key={r.id}>
                          <strong>
                            {r.state === 'canceled'
                              ? 'Answer stopped.'
                              : 'We couldn’t finish this answer.'}
                          </strong>
                          <p>
                            {r.errorCode
                              ? r.errorCode.replaceAll('_', ' ')
                              : 'Your question is still saved.'}
                          </p>
                          <button
                            className="secondary-button"
                            disabled={!!active || sending}
                            onClick={() => void send(r.question)}
                          >
                            Try this question again
                          </button>
                        </div>
                      ))}
                  </div>
                )}
              </div>
              <form
                className="composer-wrap"
                onSubmit={(event) => {
                  event.preventDefault();
                  void send();
                }}
              >
                {newResponse && (
                  <button
                    type="button"
                    className="new-response-button"
                    onClick={() => {
                      if (scrollRegion.current)
                        scrollRegion.current.scrollTop =
                          scrollRegion.current.scrollHeight;
                      nearBottom.current = true;
                      setNewResponse(false);
                    }}
                  >
                    New response ↓
                  </button>
                )}
                <div className="composer">
                  <label className="sr-only" htmlFor="question">
                    Ask Haven a question
                  </label>
                  <textarea
                    ref={composer}
                    id="question"
                    placeholder="What can we help you with?"
                    rows={2}
                    maxLength={4000}
                    value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                    onKeyDown={(event) => {
                      if (
                        event.key === 'Enter' &&
                        !event.shiftKey &&
                        !event.nativeEvent.isComposing
                      ) {
                        event.preventDefault();
                        void send();
                      }
                    }}
                  />
                  {active ? (
                    <button
                      type="button"
                      className="stop-button"
                      onClick={() => void stop()}
                    >
                      Stop
                    </button>
                  ) : (
                    <button
                      className="send-button"
                      aria-label="Send question"
                      disabled={!draft.trim() || sending || !identity}
                    >
                      <Icon name="arrow" />
                    </button>
                  )}
                </div>
                <div className="composer-note">
                  <span>
                    AI can make mistakes. Check sources before acting.
                  </span>
                  <span>
                    {draft.length}/4,000 · Shift + Enter for a new line
                  </span>
                </div>
              </form>
            </>
          ) : identity?.role === 'Admin' && view === 'knowledge' ? (
            <Knowledge />
          ) : identity?.role === 'Admin' && view === 'dashboard' ? (
            <Dashboard />
          ) : (
            <TicketList
              admin={identity?.role === 'Admin' && view === 'inbox'}
              refreshKey={ticketRefresh}
            />
          )}
        </main>
      </div>
      <div className="sr-only" role="status">
        {copyNotice}
      </div>
      {login && (
        <Login
          onClose={() => setLogin(false)}
          onLogin={(value) => {
            setIdentity(value);
            setLogin(false);
            choose(null);
            void refreshHistory();
          }}
        />
      )}
      {ticket && (
        <TicketDraft
          context={context}
          suggestedSubject={
            latestUserMessage.split(/[.!?]/)[0]?.slice(0, 160) ?? ''
          }
          suggestedSummary={latestUserMessage.slice(0, 4000)}
          onClose={() => setTicket(false)}
          onSaved={() => setTicketRefresh((n) => n + 1)}
        />
      )}
      {feedbackTarget && (
        <Dialog
          title="Help us make this clearer."
          onClose={() => {
            if (!feedbackBusy) setFeedbackTarget(null);
          }}
        >
          <p className="flow-muted">
            Your vote is saved once per answer. A reason or comment is optional.
          </p>
          <form
            className="flow-form"
            onSubmit={(event) => {
              event.preventDefault();
              if (feedbackBusy) return;
              const form = new FormData(event.currentTarget);
              setFeedbackBusy(true);
              setFeedbackError('');
              void api(`/messages/${feedbackTarget.id}/feedback`, {
                method: 'PUT',
                body: json({
                  helpful: false,
                  reason: form.get('reason') || null,
                  comment: form.get('comment') || null,
                }),
              })
                .then(async () => {
                  if (conversationId) await loadConversation(conversationId);
                  setFeedbackTarget(null);
                  setCopyNotice('Feedback saved.');
                })
                .catch((error) => setFeedbackError(message(error)))
                .finally(() => setFeedbackBusy(false));
            }}
          >
            <label>
              What could be better? <small>(optional)</small>
              <select
                name="reason"
                defaultValue={feedbackTarget.feedback?.reason ?? ''}
              >
                <option value="">Choose a reason</option>
                <option value="unclear">Hard to understand</option>
                <option value="incorrect">The answer seems incorrect</option>
                <option value="missing_information">Missing information</option>
                <option value="source_problem">
                  Source doesn’t support the answer
                </option>
                <option value="other">Something else</option>
              </select>
            </label>
            <label>
              Comment <small>(optional)</small>
              <textarea
                name="comment"
                rows={3}
                maxLength={1000}
                defaultValue={feedbackTarget.feedback?.comment ?? ''}
              />
            </label>
            <ErrorNotice error={feedbackError} />
            <button className="primary-button" disabled={feedbackBusy}>
              {feedbackBusy ? 'Saving…' : 'Save feedback'}
            </button>
          </form>
        </Dialog>
      )}
      {source && (
        <Dialog title={source.documentTitle} onClose={() => setSource(null)}>
          <p className="source-meta">
            Version {source.version} · Effective{' '}
            {policyDate(source.effectiveDate)} ·{' '}
            {source.page
              ? `Page ${source.page}`
              : `Lines ${source.lineStart}–${source.lineEnd}`}
          </p>
          <span className="passage-id">{source.sourceKey}</span>
          <blockquote className="source-quote">{source.quote}</blockquote>
          <h3>Full stored passage</h3>
          <p className="preserve-text">{source.text}</p>
        </Dialog>
      )}
    </div>
  );
}
