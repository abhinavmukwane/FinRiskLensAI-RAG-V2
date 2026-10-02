# 10 — RAG Architecture

How retrieval-augmented generation is designed, built and used in FinRiskLens.

> **Thesis:** RAG belongs on **unstructured knowledge** — policy, regulation, fraud
> typologies, past cases, document-format exemplars. It does **not** belong on the
> case's own structured data, which is queried with tools over SQL.
>
> This is the same split the engine already makes elsewhere: **Gemini reads and
> reasons, code computes and verifies.** Retrieval follows the same discipline —
> retrieve text, cite it, then verify the citation mechanically.

---

## Table of Contents
1. [Why RAG here at all](#1-why-rag-here-at-all)
2. [Where RAG is used — and where it is deliberately not](#2-where-rag-is-used--and-where-it-is-deliberately-not)
3. [The five corpora](#3-the-five-corpora)
4. [Architecture](#4-architecture)
5. [Ingestion pipeline](#5-ingestion-pipeline)
6. [Chunking strategy](#6-chunking-strategy)
7. [Embeddings](#7-embeddings)
8. [Vector storage](#8-vector-storage)
9. [Retrieval: hybrid search](#9-retrieval-hybrid-search)
10. [Reranking](#10-reranking)
11. [Generation and citation contract](#11-generation-and-citation-contract)
12. [Grounding verification for RAG](#12-grounding-verification-for-rag)
13. [Agentic RAG: the Investigator's knowledge tool](#13-agentic-rag-the-investigators-knowledge-tool)
14. [Data model](#14-data-model)
15. [Code layout](#15-code-layout)
16. [Configuration](#16-configuration)
17. [Development plan](#17-development-plan)
18. [Evaluation](#18-evaluation)
19. [Cost and latency](#19-cost-and-latency)
20. [Failure modes and mitigations](#20-failure-modes-and-mitigations)
21. [Safety, privacy and tenancy](#21-safety-privacy-and-tenancy)
22. [Scaling path](#22-scaling-path)

---

## 1. Why RAG here at all

The fraud engine produces **findings**. A finding tells an underwriter *what* is
wrong and *where the evidence is*. It does not, on its own, tell them:

- **"Is this against our policy, and which clause?"** — every lender's credit policy
  treats gambling exposure, undeclared EMIs and cash-intensity differently.
- **"Have we seen this pattern before?"** — a modus operandi confirmed three months
  ago is institutional memory that currently lives in people's heads.
- **"What am I required to do?"** — RBI master directions, internal SOPs, and the
  escalation path for a suspected fraud ring.
- **"What does a genuine document from this issuer look like?"** — the extractor
  performs better when shown a verified exemplar of the same layout family.

None of that is in the case data. All of it is **unstructured text that changes
without a code deploy**. That is precisely the problem retrieval solves, and the
reason a fine-tune or a hard-coded prompt would be the wrong tool: policy documents
are revised, typologies are added weekly, and each lender brings their own.

**Without RAG,** the model either invents policy or you hard-code it. Both are
unacceptable in a lending context.

---

## 2. Where RAG is used — and where it is deliberately not

| Question | Mechanism | Why |
|---|---|---|
| "Show all transfers to the salary payer" | **Function calling → SQL** | Exact, complete, cheap. Vector search over transaction rows would be lossy and slower |
| "Does the balance carry over on page 4?" | **Deterministic SQL** | Arithmetic is never delegated to a model |
| "Is this template like others we've seen?" | **Vector similarity** (no generation) | Retrieval, but for clustering — not RAG |
| **"Does our policy allow this exposure?"** | **RAG** | Unstructured, lender-specific, versioned |
| **"What is the SOP to verify a salary slip?"** | **RAG** | Procedural text |
| **"Have we confirmed this MO before?"** | **RAG over case memory** | Past narratives are free text |
| **"What does RBI require when we suspect a ring?"** | **RAG over regulation** | Long, citable, must not be paraphrased from memory |
| **"What does a genuine HDFC-style statement look like?"** | **RAG for few-shot extraction** | Retrieved exemplar improves structured output |

> **The rule, stated for reviewers:** if the answer exists as a row in our database,
> we query it. If it exists as a paragraph someone wrote, we retrieve it. We never
> use retrieval to answer a question that SQL can answer exactly.

---

## 3. The five corpora

Each corpus is independently versioned, filtered and cited. A query always names
the corpus or corpora it may draw from — retrieval never runs unscoped.

| # | Corpus | Contents | Source | Refresh |
|---|---|---|---|---|
| **C1** | **Credit policy & SOP** | Underwriting policy, exposure limits, verification SOPs, escalation matrix | Lender-supplied (synthetic for demo) | On upload |
| **C2** | **Regulatory** | RBI master directions (KYC, fraud classification, digital lending), DPDP obligations | Public documents | Quarterly |
| **C3** | **Fraud typology** | Modus operandi catalogue, red-flag descriptions, document-forgery techniques | Curated | Weekly |
| **C4** | **Case memory** | Confirmed findings, ring narratives, investigator confirm/dismiss notes, resolution outcomes | Generated by the product itself | Continuous |
| **C5** | **Document exemplars** | Layout descriptions of verified-genuine documents per claimed issuer | Generated at extraction, promoted on verification | Continuous |

**C4 is the compounding asset.** Every case an investigator confirms or dismisses
writes back into the corpus, so the system's institutional memory grows with use.
That is the part a competitor cannot copy — it is earned, not built.

---

## 4. Architecture

```
                         ┌─────────────────────────────────────────┐
  Policy PDFs, RBI docs  │  INGESTION (offline / on upload)        │
  typology notes ───────▶│  parse → chunk → embed → index          │
  confirmed cases ──────▶│  (Gemini Flash for parse, Vertex embed) │
                         └───────────────────┬─────────────────────┘
                                             ▼
                              ┌──────────────────────────────┐
                              │ Cloud SQL for PostgreSQL     │
                              │  knowledge_docs              │
                              │  knowledge_chunks (+GIN ts)  │
                              │  chunk_embeddings (pgvector) │
                              └──────────────┬───────────────┘
                                             │
   Underwriter question                      │
   Investigator agent  ──── search_knowledge ─┤
   Extractor (exemplar) ──────────────────────┤
                                             ▼
                    ┌────────────────────────────────────────┐
                    │ RETRIEVAL                              │
                    │  ① embed query (RETRIEVAL_QUERY)       │
                    │  ② vector top-k  ┐                     │
                    │  ③ keyword top-k ┘→ RRF fusion         │
                    │  ④ metadata filter (corpus, date)      │
                    │  ⑤ rerank top-20 → top-5 (Flash)       │
                    └───────────────┬────────────────────────┘
                                    ▼
                    ┌────────────────────────────────────────┐
                    │ GENERATION (Gemini Pro)                │
                    │  answer MUST cite [chunkId]            │
                    │  refuse if best score < threshold      │
                    └───────────────┬────────────────────────┘
                                    ▼
                    ┌────────────────────────────────────────┐
                    │ GROUNDING VERIFIER (C#)                │
                    │  cited chunks exist? quotes substring? │
                    │  → answer kept / citation stripped     │
                    │  → RagQueryLog                         │
                    └────────────────────────────────────────┘
```

---

## 5. Ingestion pipeline

Implemented as `Rag/KnowledgeIngestor.cs`, triggered on document upload (C1–C3) or
on finding confirmation (C4–C5).

**Step 1 — Acquire.** Policy and regulatory PDFs land in the GCS knowledge bucket
(`gs://<bucket>/knowledge/{corpus}/{docId}.pdf`), separate from case documents so
lifecycle and access rules differ.

**Step 2 — Parse.** Gemini Flash reads the PDF natively and returns structured
markdown preserving heading hierarchy, tables and clause numbering. We keep
headings because they become chunk metadata and make citations human-meaningful
("Credit Policy §4.2.1" beats "chunk 318").

**Step 3 — Normalise.** Strip page furniture (headers, footers, page numbers),
collapse whitespace, keep clause numbers.

**Step 4 — Chunk.** See [§6](#6-chunking-strategy).

**Step 5 — Embed.** Batch chunks through the Vertex AI embedding model with task
type `RETRIEVAL_DOCUMENT`. See [§7](#7-embeddings).

**Step 6 — Index.** Write `knowledge_chunks` + `chunk_embeddings`. Both indexes
maintain themselves: the `ts` generated column feeds the GIN index for the keyword
arm, and the HNSW index covers the new vector on insert. There is no post-ingest
rebuild step to forget.

**Idempotency.** Every source document carries a SHA-256. Re-ingesting an identical
file is a no-op. A changed file supersedes the previous version: old chunks are
marked `IsCurrent = 0` rather than deleted, so a citation in an old case still
resolves to the text that was actually shown at the time. **Citations must never
rot** — an audit six months later has to reproduce what the underwriter saw.

---

## 6. Chunking strategy

Chunking is where most RAG systems quietly fail. Different corpora need different
treatment, so the chunker is strategy-per-corpus rather than one global setting.

| Corpus | Strategy | Size | Overlap | Rationale |
|---|---|---|---|---|
| **C1 Policy / SOP** | Heading-aware recursive split; never split mid-clause | 400–700 tokens | 80 tokens | A policy clause is the unit of meaning. Splitting "…is permitted unless" from "…the applicant has prior default" inverts the rule |
| **C2 Regulatory** | Section-aware, one chunk per numbered para where possible | 300–800 | 100 | Regulations are cited by paragraph; align chunks to how they are referenced |
| **C3 Typology** | One chunk per typology entry | whole entry | 0 | Entries are already atomic |
| **C4 Case memory** | One chunk per finding or ring narrative | whole | 0 | Natural unit; keeps the link back to FindingId/RingId |
| **C5 Exemplars** | One chunk per layout description | whole | 0 | Already normalised and short |

**Contextual prefixing.** Every chunk is stored with a short generated header
describing where it sits — `"Credit Policy v3 › §4 Exposure Norms › §4.2 Gambling
and speculative spend"` — prepended before embedding. This measurably improves
retrieval for queries that use different vocabulary from the clause body, and it
gives the UI a breadcrumb to display beside the citation.

**Tables** are kept whole and serialised to markdown; a split exposure-limits table
is worse than useless because the model reads the wrong row.

---

## 7. Embeddings

- **Model:** Vertex AI text embedding model, pinned in config as
  `Gemini:EmbeddingModel`. Never hard-code the model name in C# — the model is
  config, because re-embedding the corpus is a deliberate operation.
- **Task types matter.** Documents are embedded with `RETRIEVAL_DOCUMENT`, queries
  with `RETRIEVAL_QUERY`. Using the same task type for both measurably degrades
  recall; this is the single most common implementation mistake.
- **Dimensionality is 768**, declared on the column as `vector(768)` and held in
  `ChunkEmbedding.VectorDimensions`. Request that output size from the embedding model
  explicitly: pgvector's HNSW index supports at most **2,000 dimensions** on the `vector`
  type, so a model whose native output is larger (3,072, for example) must be truncated
  to fit, or it cannot be indexed at all. Declared as `vector(n)`. The dimension is also recorded on the row, so a model change is
  detectable rather than silently corrupting distances — and changing `n` is a
  migration, which is the right amount of friction for something that invalidates
  the index.
- **Batching:** chunks are embedded in batches (config `Rag:EmbedBatchSize`, default
  32) with retry and exponential backoff.
- **Versioning:** `ChunkEmbeddings.ModelId` records which model produced the vector.
  A mixed-model index is a correctness bug, so retrieval filters on the current
  model id and a background job re-embeds stragglers.

---

## 8. Vector storage

**Cloud SQL for PostgreSQL with the `pgvector` extension.** One database holds the
application data, the knowledge chunks and their vectors — no separate vector
service, and a chunk and its embedding are written in the same transaction.

```sql
CREATE EXTENSION IF NOT EXISTS vector;

ALTER TABLE chunk_embeddings
  ADD COLUMN embedding vector(768);        -- dimension from Gemini:EmbeddingModel

-- HNSW: fast approximate search, built once, cheap to query.
CREATE INDEX idx_chunk_embedding_hnsw
  ON chunk_embeddings USING hnsw (embedding vector_cosine_ops)
  WITH (m = 16, ef_construction = 64);
```

Retrieval is then a single query — the distance operator, the metadata filter and
the ordering all happen in the database:

```sql
SELECT c.chunk_id, c.breadcrumb, c.chunk_text,
       1 - (e.embedding <=> @queryVec) AS score      -- <=> is cosine distance
FROM   chunk_embeddings e
JOIN   knowledge_chunks c ON c.chunk_id = e.chunk_id
JOIN   knowledge_docs   d ON d.knowledge_doc_id = c.knowledge_doc_id
WHERE  d.corpus = ANY(@corpora)
  AND  c.is_current
  AND  e.model_id = @modelId
  AND  (d.lender_id IS NULL OR d.lender_id = @lenderId)
ORDER  BY e.embedding <=> @queryVec
LIMIT  @denseTopK;
```

**Why this and not a vector database.** The corpus is thousands of chunks, not
millions. pgvector in the database we already run is exact enough, fast enough, and
adds no service to deploy, secure or pay for. It also keeps chunk metadata and
vector in one place, so the corpus/`is_current`/model filters run *before* the
distance scan rather than as a post-filter — which is where standalone vector stores
get awkward.

**Store normalised vectors** so cosine distance and inner product agree, and record
`model_id` + `dimensions` on the row. A mixed-model index is a silent correctness
bug; retrieval filters on the current model id and a background job re-embeds
stragglers.

**Index choice.** HNSW over IVFFlat: it needs no training step, tolerates a corpus
that grows continuously (C4 case memory does), and its recall/latency trade-off is
tuned at query time with `hnsw.ef_search` rather than at build time.

> **ponytail:** pgvector + HNSW in the primary database. No separate vector store
> until the corpus passes roughly a million chunks or p95 retrieval exceeds 150 ms —
> and `IVectorIndex` exists so that swap stays one class.

---

## 9. Retrieval: hybrid search

Pure vector search fails on exactly the queries this domain generates, because
underwriters use **exact terms**: "FOIR", "44AB", "CERSAI", "§4.2". Embeddings
blur precisely those tokens. So retrieval runs two arms and fuses them.

**Arm A — dense.** Embed the query (`RETRIEVAL_QUERY`), then the pgvector query in
[§8](#8-vector-storage), top 20.

**Arm B — sparse.** PostgreSQL full-text search — `tsvector` / `tsquery` with
`ts_rank_cd`, top 20. Full-text is part of the core engine, so there is no optional
feature to enable and no dependency on the managed instance's configuration:

```sql
ALTER TABLE knowledge_chunks
  ADD COLUMN ts tsvector
  GENERATED ALWAYS AS (to_tsvector('english', coalesce(breadcrumb,'') || ' ' || chunk_text)) STORED;

CREATE INDEX idx_chunk_ts ON knowledge_chunks USING GIN (ts);
```

```sql
SELECT c.chunk_id, ts_rank_cd(c.ts, q) AS score
FROM   knowledge_chunks c
JOIN   knowledge_docs d ON d.knowledge_doc_id = c.knowledge_doc_id,
       websearch_to_tsquery('english', @question) q
WHERE  c.ts @@ q AND c.is_current AND d.corpus = ANY(@corpora)
ORDER  BY score DESC
LIMIT  @sparseTopK;
```

The generated column means the index maintains itself on insert and update — nothing
to rebuild after ingestion. `websearch_to_tsquery` accepts quoted phrases and `-`
exclusions, which is what an underwriter types without being taught a syntax.

**Fusion — Reciprocal Rank Fusion.** Simple, parameter-light, and it does not
require the two arms' scores to be comparable:

```
score(chunk) = Σ over arms  1 / (k + rank_in_arm)      // k = 60, config: Rag:RrfK
```

**Filters** applied before both arms: corpus set, `IsCurrent = 1`, effective-date
window, and lender id for multi-tenant deployments.

**Query expansion.** For short or abbreviation-heavy questions, Gemini Flash
generates two paraphrases; all three are retrieved and fused. Off by default
(`Rag:ExpandQueries`) because it triples embedding calls for modest gain — turn it
on for the policy corpus where vocabulary mismatch is worst.

---

## 10. Reranking

The fused top-20 goes to Gemini Flash with the original question and each candidate,
scored 0–10 for relevance; top 5 survive into the prompt.

Why bother: embedding similarity answers "is this about the same topic", while the
reranker answers "does this passage actually answer the question". For policy
lookups the difference is large — five clauses may all discuss gambling exposure,
but only one states the limit.

Reranking is skipped when the fused top result clears `Rag:SkipRerankAbove` (default
0.85 normalised), which covers the common case cheaply.

---

## 11. Generation and citation contract

The answering model receives: the question, the retrieved chunks each labelled with
its `chunkId` and breadcrumb, and — where relevant — the case context (finding type,
counterparty class) so the answer can be specific.

**System prompt, in outline:**

```
Answer ONLY from the provided knowledge passages.
Every factual sentence must end with a citation [chunkId].
Quote the exact phrase you relied on in quotedText for each citation.
If the passages do not answer the question, say so plainly and suggest what
document would be needed. Do not answer from general knowledge.
Do not give a credit decision. Describe what policy says and what to verify.
```

**Structured output** (enforced by response schema):

```json
{
  "answer": "Gambling exposure above 5% of monthly credits requires senior credit approval [K-2291]. The SOP is to request three months of additional statements before escalation [K-3105].",
  "citations": [
    { "chunkId": "K-2291", "quotedText": "exposure to gambling or speculative merchants exceeding 5% of monthly credits" },
    { "chunkId": "K-3105", "quotedText": "obtain three additional months of statements prior to escalation" }
  ],
  "confidence": "HIGH",
  "unanswered": false
}
```

**Refusal is a first-class outcome.** If the best fused score is below
`Rag:MinRelevance` (default 0.45), we do not call the generator at all — the UI
says *"No policy passage found for this question"* and offers to search a different
corpus. A confident wrong answer about lending policy is worse than no answer.

---

## 12. Grounding verification for RAG

The same verifier discipline applied to fraud findings applies to RAG answers.
`Rag/RagGroundingVerifier.cs` runs in C# before the answer reaches the UI:

| Check | Rule | On failure |
|---|---|---|
| Citation exists | Every `chunkId` is a real chunk that was in *this* retrieval set | Strip the sentence, log |
| Quote is real | `quotedText` is a case-insensitive, whitespace-normalised substring of the chunk | Strip the citation, mark sentence unsupported |
| Every claim cited | No factual sentence without a citation | Flag answer as `PARTIALLY_GROUNDED` in the UI |
| No decision language | Answer contains no "approve"/"reject"/"sanction" | Rewrite request, or block |
| Corpus scope respected | Cited chunks belong to the corpora the query was scoped to | Block, log as a scope violation |

Every query and its verification outcome is written to `RagQueryLog`. The Insights
dashboard shows **"n RAG answers had unsupported claims removed"** — the same trust
signal as the discarded-findings counter, and for the same reason: showing the
verifier working is more persuasive than claiming the model is accurate.

---

## 13. Agentic RAG: the Investigator's knowledge tool

RAG is not only a chat feature. The Investigator agent gets retrieval as **one more
tool** alongside its SQL tools:

```
search_knowledge(query: string, corpus: "POLICY"|"REGULATION"|"TYPOLOGY"|"CASE_MEMORY")
  → [{ chunkId, breadcrumb, text, score }]
```

This is what makes the investigation genuinely agentic rather than a fixed pipeline.
A realistic tool sequence:

1. `get_counterparty_summary(doc)` → sees ₹84,000 of debits to betting merchants
2. `search_knowledge("gambling exposure threshold", POLICY)` → finds the 5% clause
3. `get_rows(doc, filter: credits)` → computes the actual ratio is 7.2%
4. Emits a finding whose **recommended action quotes the lender's own policy**, with
   both the transaction rows and the policy chunk as evidence

The finding that comes out the other end is not "gambling detected". It is *"7.2% of
monthly credits went to betting merchants, which exceeds your 5% threshold requiring
senior credit approval [Credit Policy §4.2]"*. Same detection, an order of magnitude
more useful to the person reading it.

**Case memory in the loop.** `search_knowledge(..., CASE_MEMORY)` lets the agent
surface *"this MO matches Ring #7, confirmed 14 Aug"* — retrieval doing something no
amount of prompt engineering can.

**Scoping is enforced server-side.** The tool cannot read another applicant's case
data; C4 chunks are written de-identified (pattern descriptions, never names,
account numbers or applicant refs).

---

## 14. Data model

PostgreSQL. Snake-case table and column names are the Npgsql/EF Core default and are
kept rather than fought.

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE knowledge_docs (
  knowledge_doc_id  SERIAL PRIMARY KEY,
  corpus            VARCHAR(20)  NOT NULL,     -- POLICY, REGULATION, TYPOLOGY, CASE_MEMORY, EXEMPLAR
  title             TEXT         NOT NULL,
  source_uri        TEXT         NULL,         -- gs://... or NULL for generated
  sha256            BYTEA        NOT NULL,
  version           VARCHAR(40)  NULL,         -- "Credit Policy v3"
  effective_from    DATE NULL,
  effective_to      DATE NULL,
  lender_id         INT  NULL,                 -- NULL = applies to all tenants
  is_current        BOOLEAN NOT NULL DEFAULT TRUE,
  ingested_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_kdoc_corpus ON knowledge_docs (corpus, is_current);

CREATE TABLE knowledge_chunks (
  chunk_id          BIGSERIAL PRIMARY KEY,
  knowledge_doc_id  INT NOT NULL REFERENCES knowledge_docs,
  ordinal           INT NOT NULL,
  breadcrumb        TEXT NULL,                 -- "Credit Policy v3 > S4 Exposure > S4.2"
  chunk_text        TEXT NOT NULL,
  token_count       INT NOT NULL,
  source_finding_id INT NULL,                  -- C4 provenance
  source_ring_id    INT NULL,
  is_current        BOOLEAN NOT NULL DEFAULT TRUE,
  ts                tsvector GENERATED ALWAYS AS
                      (to_tsvector('english', coalesce(breadcrumb,'') || ' ' || chunk_text)) STORED
);
CREATE INDEX idx_kchunk_doc ON knowledge_chunks (knowledge_doc_id, ordinal);
CREATE INDEX idx_kchunk_ts  ON knowledge_chunks USING GIN (ts);   -- sparse arm

CREATE TABLE chunk_embeddings (
  chunk_embedding_id  BIGSERIAL PRIMARY KEY,
  knowledge_chunk_id  BIGINT NOT NULL REFERENCES knowledge_chunks ON DELETE CASCADE,
  model_id            VARCHAR(100) NOT NULL,
  dimensions          INT NOT NULL,
  embedding           vector(768)  NOT NULL,   -- normalised; dimension per model
  created_at          TIMESTAMP NOT NULL
);
-- One vector per chunk PER MODEL — not per chunk. A re-embed under a new model writes
-- alongside the old vectors and retrieval cuts over on model_id (§22), so there is no
-- window where search returns nothing.
CREATE UNIQUE INDEX ux_chunk_embeddings_chunk_model ON chunk_embeddings (knowledge_chunk_id, model_id);
CREATE INDEX idx_chunk_embedding_hnsw
  ON chunk_embeddings USING hnsw (embedding vector_cosine_ops)
  WITH (m = 16, ef_construction = 64);        -- dense arm

CREATE TABLE rag_query_log (
  rag_query_id    BIGSERIAL PRIMARY KEY,
  application_id  INT NULL,
  actor           VARCHAR(100) NOT NULL,       -- user id, or 'INVESTIGATOR_AGENT'
  question        TEXT NOT NULL,
  corpora         VARCHAR(100) NOT NULL,
  retrieved_ids   JSONB NOT NULL,              -- chunk ids + scores, queryable
  answer          TEXT NULL,
  grounded        VARCHAR(20) NOT NULL,        -- GROUNDED, PARTIAL, REFUSED, BLOCKED
  stripped_claims INT NOT NULL DEFAULT 0,
  latency_ms      INT NOT NULL,
  at              TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

> **As built (2026-09-30).** The baseline migration follows this design with two
> refinements. Keys follow the codebase convention — each table has its own
> `<entity>_id` — and `chunk_embeddings` is unique on `(knowledge_chunk_id, model_id)`
> rather than keyed on the chunk: an earlier draft of this section made `chunk_id` the
> primary key, which allowed one vector per chunk and contradicted the re-embed path in
> §22. Audit columns (`created_at`, `updated_at`, `created_by`, `updated_by`) come from
> `AuditableEntity` and replace the separate `ingested_at` / `embedded_at` / `at` columns.
> Timestamps are `timestamp without time zone` (Npgsql legacy mode, UTC by convention).
> Applied and verified on Neon — see `DB_SCRIPTS.md`, 2026-09-30.

Two PostgreSQL features earn their place here. **`retrieved_ids` is `JSONB`**, not a
string, so evaluation queries can aggregate over retrieval results directly. And the
**`ts` generated column** keeps the full-text index correct on every insert and
update with no ingestion step to remember.

`vector(768)` is written explicitly for clarity — set it from the pinned embedding
model's dimension. Changing it is a migration, which is the correct amount of
friction for a change that invalidates the whole index.

Chunks are **never hard-deleted**. Superseding sets `IsCurrent = 0`, so a citation
recorded against a case last quarter still resolves to the text the underwriter
actually read.

---

## 15. Code layout

```
FinRiskLensAI.Fraud/Rag/
├─ IKnowledgeStore.cs           corpus CRUD, chunk fetch
├─ IVectorIndex.cs              search(vector, filter, k)  ← swap point for Vector Search
├─ PgVectorIndex.cs             single pgvector query, filters pushed to SQL
├─ KnowledgeIngestor.cs         acquire → parse → chunk → embed → index
├─ MarkdownChunker.cs          all five corpora — options per corpus (built, R2)
├─ EmbeddingClient.cs           Vertex AI, task types, batching, retry
├─ HybridRetriever.cs           dense + sparse + RRF + filters
├─ Reranker.cs                  Gemini Flash relevance scoring
├─ RagAnswerer.cs               prompt, structured output, refusal
├─ RagGroundingVerifier.cs      citation + quote verification
└─ KnowledgeSearchTool.cs       the agent-facing tool wrapper
```

**As built (R2):** one chunker, not three classes. Heading-aware packing and "one
whole chunk per entry" are the same algorithm with different options — atomic is a
budget of `int.MaxValue`, no overlap, and only `#`/`##` headings splitting entries — so
`MarkdownChunker.For(corpus)` picks the options and there is no interface to implement.
Tokens are estimated at four characters each; swap in the embedding model's count if
budgets ever need to be exact.

There is deliberately no `VectorMath.cs` — with pgvector the distance arithmetic
lives in the database, so there is none left to write in C#.

Everything ending in `Service` auto-registers via the existing Autofac convention;
the rest is registered explicitly in a small `RagModule`.

---

## 16. Configuration

```jsonc
"Rag": {
  "EmbedBatchSize": 32,
  "DenseTopK": 20,
  "SparseTopK": 20,
  "RrfK": 60,
  "FinalTopK": 5,
  "MinRelevance": 0.45,          // below this we refuse rather than generate
  "SkipRerankAbove": 0.85,
  "ExpandQueries": false,
  "MaxContextTokens": 8000,
  "HnswEfSearch": 64,            // pgvector recall/latency knob, tuned at query time
  "Corpora": {
    "POLICY":      { "Enabled": true },
    "REGULATION":  { "Enabled": true },
    "TYPOLOGY":    { "Enabled": true },
    "CASE_MEMORY": { "Enabled": true,  "MinCasesBeforeUse": 10 },
    "EXEMPLAR":    { "Enabled": true }
  }
},
"Gemini": {
  "Location": "global",
  "ProModel": "<pinned at build time>",
  "FlashModel": "<pinned at build time>",
  "EmbeddingModel": "<pinned at build time>"
}
```

Every threshold is config, not code. These are the calibration knobs, and they will
be tuned against the eval set in [§18](#18-evaluation) rather than guessed once.

---

## 17. Development plan

Built in this order because each stage is independently testable and the early
stages have no model dependency at all.

| Stage | Deliverable | Done when |
|---|---|---|
| **R1** | Schema + `KnowledgeDocs`/`Chunks`/`Embeddings` tables, migration logged in `DB_SCRIPTS.md` | Migration applies clean |
| **R2** | Chunkers + unit tests, no embeddings yet | A policy PDF chunks with clause boundaries intact; tests assert no mid-clause split |
| **R3** | `EmbeddingClient` + batch ingest of C3 typology corpus (smallest, cleanest) | Vectors land, dimensions recorded |
| **R4** | `PgVectorIndex` + HNSW index + dense retrieval, CLI harness | A known question returns its known chunk in top 3 |
| **R5** | Sparse arm + RRF fusion | Abbreviation queries ("FOIR", "44AB") that failed dense-only now succeed |
| **R6** | `RagAnswerer` + citation schema + refusal path | Answers cite; out-of-corpus questions refuse |
| **R7** | `RagGroundingVerifier` + `RagQueryLog` | A deliberately fabricated citation is stripped in test |
| **R8** | `KnowledgeSearchTool` wired into the Investigator | Agent cites policy in a recommended action |
| **R9** | C4 case-memory write-back on finding confirmation | Confirming a finding creates a de-identified chunk |
| **R10** | Reranker, query expansion, tuning against the eval set | Eval metrics in [§18](#18-evaluation) meet target |

**R1–R7 have no UI dependency** and can be developed against a CLI harness while the
case pipeline is built in parallel.

---

## 18. Evaluation

RAG that is not measured is RAG that is guessed. A golden set is built *before*
tuning, not after.

**Golden set:** 60 question/answer pairs — 25 policy, 15 regulatory, 10 typology,
10 case-memory — each with the chunk id(s) that should be retrieved, written by
hand from the corpus.

**Retrieval metrics** (no model call needed, so this runs in CI):

| Metric | Target |
|---|---|
| Recall@5 | ≥ 0.90 |
| MRR@10 | ≥ 0.75 |
| Refusal precision (correctly refuses unanswerable) | ≥ 0.95 |

**Answer metrics:**

| Metric | How | Target |
|---|---|---|
| Groundedness | % of sentences surviving the verifier | ≥ 0.95 |
| Citation validity | % of citations whose quote verifies | 1.00 (hard requirement) |
| Answer relevance | Gemini-as-judge against the reference answer | ≥ 0.85 |

**Ablations to run once**, because they are the decisions worth defending to a judge:
dense-only vs hybrid; with vs without contextual prefixing; with vs without
reranking. Record the numbers — "we measured it" is a materially stronger claim than
"we used best practice".

---

## 19. Cost and latency

| Operation | Calls | Notes |
|---|---|---|
| Ingest 100-page policy PDF | 1 Flash parse + ~120 embeddings | One-off per document version |
| Underwriter question | 1 query embedding + optional rerank + 1 Pro generation | ~1.5–3 s end to end |
| Agent knowledge call | 1 query embedding + retrieval only (no generation) | ~200 ms; the agent composes the answer itself |
| Case-memory write-back | 1 embedding | On confirmation only |

**Controls:** query embeddings cached by normalised question hash; reranking skipped
on high-confidence hits; `MaxContextTokens` caps prompt size; the agent's tool
returns passages rather than generating, so the expensive path runs once per user
question rather than once per tool call.

Retrieval itself is not the cost. With HNSW over a few thousand chunks both arms
return in single-digit milliseconds, and the network round trip to Vertex AI for the
query embedding dominates the retrieval budget.

---

## 20. Failure modes and mitigations

| Failure | Why it happens | Mitigation |
|---|---|---|
| **Retrieves the right topic, wrong clause** | Embeddings blur numbers and thresholds | Hybrid sparse arm + reranking; contextual prefix carries the clause number |
| **Confident answer from a superseded policy** | Old chunks still indexed | `IsCurrent` filter + effective-date window; superseded chunks readable but never retrieved |
| **Model paraphrases the rule and changes it** | Generation drift | `quotedText` must be a literal substring; verifier strips it otherwise |
| **Citation points to a chunk not actually retrieved** | Model invents a plausible id | Verifier checks against *this query's* retrieval set, not the whole table |
| **Case memory leaks PII between applicants** | C4 written from real cases | De-identify at write time; no names, accounts or refs in chunk text; scope filter in the tool |
| **Chunk splits a rule from its exception** | Naive fixed-size chunking | Heading-aware chunker + overlap + unit tests asserting clause integrity |
| **Empty corpus at demo time** | Nobody loaded the policy docs | Seed script ships synthetic policy, typology and regulatory extracts; `MinCasesBeforeUse` hides C4 until it has content |
| **Latency spike on long answers** | Large context | `FinalTopK` = 5 and `MaxContextTokens` cap |

---

## 21. Safety, privacy and tenancy

- **No decisions.** The generator is instructed and verified never to produce
  approve/reject language. RAG explains policy; humans apply it.
- **Corpus scoping is server-side.** The caller names the corpora; the tool never
  accepts a raw SQL filter from the model.
- **Multi-tenant isolation.** `LenderId` on `KnowledgeDocs`; a query filters to the
  caller's lender plus global corpora. One lender's credit policy is never
  retrievable by another.
- **C4 is de-identified at write time,** not at read time. Pattern text only —
  "salary credited from an individual account, 92% returned within 48h" — never
  applicant identifiers.
- **Every query is logged** with retrieved ids and grounding outcome, satisfying the
  audit requirement that every piece of shown guidance is reproducible.
- **Regulatory corpus is public material**; policy corpus is customer-confidential
  and lives in the private bucket with the same lifecycle rules as case documents.

---

## 22. Scaling path

| Trigger | Change |
|---|---|
| Recall drops as the corpus grows | Raise `hnsw.ef_search` first — a config change, no index rebuild |
| Corpus > ~1M chunks, or p95 retrieval > 150 ms | **AlloyDB for PostgreSQL** (ScaNN index, same pgvector SQL) or **Vertex AI Vector Search** behind `IVectorIndex` |
| Embedding model changes | New `model_id`, re-embed in the background, cut the retrieval filter over — old and new vectors coexist safely |
| Many lenders | Partition `knowledge_chunks` by `lender_id`; the corpus filter becomes the partition key |
| Ingest volume grows | Move `KnowledgeIngestor` to Cloud Run Jobs triggered by a GCS notification |
| Cross-institution intelligence (roadmap v4) | Share **typology and de-identified case-memory chunks** across lenders — text carries no PII, which is exactly why C4 is de-identified at write time |

---

## Appendix — the one-paragraph version, for the pitch

> FinRiskLens uses retrieval where retrieval belongs. Structured case data — every
> transaction, balance and field — is queried with tools over SQL, because a
> database answers exactly and a vector search only approximately. Unstructured
> knowledge — the lender's credit policy, RBI's directions, our fraud typology
> catalogue, and the de-identified memory of every case we have confirmed — is
> retrieved with a hybrid dense-plus-keyword search, reranked, and fed to Gemini
> under a hard citation contract. Every citation is then verified in code against
> the passage it claims to quote; anything unsupported is stripped and counted on
> the dashboard. The result is that a finding doesn't just say "gambling exposure
> detected" — it says "7.2% of monthly credits went to betting merchants, exceeding
> your 5% threshold requiring senior credit approval", and it shows you the clause.
