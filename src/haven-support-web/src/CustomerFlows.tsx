import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, date, json, message, setToken } from './api';
import type { Identity, Ticket } from './api';
import { Dialog, ErrorNotice, Icon } from './Controls';
export function Login({
  onClose,
  onLogin,
}: {
  onClose: () => void;
  onLogin: (identity: Identity) => void;
}) {
  const [register, setRegister] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const form = new FormData(event.currentTarget);
    const credentials = {
      email: String(form.get('email')),
      password: String(form.get('password')),
    };
    try {
      if (register) {
        await api('/auth/register', {
          method: 'POST',
          body: json(credentials),
        });
        setRegister(false);
        setNotice('Account created. Sign in to continue.');
      } else {
        const result = await api<{ accessToken: string }>('/auth/login', {
          method: 'POST',
          body: json(credentials),
        });
        setToken(result.accessToken);
        onLogin(await api<Identity>('/auth/me'));
      }
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Dialog
      title={register ? 'Make yourself at home.' : 'Welcome back.'}
      onClose={onClose}
    >
      <p className="flow-muted">
        {register
          ? 'Keep your support history with a customer account.'
          : 'Sign in to your customer or administrator account.'}{' '}
        Guest conversations stay separate when you sign in.
      </p>
      <form onSubmit={(event) => void submit(event)} className="flow-form">
        <label>
          Email
          <input
            name="email"
            type="email"
            required
            maxLength={254}
            autoComplete="username"
          />
        </label>
        <label>
          Password
          <input
            name="password"
            type="password"
            required
            minLength={12}
            maxLength={128}
            autoComplete={register ? 'new-password' : 'current-password'}
          />
        </label>
        {register && (
          <small>
            At least 12 characters, with uppercase, lowercase, a number and a
            symbol.
          </small>
        )}
        <ErrorNotice error={error} />
        {notice && <p role="status">{notice}</p>}
        <button className="primary-button" disabled={busy}>
          {busy ? 'Please wait…' : register ? 'Create account' : 'Sign in'}
        </button>
        <button
          type="button"
          className="text-button"
          disabled={busy}
          onClick={() => {
            setRegister(!register);
            setError('');
            setNotice('');
          }}
        >
          {register
            ? 'Already have an account? Sign in'
            : 'New here? Create a customer account'}
        </button>
      </form>
      <p className="flow-note">
        Local demo accounts. Email verification and password recovery are not
        available.
      </p>
    </Dialog>
  );
}
export function TicketDraft({
  context,
  suggestedSubject,
  suggestedSummary,
  onClose,
  onSaved,
}: {
  context: string;
  suggestedSubject: string;
  suggestedSummary: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [key] = useState(() => crypto.randomUUID());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState<Ticket | null>(null);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const form = new FormData(event.currentTarget);
    try {
      setSaved(
        await api<Ticket>('/tickets', {
          method: 'POST',
          body: json({
            clientRequestId: key,
            subject: form.get('subject'),
            summary: form.get('summary'),
            email: form.get('email'),
            orderReference: form.get('orderReference') || null,
            sharedContext: form.get('sharedContext'),
            consent: form.get('consent') === 'on',
          }),
        }),
      );
      onSaved();
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Dialog
      title={saved ? 'Your ticket is saved.' : 'A hand from a human.'}
      onClose={() => {
        if (!busy) onClose();
      }}
    >
      {saved ? (
        <div className="ticket-success" role="status">
          <span className="success-mark">
            <Icon name="check" />
          </span>
          <p>
            Reference <strong>{saved.reference}</strong>
          </p>
          <p>Status: Open</p>
          <p className="flow-muted">
            A local support case was created. No email has been sent. Find it in
            My tickets.
          </p>
          <button className="primary-button" autoFocus onClick={onClose}>
            Done
          </button>
        </div>
      ) : (
        <>
          <p className="flow-muted">
            Review what you’re sharing. This creates a local support ticket; it
            does not send email. Don’t include passwords or payment details.
          </p>
          <form className="flow-form" onSubmit={(event) => void submit(event)}>
            <label>
              Subject
              <input
                name="subject"
                defaultValue={suggestedSubject}
                required
                maxLength={160}
                placeholder="What do you need help with?"
              />
            </label>
            <label>
              Issue summary
              <textarea
                name="summary"
                required
                maxLength={4000}
                rows={4}
                defaultValue={suggestedSummary}
              />
            </label>
            <div className="form-columns">
              <label>
                Contact email
                <input name="email" type="email" required maxLength={254} />
              </label>
              <label>
                Order reference <small>(optional)</small>
                <input name="orderReference" maxLength={100} />
              </label>
            </div>
            <details>
              <summary>Review conversation excerpts to share</summary>
              <label>
                Shared excerpts
                <textarea
                  name="sharedContext"
                  rows={5}
                  maxLength={4000}
                  defaultValue={context.slice(-4000)}
                />
              </label>
            </details>
            <label className="checkbox-label">
              <input name="consent" type="checkbox" required />I have reviewed
              these details and consent to sharing them with support.
            </label>
            <ErrorNotice error={error} />
            <button className="primary-button" disabled={busy}>
              {busy ? 'Saving ticket…' : 'Submit ticket'}
            </button>
          </form>
        </>
      )}
    </Dialog>
  );
}
export function TicketList({
  admin,
  refreshKey,
}: {
  admin: boolean;
  refreshKey: number;
}) {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('all');
  const [search, setSearch] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(async () => {
    setError('');
    try {
      setTickets(await api<Ticket[]>(admin ? '/admin/tickets' : '/tickets'));
    } catch (error) {
      setError(message(error));
    } finally {
      setLoading(false);
    }
  }, [admin]);
  useEffect(() => {
    void load();
  }, [load, refreshKey, attempt]);
  async function status(ticket: Ticket, value: string) {
    setBusy(ticket.id);
    try {
      await api(`/admin/tickets/${ticket.id}`, {
        method: 'PATCH',
        headers: { 'If-Match': `"${ticket.rowVersion}"` },
        body: json({ status: value }),
      });
      await load();
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(null);
    }
  }
  const visible = tickets.filter(
    (t) =>
      (filter === 'all' || t.status === filter) &&
      `${t.reference} ${t.subject}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  return (
    <section className="workspace-page">
      <span className="eyebrow">HUMAN SUPPORT</span>
      <h1>{admin ? 'Support inbox' : 'My tickets'}</h1>
      <p className="flow-muted">
        {admin
          ? 'Review customer-approved details and keep cases moving.'
          : 'Your saved local support cases. Creating a ticket does not send an email.'}
      </p>
      <div className="table-toolbar">
        <input
          aria-label="Search tickets"
          placeholder="Search subject or reference"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          aria-label="Filter ticket status"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        >
          <option value="all">All statuses</option>
          {['open', 'in_progress', 'resolved', 'closed'].map((s) => (
            <option key={s} value={s}>
              {s.replace('_', ' ')}
            </option>
          ))}
        </select>
        <button
          className="secondary-button"
          onClick={() => setAttempt((n) => n + 1)}
        >
          Refresh
        </button>
      </div>
      <ErrorNotice error={error} />
      {loading ? (
        <p role="status">Loading tickets…</p>
      ) : visible.length === 0 ? (
        <div className="empty-state">
          <Icon name="ticket" />
          <h2>
            {tickets.length
              ? 'No matching cases.'
              : 'Nothing to follow up on yet.'}
          </h2>
          <p>Submitted tickets will appear here.</p>
        </div>
      ) : (
        <div className="ticket-list">
          {visible.map((ticket) => (
            <details key={ticket.id} className="ticket-row">
              <summary>
                <span>
                  <small>
                    {ticket.reference} · {date(ticket.createdAt)}
                  </small>
                  <strong>{ticket.subject}</strong>
                </span>
                <span className="status-badge">
                  {ticket.status.replace('_', ' ')}
                </span>
              </summary>
              <p className="preserve-text">{ticket.summary}</p>
              <dl>
                <dt>Contact</dt>
                <dd>{ticket.email}</dd>
                <dt>Order reference</dt>
                <dd>{ticket.orderReference || 'Not provided'}</dd>
              </dl>
              {ticket.sharedContext && (
                <details>
                  <summary>Customer-approved excerpts</summary>
                  <p className="preserve-text">{ticket.sharedContext}</p>
                </details>
              )}
              {admin && (
                <label className="status-select">
                  Update status
                  <select
                    value={ticket.status}
                    disabled={busy === ticket.id}
                    onChange={(e) => void status(ticket, e.target.value)}
                  >
                    {['open', 'in_progress', 'resolved', 'closed'].map((s) => (
                      <option key={s} value={s}>
                        {s.replace('_', ' ')}
                      </option>
                    ))}
                  </select>
                </label>
              )}
            </details>
          ))}
        </div>
      )}
    </section>
  );
}
