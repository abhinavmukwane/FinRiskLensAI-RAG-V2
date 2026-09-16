# API Contracts

> **Implementation status (2026-07-02):** the onboarding/login (§1), score
> retrieval (§2), ULI DSP (§3), LOS (§4) and refresh (§5) surfaces below are
> **not yet built**. What exists today, not anticipated by this doc, is the
> scoring/analysis surface (see `08_ML_ENGINE.md` for full contracts):
> - `POST /api/scoring/analyze` — stateless direct analysis, payloads in body
>   (dev/test/what-if harness, not the production path)
> - `api/msme-data/{uan}` — the blob-storage collection + analysis flow:
>   `PUT /manifest`, `PUT /files/{fileName}`, `GET /status`,
>   `POST /analyze?force=`, `GET /result`
> When §2 (score retrieval) is built, `GET /api/msme-data/{uan}/result` already
> covers most of "get current score" — §2 should either wrap it or read the
> persisted `ScoreComputation` rows once those entities exist. §3/§4 remain
> blocked on the JWT bearer registration (gap 2 in `07_SETUP_GAPS.md`).
>
> **Update (2026-07-03):** our own Razor consumer of §2 now exists as an MVC
> page, not an API: `GET /Dashboard/FinancialHealthCard?uan={UAN}` reads the
> blob `result.json` server-side via `IBlobAnalysisService` (no HTTP hop) and
> triggers recomputes through the existing
> `POST /api/msme-data/{uan}/analyze?force=true`. A JSON §2 endpoint is still
> unbuilt — external callers should keep using `GET /api/msme-data/{uan}/result`.

This defines the API surface at a contract level — request/response
shape and purpose. Exact route naming, versioning scheme, and DTO class
names are left to you; follow whatever convention the rest of the
`FinRiskLensAI` codebase already uses once controllers exist.

## 1. Onboarding & login API (internal — MSME-facing app/portal calls this)

**Udyam lookup** (`02_CUSTOMER_FLOW.md` Step 1)
- Input: Udyam Registration Number
- Output: the mapped Udyam profile fields (enterprise name, owner name,
  PAN, linked GSTIN status, mobile, email, enterprise type, major
  activity, address, incorporation/commencement dates, organization
  type, bank details) for the confirmation screen, plus a flag
  indicating whether an `Msme` already exists for this Udyam number
  (drives whether the client shows "Register" or routes straight to
  login)
- This is a lookup, not yet a write — nothing is persisted to `Msme`
  from this call alone

**Register** (`02_CUSTOMER_FLOW.md` Step 2)
- Input: Udyam Registration Number (re-confirms the lookup above),
  explicit consent acknowledgment
- Output: pending `Msme` created from the Udyam response fields; email
  OTP dispatched to the Udyam-sourced `emailId`
- If an `Msme` already exists for this Udyam number, this call should
  reject/redirect rather than create a duplicate — the client should
  generally have avoided calling this in that case (see the lookup
  response above), but the server must guard it too

**Verify registration OTP** (`02_CUSTOMER_FLOW.md` Step 3)
- Input: Msme identifier (or Udyam number), OTP code
- Output: verification result; on success, `Msme.RegistrationStatus`
  moves to `Active` and an authenticated session/token is issued for
  dashboard access

**Login** (`02_CUSTOMER_FLOW.md` Step 3b / Step 6 — existing users)
- Input: Udyam Registration Number (or whatever identifier the chosen
  login mechanism uses)
- Output: email OTP dispatched to the `Msme`'s stored `EmailId`

**Verify login OTP**
- Input: Msme identifier (or Udyam number), OTP code
- Output: authenticated session/token; also indicates whether the
  dashboard should land on "continue onboarding" (Step 4, no current
  score yet) or "show current score" (Step 5)

**Submit GSTIN** (`02_CUSTOMER_FLOW.md` Step 4 — first dashboard visit
only)
- Input: Msme identifier, GSTIN (client should pre-fill this from the
  Udyam lookup's linked-GSTIN info when available, and only prompt for
  manual entry if that wasn't supplied)
- Output: confirmation; proceeds to AA consent request

**Get consent request details** (what will be shown to the MSME before
they approve, per AA framework disclosure requirements)
- Input: Msme identifier
- Output: FIP list, data types requested, time window (6-12 months),
  purpose

**Submit AA consent** (or receive callback — see Integrations doc for
which leg is a redirect vs a webhook)
- Output: `ConsentRecord` status

**Trigger manual refresh** (`02_CUSTOMER_FLOW.md` Step 5 — dashboard
action for an already-scored MSME)
- Input: Msme identifier
- Output: confirmation that a re-pull + rescore has been queued; this
  is a thin wrapper that re-enters the same flow as Step 4's data
  collection, not a separate code path

## 2. Score retrieval API (internal — used by our own Razor views)

**Get current score**
- Input: Msme identifier
- Output: `ScoreComputation` (overall score, band, six sub-scores,
  freshness timestamp, which dimensions used reduced/default input),
  plus its `ScoreExplanation` children and
  `CreditProductRecommendation` children

**Get score history**
- Input: Msme identifier, optional date range
- Output: list of past `ScoreComputation` summaries (score + band +
  computed date only — enough for the trend chart, not full detail per
  point)

> **Direction matters.** §3 and §4 below are *inbound*: a lender calls us and we
> answer with a score. They are still unbuilt. What **is** built (2026-09-15) is the
> *outbound* direction — we call them, raising a scored MSME as a loan case. See
> §6.

## 3. ULI DSP-compatible Score API (external — any OCEN-compliant lender)

This is the contract that makes the platform reusable infrastructure,
not just an IDBI-internal tool. Model it after ULI's DSP pattern: the
caller authenticates via OAuth2 + mTLS (enforced at the Azure API
Management gateway layer, not in application code), then queries by a
public business identifier.

**Request score**
- Input: GSTIN (or another agreed public identifier — do not use our
  internal `Msme.Id` as the external contract key), requesting lender's
  purpose code
- Output: score band + overall score at minimum; full dimension
  breakdown only if the querying lender's authorization scope permits
  it (some lenders may be entitled only to a band, not the full score,
  depending on the MSME's consent scope for that specific query — model
  this as a scope check in the service layer, not the controller)
- If no current score exists for the GSTIN: a clear "not found /
  not yet assessed" response, not a 500 or an empty score

**Important:** every external query against this endpoint should itself
be logged (who queried, when, what was returned) — this is a
consent-adjacent action and needs its own audit trail, similar in
spirit to `ConsentRecord`. Consider a `ScoreQueryLog` entity if this
isn't already covered by request logging elsewhere.

## 4. IDBI LOS webhook / API (external — but a "known" internal partner,
distinct from the general ULI surface above because it's a direct
integration, not an arms-length OCEN query)

**LOS requests score for an application**
- Input: GSTIN, LOS application reference number
- Output: full `ScoreComputation` detail (LOS gets full detail because
  it's the primary consuming bank, not a third-party OCEN lender),
  including explanations and recommendations
- If the MSME hasn't onboarded through FinRiskLensAI yet: return a
  response that lets the LOS either trigger onboarding via our
  onboarding API or proceed without a score, rather than blocking the
  LOS workflow

**Officer override note** (optional, if built)
- Input: ScoreComputation identifier, officer identifier, note text
- Output: confirmation; stored as an addendum, never mutates the score

## 4a. Outbound loan-case push — LOS / ULI / ONDC (**built 2026-09-15**)

The mirror image of §3–4: instead of waiting to be asked for a score, the bank
officer raises the MSME as a loan case downstream from Customer 360. Three
channels, three genuinely different payload shapes, one contract
(`ILoanCasePayloadBuilder`, registered by the `*PayloadBuilder` suffix — a fourth
channel is one class).

| Channel | Shape | Score travels as |
|---|---|---|
| **LOS** | `finrisklens.los.case.v1` — an appraisal case: applicant, decision, eligibility, six dimensions, the ratio table, bank/GST/ITR conduct, provenance | a decision block, because this is our own bank's system |
| **ULI** | OCEN-4.0 loan application | a **derived data packet** beside the source packets and consent artefacts, with **no decision block** — ULI's model is that the lender decides and we supply evidence |
| **ONDC** | beckn envelope, domain `ONDC:FIS12` — `context` + `message.order`, borrower as customer, loan as item | order tags |

**Surface** (bank session only, `[BankAdminAuthorize]`):

| Route | Does |
|---|---|
| `GET /loan-case/status?uan=` | latest push per channel + which channels have a live endpoint |
| `GET /loan-case/preview?uan=&channel=` | renders the exact payload in a new tab — pure read, writes nothing |
| `POST /loan-case/push` | builds, sends (or simulates), records, returns the receipt URL. `[ValidateAntiForgeryToken]` |
| `GET /loan-case/receipt/{id}` | the recorded payload, response and frozen score for one push |

**Preview-then-push, not one-click.** Raising a loan case is outward-facing and
hard to reverse, so the officer sees the payload before committing. Preview and
push run the same builder over the same inputs — the previewed bytes are the sent
bytes.

**Sent vs Simulated is honest, not faked.** `LoanCase:Endpoints:{LOS|ULI|ONDC}` is
blank by default, so a push is recorded as `Simulated` with the exact payload that
would have gone over the wire and a generated reference. Point a channel at a real
URL (optional `LoanCase:ApiKeys:{channel}` as Bearer) and the same code path does a
real POST, records `Sent`/`Failed` and extracts the downstream case id. **Pointing
this at IDBI's LOS is a config change, not a code change** — which is the claim
`09_FINALS_ENHANCEMENT_PLAN.md` makes about LOS integration.

Every attempt is stored append-only in `t_LoanCasePush` with the score, band,
eligibility and enterprise name **frozen at push time** — see `DB_SCRIPTS.md`. That
is the audit trail §3 asks for above (the `ScoreQueryLog` idea), for the outbound
direction.

## 5. Internal refresh trigger (Azure Function endpoints, not
public-facing, but worth documenting since Claude Code will build
these as HTTP/Queue/Timer triggers)

- Queue-triggered: fired when a `ConsentRecord` transitions to `Active`
  — kicks off the AA data pull for that Msme
- Timer-triggered: daily sweep for stale scores (see
  `02_CUSTOMER_FLOW.md` Step 10)
- HTTP-triggered: AA consent status callback receiver (the AA
  framework's webhook target)

## Cross-cutting conventions

- All external-facing responses (ULI DSP API, LOS API) should be
  versioned from day one (even if there's only ever a v1) — external
  consumers can't tolerate breaking changes the way internal callers
  can.
- Error responses should distinguish "not found" (valid GSTIN, no score
  yet) from "unauthorized" (caller's scope doesn't cover this MSME or
  this level of detail) from "invalid input" (malformed GSTIN) — these
  are different failure modes a lender's system needs to handle
  differently, don't collapse them into a generic 400.
- Any endpoint returning score data should include the
  `DataFreshnessAt` timestamp somewhere in the response — a consumer of
  the API needs the same freshness signal the human-facing card shows.
