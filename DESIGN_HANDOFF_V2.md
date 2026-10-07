# TruvoID — Design Handoff (V2 Redesign)

Purpose: hand the current product surface (V1) to a redesign so V2 can be built cleanly.
This document lists every screen, every user flow, the current design language, and the
infrastructure upgrades V2 should include.

Stack today: **React + Vite SPA** (`web/`) against a **.NET 10 minimal API + PostgreSQL**
(`src/TruvoID.API/`). The old Blazor app has been removed. Public API base is
`https://api.gettruvoid.com`; the SPA origin is `https://gettruvoid.com` (Vercel).

---

## 1. Current design language (what V2 should consciously keep or replace)

- **Theme:** dark-first. Background `#071018`, panels `#0d1b24`, borders translucent teal.
- **Accent tokens:** `--cyan` (primary), `--lime` (positive), `--warm-gold` (secondary/CTA).
- **Type:** `Space Grotesk` (headings), `DM Mono` (labels, code, eyebrows), loaded from Google Fonts.
  Marketing uses a second palette under `.marketing-site` (`--mk-*`).
- **Reusable classes:** `.button` / `.button-primary` / `.button-secondary`, `.field`,
  `.form-card`, `.table-wrap`, `.badge`, `.notice`, `.segmented`, `.mode-switch`,
  `.stat-card`, `.activity-card`, `.key-reveal`, `.empty`, `.retry-button`, `.spinner`,
  `.skeleton`, `.review-*`, `.wallet-hero`, `.sandbox-banner`.
- **Two CSS files:** `styles.css` (shell/auth) and `migration.css` (workspace + marketing).
  V2 should consolidate into a token-driven system (design tokens → components).

---

## 2. Screen inventory (V1)

### 2.1 Marketing (public)
| Route | Screen | Notes |
|---|---|---|
| `/` | Home | Hero, proof stats, services grid (NIN/BVN/Phone; CAC "coming soon"), architecture model (Institutions vs Agencies), developer section, security, CTA, footer |
| `/solutions` | Solutions | Institutions vs Agencies split, access levels, "operational" console mock, out-of-date capability claims |
| `/api-and-developers` | API & Developers | Hero, request/response console, 5-step integration, environments, use cases |
| `/about` | About | Hero, capability grid, approach/principles, **contact form** (opens the mail client) |
| `/api-docs` → `/api-docs.html` | API reference | Static HTML with tabbed examples, copy buttons; external `api-docs.js` |

### 2.2 Authentication (public)
| Route | Screen | Notes |
|---|---|---|
| `/login` | Workspace sign in | Multi-step progress; shows "session expired" notice on `?expired=1` |
| `/admin/login` | Platform admin sign in | Separate; undocumented in the UI (staff-only URL) |
| `/register` | Institution sign-up | Institution name, admin name/email, password; provisions workspace |
| `/accept-agency-invite` | Accept agency invite | Legacy invite flow |
| `/accept-team-invite` | Accept team invite | Team member activation |
| `/accept-invite` | Accept organization invite | Ops invite; activates institution/agency + admin |
| `/forgot-password` | Forgot password | Sends reset link |
| `/reset-password` | Reset password | Token + new password |

### 2.3 Workspace (organization users)
| Route | Screen | Role access |
|---|---|---|
| `/dashboard` | Overview | All workspace users; status-aware cards (setup / under review / changes / approved) |
| `/setup` | Organization setup | Org admins only; 9 sections + access level + documents + attestation + submit |
| `/verify` | Verify identity | All; Test/Live control, type switch (NIN/BVN/Phone), result card (identity, charge, balance) |
| `/history` | Verification history | All; filters (type/status), result badges, embedded on Verify |
| `/team` | Team | Org admins; invite, roles, outlet scope (agency), disable/reactivate |
| `/outlets` | Outlets | Org admins; list + create |
| `/outlets/:outletId` | Outlet detail | Org admins; outlet wallet, activity, suspend/reactivate |
| `/api-keys` | API keys | Org admins; test/live keys, per-outlet keys (agency), revoke |
| `/wallet` | Wallet | All; balance, ledger, Flutterwave checkout, bank-transfer instructions |
| `/settings` | Account settings | All; change password; org admins can deactivate the organization |

### 2.4 Outlet-scoped experience
Outlet users (`outlet_owner`/`outlet_staff`) see a reduced nav: **Overview, Verify,
History, Wallet, Settings**. All data is RLS-scoped to their outlet.

### 2.5 Platform admin
| Route | Screen | Notes |
|---|---|---|
| `/admin/agencies` | Organizations | List (status, setup, users, **balance**), create organization (invitation), review profile, add credit, suspend/reactivate |
| `/admin/financials` | Financials | Credit-sale/refund totals + net, by type, top organizations, recent ledger; 7/30/90-day window |
| `/admin/pricing` | Pricing | Platform rates per type; **per-organization overrides** |
| `/admin/api-keys` | API keys | All keys across tenants; issue tenant key; revoke |
| `/admin/audit` | Activity log | Paginated audit entries with action/entity filters |

### 2.6 Shared components / overlays
- **NotificationsBell** — unread badge + dropdown; mark read / all read (60s poll).
- **AdminReview** — profile review panel (sections, documents, approve / request changes).
- **ConfirmButton** — inline confirm wrapper used for destructive actions.
- **CopyButton** — clipboard with copied/failed feedback.
- **Mode switch (Test/Live)** — top bar + on the Verify page.
- **Banners** — SANDBOX, SETTING UP, TEST MODE, LIVE MODE AVAILABLE, LIVE MODE, LIVE MODE LOCKED.
- **Loading / empty / error / retry** states across tables.

---

## 3. User flows (V1)

1. **Institution onboarding**
   Register → provision workspace → Dashboard (setup %) → fill the 9 profile sections
   (auto-saves on section switch) → upload documents → choose access level → attest →
   submit → Dashboard/Setup show "under review" → platform admin reviews → **approved**
   (live enabled) or **changes requested** (org edits + resubmits).
2. **Sign in / session**
   Email + password → multi-step progress → workspace. 401s refresh silently; a failed
   refresh clears the session, clears the Test/Live mode, and lands on `/login?expired=1`.
3. **Invitations (Ops)**
   Admin creates organization invitation → email + copyable link → recipient opens
   `/accept-invite` → previews org → sets password → organization + admin created → sign in.
4. **Team invitation**
   Org admin invites (role + outlet scope for agencies) → email + link → `/accept-team-invite`.
5. **Verification**
   Verify page → choose type (NIN/BVN/Phone) + Test/Live → submit → result card
   (match/no-match/provider error, identity once, charge, wallet-after). Test mode uses
   documented test numbers and is free; live is billed and gated on approval.
6. **Wallet funding**
   Wallet → amount → **Flutterwave** hosted checkout (redirect back → verify) or
   **bank transfer** instructions + support contact → admin credits manually.
7. **Outlets (agency)**
   Create outlet → per-outlet wallet (agency) or shared org wallet (institution) →
   outlet detail: activity + suspend/reactivate → issue per-outlet API keys.
8. **API keys**
   Org admin issues test/live org keys; agency admin issues per-outlet keys; single-reveal;
   revoke with confirmation.
9. **Platform operations**
   Organizations: invite, review profile, credit wallet, suspend/reactivate.
   Pricing: platform rates + per-org overrides. API keys: issue/revoke. Activity: audit log.
   Financials: revenue over a window.
10. **Password lifecycle**
    Forgot → emailed reset link → reset → sign in. Settings → change password (other
    devices signed out). Org admin can deactivate the organization (all sessions revoked).
11. **Notifications**
    In-app bell (profile approved / changes requested today); polled every 60s.

---

## 4. Backend surface (for V2 parity)

Auth: register, login, admin login, refresh, me, change-password, deactivate, logout,
forgot/reset password, invite preview/accept, change-password. Tenancy: setup (sections,
access level, attestation, documents, submit), branding, team, outlets, API keys, wallet
(balance, ledger, sandbox-funds, outlet purchase-credit), verification
(`/v1/verify/{type}`, test-numbers, pricing, history). Payments: Flutterwave
initialize/verify/webhook. Admin: organizations list + suspend/reactivate + setup
review + documents, tenant-wallet credit, pricing (platform + per-org), API keys,
invitations, audit, financials. Notifications: list/read/read-all. Health: `/health`,
`/health/ready`.

Isolation is enforced by PostgreSQL RLS with a per-organization DB role; the runtime API
holds DML-only credentials. Migrations run separately as the DDL-owning role.

---

## 5. V2 — Information architecture & UX direction (for the redesign)

- **Two products in one shell:** the workspace (org/outlet) and the platform console.
  Give them distinct visual identities while sharing tokens.
- **Role-aware, first-class personalization:** nav and default landing differ by role
  (platform admin, institution admin, agency admin, staff, outlet). Avoid screens that 403.
- **Onboarding as a guided flow**, not a long form: progress, autosave, clear review state.
- **Verify as the hero action:** one clear result surface; make Test vs Live unmistakable;
  show balance impact inline.
- **Real-time where it matters:** wallet balance and activity should update live (SSE/WS or
  short polling), not on manual reload.
- **Consistency:** one confirm pattern, one toast/notification pattern, one empty/error
  pattern, one table pattern.

---

## 6. Infrastructure upgrades & changes required for V2

### 6.1 Observability (partially done in V1)
- **Done:** correlation id (`X-Request-Id`) + structured request logging; `/health` and
  `/health/ready`.
- **V2:** add **error tracking** (Sentry or OpenTelemetry), **metrics** (OTel → Prometheus/
  Grafana: request rate/latency/errors, verify success rate, provider latency, outbox lag,
  wallet credit failures), **distributed tracing** across API → worker → provider, and
  **alerting** (worker down, outbox backlog, webhook failures, provider error spike, DB
  saturation). Emit logs as JSON for the platform.

### 6.2 Reliability & delivery
- **Staging environment** mirroring prod + a migration/rollback runbook.
- **DB backups with PITR**, tested restore, and a disaster-recovery drill.
- **Blue/green or canary deploys**; health-gated rollouts using `/health/ready`.
- **Queue-based worker** (e.g., a job table or broker) instead of interval polling; retries
  with backoff; dead-letter visibility.
- **Load/soak testing** with the provider mocked; capacity targets documented.
- **Idempotency everywhere** (payments already; extend to webhooks and provider calls).

### 6.3 Security & compliance
- **MFA** for platform admins (and optionally org admins).
- **Session hardening:** consider httpOnly, SameSite cookies or short-lived access tokens +
  rotation (today tokens are in `localStorage`); add a session/device manager.
- **CSP is in place** (Vercel headers); add **report-uri/report-to** and tune.
- **Secret management** (host secret store), **secret rotation**, and no secrets in config.
- **Dependency & container scanning** (Dependabot, Trivy) and **SAST in CI** (Qodana is
  configured but not wired).
- **Upload scanning** (AV / content sniffing), and HMAC the verification `subject_ref`.
- **Compliance:** NDPA/DPA, privacy policy, terms, retention & erasure flows.
- **Penetration test** before go-live; document the threat model.

### 6.4 Data & analytics
- Event pipeline for product analytics (funnel: sign-up → setup → first verification →
  live), plus revenue reconciliation jobs.
- Audit retention policy and admin access review.
- Read replicas / caching if verification history or financials grow.

### 6.5 CI/CD
- Pipeline builds API + tests + web + web tests (already), adds **e2e (Playwright)** and
  **coverage gates**, and **preview deploys** per PR.
- **Feature flags** for progressive rollout of V2 screens.

### 6.6 Product features to add in V2 (backend exists or partially exists)
- **Customer-facing webhooks** (notify orgs of verification events).
- **Notifications** expansion (low balance, failed payments, outlet events) with email +
  in-app parity and preferences.
- **Exports** (CSV) of verification history and wallet ledger for reconciliation.
- **Session/device management** UI.
- **Public pricing** and legal pages.

---

## 7. Environment variables (reference)

API: `ConnectionStrings__Postgres`, `ConnectionStrings__PostgresMigrator`,
`Postgres__TenantCredentialKey(Id)`, `Postgres__AppRole`, `Jwt__SecretKey`,
`Jwt__Issuer`, `Jwt__Audience`, `Cors__Origins__0`, `App__BaseUrl`,
`Resend__ApiKey` / `RESEND_API_KEY`, `EMAIL_FROM_ADDRESS`, `Verification__Provider`,
`IDACCESS_API_KEY` (or `IDACCESS_SECRET_KEY`), `IDACCESS_BASE_URL`,
`Flutterwave__SecretKey`, `Flutterwave__WebhookHash`, `RateLimits__*`.

Web: `VITE_API_BASE_URL`, `VITE_SUPPORT_WHATSAPP`, `VITE_SUPPORT_EMAIL`,
`VITE_BANK_ACCOUNTS` (JSON).

---

## 8. Known V1 gaps carried into V2 (fix during redesign)
- Marketing claims vs implemented capabilities; single contact address (mostly fixed).
- Real-time balance is manual-reload today.
- No error tracking/metrics/alerting (this doc's §6.1).
- No MFA; tokens in `localStorage`.
- Legal pages missing.
- Platform admin management is CLI-only (`create-platform-admin`).
- No customer webhooks, exports, or session management.
