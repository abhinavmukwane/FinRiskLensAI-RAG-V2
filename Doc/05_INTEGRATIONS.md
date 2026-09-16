# External Integrations

> **Implementation status (2026-07-02):** the connector interfaces below are
> not built yet, but the plumbing around them exists:
> - Real sample responses for Udyam, ITR (ITR-1 and ITR-3), and AA are in hand
>   (kept in the git-ignored `Json files/` folder — real personal data, never
>   commit); GST responses exist as schema files only so far. EPFO pending.
> - The **destination** for every pull is decided: each raw response is saved
>   into the MSME's Azure Blob folder (container `msme-data`, folder = UAN,
>   file names per `MsmeDataFiles` — see `08_ML_ENGINE.md` §2b). Connectors
>   should inject `IMsmeDataStore` and call `UploadAsync(uan, fileName, json)`
>   as each response arrives; analysis is then triggered by UAN alone.
> - Udyam responses are additionally cached relationally in `t_MsmeEnquiry`
>   (one row per UAN) so repeat lookups skip the API.
> - A teammate has started AA/HTTP client groundwork (`HttpClientHelper` in Core).

Each of these is a connector the `Services` layer calls out to. Build
each behind an interface (`IGstConnector`, `IItrConnector`,
`IAaConnector`, `IEpfoConnector`, `IUdyamConnector`) so the
synthetic-data generator and the eventual real sandbox/production call
can be swapped without touching the scoring engine or the customer
flow logic.

## Account Aggregator (AA) — FIP-AA-FIU framework

**What it is:** RBI's consent-based data-sharing framework. We act as an
FIU (Financial Information User). The MSME's bank is the FIP (Financial
Information Provider). An AA (a licensed intermediary) brokers consent
and data transfer between the two — we never talk to the bank directly.

**Flow shape:**
1. We submit a consent request to the AA, specifying FIP, data type
   (bank transactions), purpose, and time window.
2. The AA returns a consent handle; the actual approval happens on the
   MSME's AA app/interface, outside our system.
3. We receive a status callback (webhook) when the MSME approves or
   rejects.
4. Once approved, we submit a data request against the consent
   artifact; the AA returns the financial information (in the AA
   framework's standard bank-statement schema) either synchronously or
   via a second callback, depending on the AA's implementation.

**What we store:** the consent handle and lifecycle in `ConsentRecord`;
the raw statement payload in `DataSourceSnapshot` with `SourceType =
AaBankStatement`.

**For the hackathon:** the sandbox environment (opening July 22) should
provide either a sandbox AA or synthetic bank statement data in the AA
schema. Until then, build `IAaConnector` against a local fixture that
returns data in the same shape.

## GST System API

**What it is:** access to a business's filed GST returns — primarily
GSTR-1 (outward supplies / sales) and GSTR-3B (summary return with tax
liability), plus ITC (input tax credit) claim data.

**Flow shape:** typically accessed either directly via GSTN's API
(requires GSP — GST Suvidha Provider — empanelment) or via a licensed
GSP/account aggregator-adjacent intermediary. For the hackathon build,
treat this as a black-box HTTP call behind `IGstConnector` — the
important part is what we do with the response, not how the GSP
relationship is commercially structured (that's a partnership detail
outside the scope of the prototype).

**Authorization:** unlike AA data, GST data access is typically
authorized once at onboarding (the MSME grants access to their GSTIN's
return data as part of the onboarding terms), not through a separate
per-pull consent artifact. Model this as a boolean/timestamp on `Msme`
rather than a full `ConsentRecord`, unless you want to unify everything
under `ConsentRecord` for audit consistency — either is defensible, just
be consistent.

**What we store:** `DataSourceSnapshot` with `SourceType = Gst`,
containing the trailing 12-24 months of return summaries needed for the
Revenue Vitality and Compliance Quotient features.

## ITR (Income Tax Return) API

**What it is:** access to a business's filed Income Tax Returns — the
declared gross/net income figures and filing history across available
assessment years. This is the second income-side signal alongside GST,
and it's what lets Revenue Vitality work for MSMEs that don't clear the
GST registration threshold but still file ITR (a real segment of
credit-invisible micro-businesses).

**Flow shape:** typically accessed via an authorized ITR data-access
API/GSP-equivalent intermediary (e.g. through the income tax
department's e-filing data-sharing mechanisms, or a licensed
aggregator), similar in spirit to the GST access pattern. For the
hackathon build, treat this as a black-box HTTP call behind
`IItrConnector` — the commercial/empanelment relationship is out of
scope for the prototype.

**Authorization:** same as GST — authorized once at onboarding as part
of the terms the MSME accepts (see `Msme` field mapping in
`01_DOMAIN_MODEL.md`), not a separate per-pull consent artifact. Keep
this consistent with however GST's authorization was modeled (boolean/
timestamp on `Msme`, or unified under `ConsentRecord`).

**What we store:** `DataSourceSnapshot` with `SourceType = Itr`,
containing declared income by assessment year and filing dates, feeding
Revenue Vitality (income trend, GST-vs-ITR cross-check) and Compliance
Quotient (filing regularity) per `03_SCORING_ENGINE.md`.

**Missing-data note:** not every MSME will have ITR history — very
young businesses may not have filed yet. Treat absence the same way as
other missing sources: don't zero the dimension, redistribute weight
across whatever income signal is available (GST, or bank-credit-derived
revenue proxies from AA if GST is also thin), and disclose the
reduced-input basis on the Financial Health Card.

## EPFO REST API

**What it is:** access to a registered establishment's PF contribution
history — used as a proxy for payroll stability and headcount trend.

**Note:** many MSMEs, especially very small ones, will not have EPFO
registration at all (it's mandatory only above a headcount threshold).
This is expected, not an error — `IEpfoConnector` should return a clear
"not registered" result distinct from a fetch failure, and the scoring
engine should treat "not registered" as a valid input to Business
Stability (small, informal-sector businesses aren't penalized for being
small), not as missing data requiring weight redistribution.

**What we store:** `DataSourceSnapshot` with `SourceType = Epfo`.

## Udyam Registry — primary onboarding source

**What it is:** the government MSME registration database. This is now
the *entry point* for onboarding (`02_CUSTOMER_FLOW.md` Step 1), not a
secondary lookup — the MSME provides only their Udyam Registration
Number, and the API response supplies enough verified identity data to
populate the `Msme` record without further manual entry.

**Flow shape:** a lookup by Udyam Registration Number, no consent flow
needed for the lookup itself (it's public registry data) — though
displaying the result and asking the MSME to confirm/Register is itself
a consent moment, captured per Step 2.

**Response contract:**

| Field | Type | Notes |
|---|---|---|
| `udyamRegistrationNumber` | string | echo of the input key |
| `enterpriseName` | string | maps to `Msme.EnterpriseName` |
| `ownerName` | string | maps to `Msme.OwnerName` |
| `panNumber` | string | maps to `Msme.PanNumber` |
| `gstinStatus` | string | linked GSTIN info; used to pre-fill Step 4's GSTIN prompt |
| `mobileNumber` | string | maps to `Msme.MobileNumber`; secondary contact, not the OTP channel |
| `emailId` | string | maps to `Msme.EmailId`; **this is the registration/login OTP channel** |
| `enterpriseType` | string | maps to `Msme.EnterpriseType` |
| `majorActivity` | string | maps to `Msme.MajorActivity`; candidate input for `SectorCode` |
| `officialAddress` | string | maps to `Msme.OfficialAddress` |
| `dateOfIncorporation` | string | maps to `Msme.DateOfIncorporation`; preferred source for `BusinessVintageMonths` |
| `dateOfCommencement` | string | maps to `Msme.DateOfCommencement` |
| `organizationType` | string | maps to `Msme.OrganizationType` |
| `bankName` | string | maps to `Msme.BankName` |
| `bankAccountNumber` | string | maps to `Msme.BankAccountNumber`; treat as sensitive |
| `ifscCode` | string | maps to `Msme.IfscCode` |

**What we store:** the full response is written to
`DataSourceSnapshot` with `SourceType = Udyam` (as before, for audit/
explainability purposes), and the mapped fields above are written
directly onto `Msme` at Step 2 registration — this is the one source
that populates the entity itself rather than only feeding the scoring
pipeline, since it's identity data, not financial history.

**Sensitive fields:** `bankAccountNumber` and `panNumber` should be
handled with the same care as any PII/financial identifier — avoid
logging them, mask in any UI display beyond what's needed, and don't
include them in the ULI/OCEN external API responses described in
`04_API_CONTRACTS.md` (those responses are score-focused; there's no
reason a third-party lender query needs bank account details).

## ULI (Unified Lending Interface), OCEN and ONDC

These aren't inbound data sources — they're the outbound side described
in `04_API_CONTRACTS.md` §3-4a. Documented here only to note the
distinction clearly: AA/GST/ITR/EPFO/Udyam are things we *call* for
data; ULI/OCEN/ONDC are lending rails we *hand a case to*, and whose
standards our own inbound API should conform to so other lenders can
call *us*.

**Built 2026-09-15 — the hand-off direction.** The bank portal can raise a
scored MSME as a loan case in **LOS**, **ULI** (OCEN-4.0 loan application)
or **ONDC** (beckn, domain `ONDC:FIS12`), each with its own payload
builder; see `04_API_CONTRACTS.md` §4a. Note what this deliberately is
*not*: it is not a generic "ULI connector" with its own client library,
credential store and retry machinery. It is one POST per channel, with
the envelope shaped to the standard and the endpoint read from config —
blank endpoint means the payload is built and recorded but not sent.

**Still unbuilt:** the inbound DSP-facing endpoint (a lender queries *us*
by GSTIN), which is gap #2 in `07_SETUP_GAPS.md` and item 2.2 in
`09_FINALS_ENHANCEMENT_PLAN.md`.

## Connector interface shape (suggested)

Keep each connector interface narrow and focused on one responsibility
— fetch the data, return it in a normalized shape, don't do feature
engineering inside the connector. `IUdyamConnector` is the one called
first and synchronously (the MSME is waiting on the confirmation
screen in `02_CUSTOMER_FLOW.md` Step 1); the other four are called
later, during Step 4, and can tolerate the async/partial-completeness
handling described there:

```
Task<UdyamLookupResult> FetchAsync(string udyamRegistrationNumber, CancellationToken ct);
Task<GstDataResult> FetchAsync(string gstin, DateRange window, CancellationToken ct);
Task<ItrDataResult> FetchAsync(string panNumber, DateRange window, CancellationToken ct);
```

Where each result type carries either the successful payload or a
typed failure reason (not found, consent required, upstream timeout,
upstream error) — the calling service needs to distinguish these to
implement the partial-completeness handling described in
`02_CUSTOMER_FLOW.md` Step 4.

## Synthetic data generator (build this first)

Before any real connector, build a generator that implements all five
connector interfaces against fixture data. Since every MSME now enters
through Udyam lookup, every fixture profile needs a valid Udyam
response — vary the *other* four sources to cover:

- A clean NTB MSME: full Udyam, GST, ITR, AA, EPFO data, healthy across
  all dimensions
- A clean NTC MSME: valid Udyam, strong GST and ITR history, no
  bureau record, AA consent granted
- A thin-file MSME: valid Udyam with a linked GSTIN, but AA consent
  not granted and no EPFO (below headcount threshold) — this is now
  the right way to model "thin file", since Udyam itself is no longer
  optional
- A below-GST-threshold MSME: valid Udyam, ITR filed but no GST
  registration — exercises Revenue Vitality and Compliance Quotient
  falling back to ITR alone
- An anomalous MSME: GST-declared turnover, ITR-declared income, and
  AA bank credits disagree significantly with each other (to exercise
  the three-way fraud/anomaly signal)
- A declining MSME: negative revenue trend over the trailing 6 months,
  to exercise the trend overlay and produce a realistic "at risk" band

This generator is what the scoring engine and UI get built and
demoed against until sandbox access opens July 22 — treat it as a
first-class part of the build, not a throwaway script.
