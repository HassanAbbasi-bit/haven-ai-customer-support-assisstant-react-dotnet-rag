let token = '';
let refreshing: Promise<boolean> | null = null;
export function clearToken() {
  token = '';
}
export function setToken(value: string) {
  token = value;
}
export async function refreshSession(): Promise<boolean> {
  refreshing ??= fetch('/api/v1/auth/refresh', {
    method: 'POST',
    credentials: 'include',
    headers: { 'X-Haven-Request': '1' },
  })
    .then(async (response) => {
      if (!response.ok) return false;
      const payload = (await response.json()) as { accessToken: string };
      token = payload.accessToken;
      return true;
    })
    .catch(() => false)
    .finally(() => {
      refreshing = null;
    });
  return refreshing;
}
export async function api<T>(
  path: string,
  options: RequestInit = {},
  retry = true,
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('X-Haven-Request', '1');
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (options.body && !(options.body instanceof FormData))
    headers.set('Content-Type', 'application/json');
  const response = await fetch(`/api/v1${path}`, {
    ...options,
    headers,
    credentials: 'include',
    signal: options.signal ?? AbortSignal.timeout(15000),
  });
  if (response.status === 401 && retry && token && (await refreshSession()))
    return api<T>(path, options, false);
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ??
        (response.status === 401
          ? 'Your session expired. Sign in again or refresh to start a guest session.'
          : response.status === 403
            ? 'This action requires an administrator account.'
            : response.status === 429
              ? 'Too many requests. Please wait a minute and try again.'
              : 'The service is unavailable. Please try again.'),
    );
  }
  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}
export type Identity = {
  id: string;
  email?: string;
  role: 'Guest' | 'Customer' | 'Admin';
};
let boot: Promise<Identity> | null = null;
export function bootSession() {
  boot ??= (async () => {
    if (!(await refreshSession()))
      await api('/auth/guest-session', { method: 'POST' });
    return api<Identity>('/auth/me');
  })().catch((error) => {
    boot = null;
    throw error;
  });
  return boot;
}
export function resetBoot() {
  boot = null;
}
export const json = (body: unknown) => JSON.stringify(body);
export const message = (error: unknown) =>
  error instanceof Error
    ? error.message
    : 'Something went wrong. Please try again.';
export const date = (value: string) =>
  new Intl.DateTimeFormat('en', { dateStyle: 'medium' }).format(
    new Date(value),
  );
export const policyDate = (value: string) =>
  new Intl.DateTimeFormat('en', {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(value));
export type Source = {
  id: string;
  messageId: string;
  documentTitle: string;
  version: string;
  effectiveDate: string;
  page: number | null;
  lineStart: number | null;
  lineEnd: number | null;
  sourceKey: string;
  quote: string;
  claim: string;
  text: string;
  available: boolean;
};
export type Message = {
  id: string;
  requestId: string;
  role: string;
  text: string;
  kind: string;
  citations: Source[];
  feedback: {
    helpful: boolean;
    comment?: string | null;
    reason?: string | null;
  } | null;
};
export type RequestState = {
  id: string;
  state: string;
  stage: string;
  errorCode: string | null;
  question: string;
};
export type Conversation = {
  id: string;
  title: string;
  messages: Message[];
  requests: RequestState[];
};
export type History = { id: string; title: string; updatedAt: string };
export type Ticket = {
  id: string;
  reference: string;
  subject: string;
  summary: string;
  email: string;
  orderReference: string | null;
  sharedContext: string;
  status: string;
  createdAt: string;
  rowVersion: string;
};
export type Version = {
  id: string;
  label: string;
  state: string;
  publication: string;
  errorCode: string | null;
  chunkCount: number;
  indexedCount: number;
  effectiveDate: string;
};
export type Document = {
  id: string;
  title: string;
  category: string;
  activeVersionId: string | null;
  rowVersion: string;
  versions: Version[];
};
export type Review = {
  document: Document;
  version: Version;
  passages: {
    id: string;
    text: string;
    page: number | null;
    lineStart: number | null;
    sourceKey: string;
  }[];
};
