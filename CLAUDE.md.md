## Documentation Loading Strategy

Always read these first:

1. HANDOFF.md
2. Doc/00_README.md

Then load ONLY the documents required for the requested task.

Do not load all documentation if it is unrelated.

Always search the repository before assuming functionality does not exist.

## Version Control Rules

- **Never commit any file without explicit permission.** Do not run `git commit`,
  `git push`, `git rebase`, or any history-changing command unless the user
  explicitly asks for it in that request. Make and verify code changes only;
  leave staging/committing to the user unless told otherwise.
- When a commit IS explicitly requested: commit directly to `main` (no PR flow),
  author is the repo owner only, and never add a Claude co-author line.
- A teammate also pushes to `main`; when asked to sync, pull with `--rebase`.

## Working Conventions

- Reuse existing implementation before writing new code; never duplicate
  functionality (one service/helper per responsibility).
- Follow the existing layering and Autofac auto-registration naming
  (`*Repository`, `*Service`); bind config via settings POCOs registered in
  `Program.cs` (like `SmtpSettings`/`GroqSettings`).
- After changes, build to verify (0 errors) before reporting done.