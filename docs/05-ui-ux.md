# UI and UX specification

## Experience direction

Build a calm, credible retail support experience. Give the answer visual priority, keep evidence one click away, and make escalation easy. Avoid a generic AI dashboard full of decorative charts, gradients and implementation terminology. Customers see “Checking Haven policies,” never “Embedding vectors.”

## Visual system

| Token | Initial specification |
| --- | --- |
| Background | Warm neutral `#F7F8FA` |
| Surface | White `#FFFFFF`; subtle borders `#D8DFE7` |
| Main text | `#172B3A`; secondary `#4B5D6B` |
| Brand/action | Deep teal `#0B655C`, white action text |
| Focus | High-contrast visible ring, offset from component boundary |
| States | Error, warning and success use icon + text as well as tested colors |
| Type | Local system sans stack; body 16 px, line-height 1.5–1.65; clear heading scale |
| Layout | 4/8 px spacing rhythm; 12 px cards; restrained shadows |
| Targets | Prefer >= 44 x 44 px interactive areas; controls remain reachable on touch |
| Reading | Answer measure about 65–75 characters; source panel may be independently scrollable |

These are design inputs, not a contrast certification. Check actual foreground/background pairs, hover, focus, disabled states and zoom in implementation. Use one consistent accessible icon set with text labels for ambiguous actions. Respect reduced motion and system text scaling; no essential animation.

## Information architecture

Customer: Support chat, conversation history, My tickets, account menu. Anonymous users start immediately; explain session history limits without forcing sign-up. Admin: Overview, Knowledge, Tickets, Feedback. Customer and admin navigation are visually distinct; server authorization protects both regardless of visible links.

## Chat screen

Desktop: compact header, collapsible 260 px history sidebar, centered conversation area, optional 360 px source drawer. Tablet: history as a drawer, source detail overlays alongside or replaces the reading panel. Mobile from 320 px: single column, full-height source sheet, keyboard-safe sticky composer, no horizontal scrolling. Test 320, 375, 768, 1024 and 1440 px widths.

Empty state: “How can we help?” with four genuine examples: returns, delivery, warranty, product care. Explain “Answers include the policy passages they use.” Show an unobtrusive AI disclosure and link to human help. Do not fill chat with a fake conversation.

Messages: user text readable in a distinct bubble; assistant response on an open surface. Put a short answer before supporting detail. Source chips show document title and page when available. Place copy, helpful/not helpful and “Get human help” actions after completed responses; copied confirmation is announced. Feedback controls have readable labels, selected states and an optional reason form.

Composer: labeled multiline input, Send button, Enter to send and Shift+Enter for newline, IME-safe handling, character limit, preserved draft. Disable empty sends. One request in flight per conversation; show Stop while processing. Do not erase a question on error. If the reader scrolls upward, do not force auto-scroll; show “New response” with a jump action.

Progress stages: “Checking policies,” “Preparing your answer,” then validated answer. Never simulate percentages or typewriter tokens. After a measured wait threshold, show “This is taking longer than usual. You can stop and try again.” Avoid rapidly changing screen reader announcements.

## Source detail

Show title, version, effective date, page or TXT line range, exact supporting passage, and relevant highlighted quote. Preserve paragraphs and allow copying. Use a focus-managed dialog/drawer with Escape close and focus return to the original chip. A withdrawn source shows a clear unavailable state and an action to ask again using current policy. Do not make a raw uploaded PDF the only way to verify an answer.

## Human support flow

“Get human help” opens an editable draft with subject, concise issue summary, contact email and optional order reference. Include a collapsed preview of the exact conversation excerpts to be shared. Default to minimal relevant context, require an explicit consent checkbox, explain that a local support ticket will be created, and use **Submit ticket** as the final action. Show inline errors, block duplicate submission, preserve draft on network failure, then show ticket reference and status. Never claim an email was sent. Customer can inspect the case from My tickets.

## Admin screens

- Overview: actionable failed uploads, open tickets, unanswered questions and measured latency. Each metric includes timeframe, denominator and empty state; chart visuals accompany readable values.
- Knowledge: search and filter table with title, version, effective date, publication state, processing stage, updated time and contextual actions. Upload drawer accepts PDF/TXT with limits and a normal file picker as well as drag/drop.
- Document review: page/passages preview, extraction warnings, indexed count, current/replacement comparison and explicit Publish. Unsafe extraction blocks publication with recovery instructions.
- Tickets: status/search filters, concise rows, details and customer-approved context; visible unsaved state and conflict resolution if another admin changed it.
- Feedback: useful/not useful totals, reason filters, permission-checked answer/source context; never expose customer content on an anonymous dashboard.

Destructive actions name the document and explain consequences in a confirmation dialog. Success toasts supplement persistent state updates. Do not rely on toasts for errors requiring action.

## Required state matrix

| Component | States that must be designed and checked |
| --- | --- |
| Chat | Empty, composing, processing, slow, complete, clarification, unknown, human help, canceled, failed, offline, recovered after reload |
| Sources | Loading, available, withdrawn, unauthorized, load failure |
| Upload | Idle, dragging, validating, queued, processing, warning, ready, published, failed, canceled, retrying |
| Tickets | Draft, invalid, submitting, saved, failed, duplicate retry, status conflict |
| Feedback | Unselected, selected, pending, saved, failed/retry |
| Admin tables | Loading skeleton, empty, filtered empty, populated, partial dependency error, forbidden |
| Account | Signed out, signing in, invalid credentials, expired session, refresh failure, signed in |

## Accessibility and usability gate

Use landmarks, semantic headings, real buttons, proper labels, error associations, skip link, focus order, keyboard menus, restrained aria-live regions and accessible dialog primitives. Focus stays in the composer during generation; completion does not unexpectedly steal focus. Support 200% zoom and reflow at 400%, reduced motion, screen reader source reading, and color-independent statuses. Tables have headers and a usable narrow-screen presentation.

Before coding business screens, approve implementation internally against wireframes for chat, source drawer, ticket review and knowledge review. Test the five-person journey study described in the product requirements. Record task completion, time, confusion, severity and changes. Polish is demonstrated by successful tasks, not decorative density.
