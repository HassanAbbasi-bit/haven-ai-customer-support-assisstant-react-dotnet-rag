import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, date, policyDate, message } from './api';
import type { Document, Review, Version } from './api';
import { Dialog, ErrorNotice, Icon } from './Controls';
type PreviewPassage = {
  id: string;
  sourceKey: string | null;
  documentTitle: string;
  page: number | null;
  text: string;
};
function RetrievalPreview() {
  const [question, setQuestion] = useState('');
  const [passages, setPassages] = useState<PreviewPassage[] | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setPassages(null);
    try {
      const result = await api<{ ids: string[]; passages: PreviewPassage[] }>(
        '/admin/retrieval-preview',
        {
          method: 'POST',
          body: JSON.stringify({
            message: question.trim(),
            clientRequestId: crypto.randomUUID(),
          }),
        },
      );
      setPassages(
        result.ids.flatMap((id) => result.passages.filter((p) => p.id === id)),
      );
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <details className="knowledge-card retrieval-preview">
      <summary>Test published knowledge</summary>
      <p className="flow-muted">
        Check which published passages a customer question finds before trying a
        chat answer.
      </p>
      <form className="flow-form" onSubmit={(event) => void search(event)}>
        <label htmlFor="knowledge-test-question">Customer question</label>
        <textarea
          id="knowledge-test-question"
          required
          maxLength={4000}
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Can I return an opened item?"
        />
        <button
          className="secondary-button"
          disabled={busy || !question.trim()}
        >
          {busy ? 'Searching published policies…' : 'Find passages'}
        </button>
      </form>
      <ErrorNotice error={error} />
      {passages !== null && (
        <p role="status">
          {passages.length
            ? `${passages.length} relevant passages found.`
            : 'No relevant published passages found. Check the question and publication status.'}
        </p>
      )}
      {passages?.map((passage) => (
        <article className="preview-passage" key={passage.id}>
          <h3>
            {passage.documentTitle}
            {passage.page ? ` · Page ${passage.page}` : ''}
          </h3>
          {passage.sourceKey && <small>{passage.sourceKey}</small>}
          <p>{passage.text}</p>
        </article>
      ))}
    </details>
  );
}
export function Knowledge() {
  const [documents, setDocuments] = useState<Document[]>([]);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [review, setReview] = useState<Review | null>(null);
  const [upload, setUpload] = useState(false);
  const [replacement, setReplacement] = useState<Document | null>(null);
  const [busy, setBusy] = useState(false);
  const [filter, setFilter] = useState('');
  const [loaded, setLoaded] = useState(false);
  const load = useCallback(async () => {
    try {
      setDocuments(await api<Document[]>('/admin/documents'));
      setLoaded(true);
    } catch (error) {
      setError(message(error));
    }
  }, []);
  useEffect(() => {
    void load();
    const timer = setInterval(() => void load(), 4000);
    return () => clearInterval(timer);
  }, [load]);
  async function inspect(document: Document, version: Version) {
    try {
      setReview(
        await api<Review>(
          `/admin/documents/${document.id}/versions/${version.id}`,
        ),
      );
    } catch (error) {
      setError(message(error));
    }
  }
  async function action(path: string, etag?: string, method = 'POST') {
    setBusy(true);
    setError('');
    try {
      await api(path, {
        method,
        ...(etag ? { headers: { 'If-Match': `"${etag}"` } } : {}),
      });
      setReview(null);
      setNotice('Knowledge updated.');
      await load();
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(false);
    }
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const body = new FormData(event.currentTarget);
    if (replacement) body.set('documentId', replacement.id);
    const file = body.get('file');
    if (file instanceof File && file.size > 20 * 1024 * 1024) {
      setError('Choose a file smaller than 20 MiB.');
      setBusy(false);
      return;
    }
    try {
      await api('/admin/documents', { method: 'POST', body });
      setUpload(false);
      setReplacement(null);
      setNotice(
        'Upload saved. Processing continues even if you close this page. Review and publish when ready.',
      );
      await load();
    } catch (error) {
      setError(message(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="workspace-page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">TRUSTED KNOWLEDGE</span>
          <h1>Good answers start here.</h1>
          <p className="flow-muted">
            Upload, inspect, then publish. Draft documents never appear in
            customer answers.
          </p>
        </div>
        <button
          className="primary-button"
          onClick={() => {
            setReplacement(null);
            setError('');
            setUpload(true);
          }}
        >
          <Icon name="plus" />
          Upload policy
        </button>
      </div>
      <ErrorNotice error={error} />
      <RetrievalPreview />
      {notice && (
        <p className="flow-notice" role="status">
          {notice}
        </p>
      )}
      <div className="table-toolbar">
        <input
          aria-label="Search documents"
          placeholder="Search knowledge"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        <button className="secondary-button" onClick={() => void load()}>
          Refresh
        </button>
        <span>{documents.length} documents</span>
      </div>
      {!loaded ? (
        <p>Loading knowledge…</p>
      ) : documents.length === 0 ? (
        <div className="empty-state">
          <Icon name="book" />
          <h2>Give your assistant something to work with.</h2>
          <p>
            Upload your first PDF or TXT policy, review its passages, then
            publish it.
          </p>
        </div>
      ) : (
        <div className="knowledge-list">
          {documents
            .filter((d) => d.title.toLowerCase().includes(filter.toLowerCase()))
            .map((document) => (
              <article className="knowledge-card" key={document.id}>
                <div className="knowledge-heading">
                  <div>
                    <h2>{document.title}</h2>
                    <small>
                      {document.category} ·{' '}
                      {document.activeVersionId
                        ? 'Published policy available'
                        : 'Not published'}
                    </small>
                  </div>
                  <button
                    className="secondary-button"
                    onClick={() => {
                      setReplacement(document);
                      setUpload(true);
                    }}
                  >
                    Upload new version
                  </button>
                </div>
                {document.versions.map((version) => (
                  <div className="version-row" key={version.id}>
                    <div>
                      <strong>Version {version.label}</strong>
                      <small>
                        Effective {policyDate(version.effectiveDate)}
                      </small>
                    </div>
                    <div>
                      <span className={`status-badge state-${version.state}`}>
                        {version.state}
                      </span>
                      <small>
                        {version.publication} · {version.indexedCount}/
                        {version.chunkCount} passages indexed
                      </small>
                      {version.errorCode && (
                        <p className="inline-error">
                          {version.errorCode.replaceAll('_', ' ')}
                        </p>
                      )}
                    </div>
                    <div className="row-actions">
                      <button
                        className="secondary-button"
                        onClick={() => void inspect(document, version)}
                      >
                        Review
                      </button>
                      {['failed', 'canceled'].includes(version.state) && (
                        <button
                          disabled={busy}
                          className="text-button"
                          onClick={() =>
                            void action(`/admin/versions/${version.id}/retry`)
                          }
                        >
                          Retry
                        </button>
                      )}
                      {['queued', 'extracting', 'indexing'].includes(
                        version.state,
                      ) && (
                        <button
                          disabled={busy}
                          className="text-button"
                          onClick={() =>
                            void action(`/admin/versions/${version.id}/cancel`)
                          }
                        >
                          Cancel
                        </button>
                      )}
                    </div>
                  </div>
                ))}
                <div className="knowledge-footer">
                  <button
                    className="text-button"
                    disabled={busy || !document.activeVersionId}
                    onClick={() => {
                      if (
                        confirm(
                          `Archive “${document.title}”? It will stop appearing in new answers and source details.`,
                        )
                      )
                        void action(
                          `/admin/documents/${document.id}/archive`,
                          document.rowVersion,
                        );
                    }}
                  >
                    Archive policy
                  </button>
                  <button
                    className="text-button danger"
                    disabled={busy}
                    onClick={() => {
                      if (
                        confirm(
                          `Delete “${document.title}”? Access will be withdrawn immediately. Stored files remain pending retention cleanup.`,
                        )
                      )
                        void action(
                          `/admin/documents/${document.id}`,
                          document.rowVersion,
                          'DELETE',
                        );
                    }}
                  >
                    Delete policy
                  </button>
                </div>
              </article>
            ))}
        </div>
      )}
      {upload && (
        <Dialog
          title={replacement ? `Replace ${replacement.title}` : 'Add a policy'}
          onClose={() => {
            if (!busy) setUpload(false);
          }}
        >
          <p className="flow-muted">
            Text-based PDF or UTF-8 TXT, up to 20 MiB. Scanned PDFs require OCR
            and cannot be indexed here.
          </p>
          <form className="flow-form" onSubmit={(event) => void submit(event)}>
            <label>
              Document title
              <input
                name="title"
                required
                maxLength={160}
                defaultValue={replacement?.title ?? ''}
              />
            </label>
            <div className="form-columns">
              <label>
                Category
                <input
                  name="category"
                  maxLength={80}
                  defaultValue={replacement?.category ?? 'General'}
                />
              </label>
              <label>
                Version
                <input
                  name="version"
                  required
                  maxLength={40}
                  defaultValue={replacement ? '' : '1.0'}
                  placeholder="e.g. 2.0"
                />
              </label>
            </div>
            <label>
              Effective date
              <input
                type="date"
                name="effectiveDate"
                required
                defaultValue={new Date().toISOString().slice(0, 10)}
              />
            </label>
            <label className="file-picker">
              Policy file
              <input type="file" name="file" accept=".pdf,.txt" required />
            </label>
            <ErrorNotice error={error} />
            <button className="primary-button" disabled={busy}>
              {busy ? 'Uploading…' : 'Upload for review'}
            </button>
          </form>
        </Dialog>
      )}
      {review && (
        <Dialog
          wide
          title={review.document.title}
          onClose={() => setReview(null)}
        >
          <p className="flow-muted">
            Version {review.version.label} · {review.version.state} ·{' '}
            {review.version.publication}
          </p>
          <ErrorNotice error={error} />
          {review.passages.length === 0 ? (
            <p>
              No extracted passages yet. Close this review and reopen after
              processing completes.
            </p>
          ) : (
            <div className="review-passages">
              {review.passages.map((p) => (
                <section key={p.id}>
                  <small>
                    {p.sourceKey} ·{' '}
                    {p.page ? `Page ${p.page}` : `Line ${p.lineStart}`}
                  </small>
                  <p className="preserve-text">{p.text}</p>
                </section>
              ))}
            </div>
          )}
          <div className="review-actions">
            <span>
              Publishing makes this version available to customers and replaces
              the active version.
            </span>
            <button
              className="primary-button"
              disabled={
                busy ||
                review.version.state !== 'ready' ||
                review.document.activeVersionId === review.version.id
              }
              onClick={() =>
                void action(
                  `/admin/versions/${review.version.id}/publish`,
                  review.document.rowVersion,
                )
              }
            >
              {busy
                ? 'Verifying index…'
                : review.document.activeVersionId === review.version.id
                  ? 'Published'
                  : 'Publish policy'}
            </button>
          </div>
        </Dialog>
      )}
    </section>
  );
}
type Metrics = {
  requests: number;
  completed: number;
  failed: number;
  unknown: number;
  feedbackCount: number;
  helpfulCount: number;
  p95Ms: number | null;
  openTickets: number;
  failedDocuments: number;
  since: string;
  until: string;
};
export function Dashboard() {
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [feedback, setFeedback] = useState<
    {
      messageId: string;
      helpful: boolean;
      comment: string | null;
      answer: string;
    }[]
  >([]);
  const [error, setError] = useState('');
  useEffect(() => {
    void Promise.all([
      api<Metrics>('/admin/metrics'),
      api<typeof feedback>('/admin/feedback'),
    ])
      .then(([m, f]) => {
        setMetrics(m);
        setFeedback(f);
      })
      .catch((e) => setError(message(e)));
  }, []);
  return (
    <section className="workspace-page">
      <span className="eyebrow">ANSWER QUALITY</span>
      <h1>A clearer picture.</h1>
      <p className="flow-muted">
        Actual activity from this local installation over the last 30 days.
      </p>
      <ErrorNotice error={error} />
      {metrics ? (
        <>
          <div className="metrics-grid">
            {[
              {
                label: 'Questions received',
                value: metrics.requests,
                detail: `${metrics.completed} completed · ${metrics.failed} failed`,
              },
              {
                label: 'Helpful responses',
                value: metrics.feedbackCount
                  ? `${Math.round((metrics.helpfulCount / metrics.feedbackCount) * 100)}%`
                  : 'No votes yet',
                detail: `${metrics.helpfulCount} helpful / ${metrics.feedbackCount} votes`,
              },
              {
                label: 'Unknown answers',
                value: metrics.unknown,
                detail: `Out of ${metrics.completed} completed responses`,
              },
              {
                label: 'Answer time · p95',
                value:
                  metrics.p95Ms === null
                    ? 'No samples yet'
                    : `${(metrics.p95Ms / 1000).toFixed(1)}s`,
                detail: `${metrics.completed} completed samples`,
              },
              {
                label: 'Open support cases',
                value: metrics.openTickets,
                detail: 'Open or in progress · all time',
              },
              {
                label: 'Failed uploads',
                value: metrics.failedDocuments,
                detail: 'Needs review · all time',
              },
            ].map((m) => (
              <article key={m.label}>
                <span>{m.label}</span>
                <strong>{m.value}</strong>
                <small>{m.detail}</small>
              </article>
            ))}
          </div>
          <p className="flow-note">
            Window: {date(metrics.since)} – {date(metrics.until)}. These are
            measured counts, not accuracy claims.
          </p>
          <h2>Recent feedback</h2>
          {feedback.length === 0 ? (
            <div className="empty-state">
              <p>No feedback has been submitted yet.</p>
            </div>
          ) : (
            feedback.map((f) => (
              <article className="feedback-row" key={f.messageId}>
                <strong>{f.helpful ? 'Helpful' : 'Not helpful'}</strong>
                <p>{f.answer}</p>
                {f.comment && <small>{f.comment}</small>}
              </article>
            ))
          )}
        </>
      ) : (
        !error && <p>Loading measured activity…</p>
      )}
    </section>
  );
}
