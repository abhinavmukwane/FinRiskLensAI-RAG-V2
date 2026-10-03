# Knowledge corpora

Source markdown for the RAG layer (`11_RAG_ARCHITECTURE.md` §3). **Written for the
FinRiskLens demo — not any lender's real policy.**

| File | Corpus | Title | Version |
|---|---|---|---|
| `typology.md` | Typology (C3) | Fraud Typology Catalogue | v1 |
| `credit-policy.md` | Policy (C1) | Sample Credit Policy | v3 |

## Ingest

From the repository root. Same connection string as the app; identical bytes are a no-op,
and a changed file retires the previous version of the same title.

```bash
dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Typology Doc/knowledge/typology.md --title "Fraud Typology Catalogue" --version v1
```

```bash
dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Policy Doc/knowledge/credit-policy.md --title "Sample Credit Policy" --version v3
```

## Query

```bash
dotnet run --project tools/FinRiskLensAI.RagCli -- search Policy,Typology "what is the maximum FOIR for income above 2 lakh" --k 3
```

## Writing for the chunker

- Headings become the citation breadcrumb, so number them the way people cite them (`§4.2`).
- A clause that ends with a colon keeps the list or table after it in the same chunk.
- Typology entries are atomic: one `##` heading per entry; `###` sub-headings stay inside it.
- Do not repeat the title as a top heading — the title is already the breadcrumb root.
