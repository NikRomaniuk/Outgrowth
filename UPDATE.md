# UPDATE

This file is for recording brief update notes (one-line per update)
It will be automatically cleared by GitHub Actions after a push to the repository

Guidelines:
- Add a new line for each update with a date and short summary
- Example: 2026-01-01 — Fixed plant lifecycle timing
- Avoid sensitive data

How to append an update from the command line:

```bash
echo "$(date +%F) — Short summary of changes" >> UPDATE.md
git add UPDATE.md
git commit -m "Update notes: $(date +%F)"
git push
```

The file will be cleared on the next push by the included workflow
