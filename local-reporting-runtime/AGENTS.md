# AGENTS.md

Self-contained Docker Compose runtime that runs the `ttd/frontend-test` sample app against the real
Altinn platform services (Storage, Events, Authentication, Authorization, Access Management, Register,
Profile) built from sibling fork checkouts, plus workflow engine, pdf3 and local emulators. It is an
architecture-evaluation setup, independent of Localtest and of the Designer dev stack.

- Read [README.md](README.md) (operations, acceptance, substitutes, limitations) and
  [INVENTORY.md](INVENTORY.md) (revisions, versions, dependency classification) first.
- Always drive Compose through `scripts/*.sh`; they source `scripts/lib.sh`, which exports the
  variables `docker-compose.yml` requires. Bare `docker compose` fails interpolation.
- `certs/`, `data/`, `logs/`, `.env` and `app/publish` are generated and git-ignored; never commit them.
- Service-specific fixes belong in the respective fork (e.g. MockPorten in `altinn-authentication`),
  only orchestration, fixtures, scripts and docs live here.
- Acceptance is `scripts/acceptance.sh`, `scripts/restart-check.sh`, `scripts/observability.sh`; their
  output goes to `evidence/`.
