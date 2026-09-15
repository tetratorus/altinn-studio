# Local Altinn reporting runtime

A representative Altinn reporting app (`ttd/frontend-test`) running against the **real** .NET platform
services — Storage, Events (+ workers), Authentication, Authorization, Access Management, Register,
Profile — plus the workflow engine and PDF service the app needs, all in one Docker Compose stack on a
single machine. Localtest is not part of the integrated flow.

This is an architecture evaluation setup (IRAS-like reporting), not a production deployment. The
dependency and compatibility inventory that preceded it is in [INVENTORY.md](INVENTORY.md) (repository
revisions, SDK/tool/image versions, licensing, dependency classification).

## 1. Service / dependency diagram

```
                       Browser  https://app.local.altinn.cloud/ttd/frontend-test/
                          │
      ┌───────────────────▼───────────────────────────────────────────────────────────┐
      │ gateway (nginx, TLS from local CA)                                             │
      │  app.local.altinn.cloud → app          local.altinn.cloud/<svc>/ → platform    │
      │  mockporten.local…      → mockporten   keyvault.local…:8443     → lowkey-vault │
      │  subscriber.local…      → subscriber   pdf/workflow-engine.local… → pdf3/wfe   │
      └──┬─────────────┬──────────────┬─────────────┬──────────────┬──────────────────┘
         │             │              │             │              │
   ┌─────▼─────┐  ┌────▼─────┐  ┌─────▼──────┐ ┌────▼────┐  ┌──────▼──────┐
   │ app       │  │ authenti-│  │ authoriza- │ │ storage │  │ events      │
   │ frontend- │─▶│ cation   │  │ tion (PDP) │ │         │─▶│ + workers   │─▶ subscriber
   │ test      │  │  ▲   │   │  │  │         │ │  │  │   │  │ (in-proc)   │   (webhook)
   └─┬──┬──┬───┘  └──┼───┼───┘  └──┼─────────┘ └──┼──┼───┘  └──┬──────────┘
     │  │  │         │   │         │              │  │         │
     │  │  │   mockporten│   accessmanagement ◀───┘  │   servicebus emulator (+ sb-sqledge)
     │  │  │  (ID-porten +│       ▲                    │
     │  │  │  Maskinporten│       │ RegisterSync       │
     │  │  │  substitute) │       │ (RabbitMQ)         │
     │  │  │              ▼       │                    ▼
     │  │  └──▶ profile ──▶ register ◀── register-init (migrations + synthetic seed)
     │  │
     │  └──▶ workflow-engine (process, callbacks to app)      pdf3 (Chromium, renders app pages)
     │
     └──▶ Shared infra: postgres (one DB per service) · azurite (blob/queue/table) · rabbitmq ·
          lowkey-vault (Key Vault API: platform access-token public certs) · otel-collector
```

Every arrow is a real HTTP/AMQP call over the compose network `local-reporting-runtime_default`;
services discover each other by compose service name, browsers/pdf3 use the `*.local.altinn.cloud`
hostnames (in `/etc/hosts`, resolving to 127.0.0.1) through the gateway.

## 2. Repository and version manifest

| Component | Source | Revision / version |
|---|---|---|
| Studio (app runtime libs, frontend, sample app, workflow engine, pdf3, this dir) | `tetratorus/altinn-studio` | `0d026001e43086bc41917bad94b2ed355c527f2f` |
| Storage | `tetratorus/altinn-storage` | `daa7654a72ef89a0be0c51587dacdfe2f4e1ccdd` |
| Events | `tetratorus/altinn-events` | `fcc0a70a06eb594968a52fedee8ef37052e61d16` |
| Authentication (+ `samples/mockporten`) | `tetratorus/altinn-authentication` | `b43b87eb32e4241ca43e9f03f67c09c24417e1e` + PR #4 (MockPorten changes) |
| Authorization, Access Management | `tetratorus/altinn-auth` | `1bd7288df37e9fc8cdd3c24f12e8f850606616b7` |
| Register | `tetratorus/altinn-register` | `1bdb4789d7a8abd726abe6bf92ef7da9fd4cbb55` |
| Profile | `tetratorus/altinn-profile` | `156d8c10b280f02b6b71ca28492ee4587cef8d71` |
| Workflow engine | `ghcr.io/altinn/altinn-studio/runtime-workflow-engine-app` | `a45a743b78` |
| pdf3 | `ghcr.io/altinn/altinn-studio/runtime-pdf3-worker` | `694406e93c` |
| PostgreSQL | `postgres` | `16-alpine` |
| Azurite | `mcr.microsoft.com/azure-storage/azurite` | `3.35.0` |
| RabbitMQ | `rabbitmq` | `4.1-management-alpine` |
| Service Bus emulator | `mcr.microsoft.com/azure-messaging/servicebus-emulator` + `mssql/server:2022-latest` | `latest` (only tag published) |
| Lowkey Vault | `nagyesta/lowkey-vault` | `7.3.98` |
| OpenTelemetry collector | `otel/opentelemetry-collector-contrib` | `0.128.0` |
| Gateway | `nginx` | `1.27-alpine` |
| Toolchain | Docker 29.7.2, Compose v5.4.0, .NET SDK 10.0.401, Node 24.19.0 / Yarn 1.22.22, Go 1.26.4 | Ubuntu 22.04 x86_64 |

The seven `lrr/*:local` images are built by `scripts/build-images.sh` from the sibling checkouts
(`~/repos/altinn-*`) at the revisions above; `scripts/build-app.sh` publishes the sample app with the
real frontend bundle (`src/App/frontend` → `yarn build`).

## 3. Layout

```
docker-compose.yml        all 24 containers, one network, bind-mounted state under ./data
gateway/nginx.conf        TLS termination, host routing, request-id + traceparent access log
infra/postgres/00-init.sql  one role + database per service, cross-schema grants
infra/register/seed.sql   synthetic parties/persons/orgs/users/roles (see §5)
infra/servicebus/config.json  queues/topics the Events workers expect
infra/otel/config.yaml    OTLP in (gRPC 4317 / HTTP 4318) → logs/otel/traces.jsonl
infra/static/orgs/altinn-orgs.json  org list normally served from altinncdn.no
subscriber/subscriber.py  local CloudEvents webhook receiver (records id + traceparent)
app/Dockerfile            runtime image for the published sample app (app/publish is generated, ignored)
scripts/                  up/down/build/bootstrap/login/acceptance/restart-check/observability
evidence/                 outputs of the acceptance scripts (committed examples, see §8)
```

## 4. Start, stop, restart, reset

Prerequisites: Docker + Compose v2, .NET SDK 10, Node 24 + Yarn (for the frontend bundle), `/etc/hosts`
entries for the hostnames below (one `127.0.0.1 <name>` line each), and the sibling forks cloned next
to `altinn-studio`.

```bash
cd local-reporting-runtime
cp .env.example .env              # optional: dev-only passwords, all have defaults

LRR_BUILD=1 ./scripts/up.sh       # first run: build images (~20 min) + start + bootstrap
./scripts/up.sh                   # later runs: start + (idempotent) bootstrap
./scripts/acceptance.sh           # end-to-end acceptance against the running stack (§7)
./scripts/restart-check.sh        # down.sh → up.sh → prove last instance survived (§7, item 9)
./scripts/observability.sh        # correlation evidence for the last instance (§7, item 7)

./scripts/down.sh                 # stop + remove containers, KEEP ./data (Postgres, Azurite, RabbitMQ,
                                  # SQL Server, Lowkey Vault, subscriber log, app data-protection keys)
./scripts/down.sh --purge         # stop and wipe ./data (next up.sh re-migrates and re-seeds)
```

`up.sh` starts infra → init jobs (`register-init` migrations + seed, `profile-init --run-db-migrations`,
`accessmanagement-init`) → platform services → `bootstrap.sh` (Lowkey Vault certs, Azurite containers,
Storage app metadata/texts, XACML policy, Register→Access Management sync check) → app → Events
subscription. It waits on each service's health endpoint; failures are fatal and printed.

Hostnames (all → 127.0.0.1): `local.altinn.cloud`, `app.local.altinn.cloud`, `mockporten.local.altinn.cloud`,
`keyvault.local.altinn.cloud`, `subscriber.local.altinn.cloud`, `pdf.local.altinn.cloud`,
`workflow-engine.local.altinn.cloud`, `app-frontend.local.altinn.cloud`, `otel.local.altinn.cloud`,
`pgadmin.local.altinn.cloud`.

Trust: `scripts/gen-certs.sh` creates a local CA (`certs/ca.crt`) and issues the gateway certificate,
Lowkey Vault keystore, the platform access-token signing certificate, the Authentication JWT signing
certificate, the MockPorten signing certificate and a synthetic Maskinporten client key. Containers
trust the CA via a mounted bundle; pdf3 imports it into Chromium's NSS store (`STUDIO_CA_BUNDLE`).
Import `certs/ca.crt` into your browser to avoid warnings (`certutil -d sql:$HOME/.pki/nssdb -A -t
"C,," -n lrr -i certs/ca.crt` for Chrome on Linux). `certs/` is git-ignored; all keys are generated
locally and dev-only.

## 5. Synthetic fixtures and identities

Nothing here refers to a real person or organization. PIDs are Tenor-style synthetic (month +80).

| Identity | Values | Purpose |
|---|---|---|
| Authorized person | PID `02856221086`, party `50100001`, user `1001`, role `DAGL` for the org | Allowed by the app policy |
| Unauthorized person | PID `15877749964`, party `50100002`, user `1002`, no roles | Denied for other parties |
| Reporting organization | orgno `310548510`, party `50100003` | Instance owner for org reporting |
| Service owner `ttd` | orgno `991825827`, party `50100004` | App owner / machine token consumer |

Seeded by `infra/register/seed.sql` into Register; Access Management imports parties/roles through its
real `RegisterSync` hosted service (RabbitMQ), so the Authorization PDP decides on real Access
Management data. Note that any person may act for **their own party** (role `PRIV`, granted by the
app's `policy.xml`), so "unauthorized" means no rights over other parties — see acceptance 3b.

MockPorten login: shared password `LRR_MOCKPORTEN_PASSWORD` (default `lrr-synthetic-only`) + PID.

Non-secret configuration examples: `.env.example` (all values have dev defaults), and every service's
environment in `docker-compose.yml`. No secret is committed; `.env`, `certs/`, `data/`, `logs/` are
ignored.

## 6. Browser demonstration

1. `./scripts/up.sh`, then open **https://app.local.altinn.cloud/ttd/frontend-test/**.
2. You are redirected to real Authentication (`https://local.altinn.cloud/authentication/...`) and on to
   MockPorten's login form. Enter the shared password and PID `02856221086`.
3. The app frontend loads with the synthetic user's name (Profile/Register). Start an instance;
   Task_1 → Task_2 (validation errors on invalid data, a file-upload component) → Task_3/receipt with
   generated PDFs (pdf3 → Storage).
4. In a private window, sign in as `15877749964` and open the first user's instance URL → 403.
5. Look behind the scenes: `docker compose logs -f events storage authorization`, the subscriber's
   deliveries in `data/subscriber/received.jsonl`, traces in `logs/otel/traces.jsonl`, RabbitMQ UI at
   `http://localhost:15672` (guest/guest).

## 7. Acceptance tests and results

`scripts/acceptance.sh` drives the API the frontend uses (cookie session + XSRF), against the real
services; `scripts/restart-check.sh` and `scripts/observability.sh` cover items 9 and 7. Latest results
(committed in `evidence/`):

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Synthetic sign-in through real Authentication via local IdP (MockPorten, PKCE, nonce, RFC 9207 `iss`, strict issuer validation on) | PASS | `acceptance-*.md` 1 |
| 2 | User/party/profile resolved through real Register + Profile | PASS | 2 (`userId=1001 partyId=50100001`) |
| 3 | Real Authorization + Access Management: allow authorized, deny unauthorized (read other's instance, instantiate for other party, instantiate for org → 403) | PASS | 3a, 3b |
| 4 | Valid input accepted, invalid rejected (Task_2 validator → 2 errors, `process/next` 409) | PASS | 4 |
| 5 | Data + attachments in real Storage (Postgres + Azurite), byte-identical on read; 2 PDFs rendered by pdf3 | PASS | 5 |
| 6 | Events reach real Events and the local subscriber; workers `Registered → OutboundQueue → WebhookPostResponse 200` | PASS | 6, `events-trace-*.txt`, `subscriber-*.txt` |
| 7 | Logs/traces with request ids, W3C traceparent/trace ids, instance ids, CloudEvent ids across gateway, app, Storage, Events, workflow engine | PASS | `observability-*.md` |
| 8 | Upstream tests / smoke tests | see below | |
| 9 | Stop and restart without data loss (`down.sh` → `up.sh`, 13 checks incl. attachment sha256, PDFs, process state, events, subscription) | PASS | `restart-*.md` |

Last run: acceptance `passed=26 failed=0`, restart `passed=13 failed=0`.

### Upstream builds and tests run

| Repo / area | Command | Result |
|---|---|---|
| Authentication — MockPorten | `dotnet build` + `dotnet test samples/mockporten/Mockporten.sln` | 43 passed, 0 failed, 0 skipped (`DOTNET_ROLL_FORWARD=Major`, runtime 10 only on this box) |
| All 7 service images | `docker build` from each fork's Dockerfile (release publish) | built |
| Sample app + frontend | `dotnet publish` + `yarn build` (`src/App/frontend`) | built |
| Studio spellcheck | `yarn spell:quick` on this directory | see PR CI |
| Storage / Events / Auth / Register / Profile unit tests | **not run** | skipped: no service source was changed in those forks (config only, via compose); their suites need Testcontainers/Aspire hosts and were out of scope for a config-only change |
| Cypress/Playwright E2E for `frontend-test` | **not run** | skipped: they target Localtest URLs and fixtures |

## 8. Cross-service evidence

`evidence/observability-<ts>.md` for the last acceptance instance shows, for one instance id:

- gateway access log lines with `req_id` and `traceparent` for app, Storage, workflow-engine hops;
- OTel spans from resources `frontend-test` and `WorkflowEngine` sharing trace ids
  (`Process.Callback`, `Instance.GetInstanceByGuid`, `DataClient.GetBinaryData`, `Process.RegisterEvent`,
  workflow handlers, DB spans);
- CloudEvent ids/types from `events.events`, joined to `events.trace_log` worker steps and to the
  subscriber's received ids + traceparents;
- per-service log line counts mentioning the instance (app, Storage, Events, Authorization, workflow
  engine, pdf3, subscriber).

## 9. Running inventory

`docker compose ps -a` after `up.sh` (24 containers; 3 are one-shot init jobs that exit 0):

| Container | Kind | Image | Health |
|---|---|---|---|
| app | Real Altinn app (`ttd/frontend-test`, Altinn.App from this repo) | `lrr/app-frontend-test:local` | healthy |
| storage, events, authentication, authorization, accessmanagement, register, profile | Real Altinn platform services | `lrr/<name>:local` | healthy |
| register-init, profile-init, accessmanagement-init | Migration/seed jobs | same images | exited 0 |
| workflow-engine | Real Altinn runtime (process engine) | `runtime-workflow-engine-app:a45a743b78` | started (no health endpoint exposed) |
| pdf3 | Real Altinn runtime (PDF worker, Chromium) | `runtime-pdf3-worker:694406e93c` | started |
| mockporten | External-system substitute (ID-porten + Maskinporten), from Authentication repo | `lrr/mockporten:local` | started |
| subscriber | Local Events webhook subscriber (test double for a downstream system) | `python:3.12-alpine` | started |
| gateway | Local infra (TLS, routing) | `nginx:1.27-alpine` | healthy |
| postgres | Local infra | `postgres:16-alpine` | healthy |
| azurite | Local emulator (Blob/Queue/Table) | `azurite:3.35.0` | healthy |
| rabbitmq | Local infra (MassTransit for Register/Access Management) | `rabbitmq:4.1-management-alpine` | healthy |
| servicebus, sb-sqledge | Local emulator (Azure Service Bus for Storage/Events) | emulator `latest`, `mssql/server:2022-latest` | started / healthy |
| lowkey-vault | Local emulator (Azure Key Vault API) | `nagyesta/lowkey-vault:7.3.98` | healthy |
| otel-collector | Local infra | `otel-collector-contrib:0.128.0` | started |

## 10. External substitutes (explicit)

| Real dependency | Substitute here | What stays real |
|---|---|---|
| ID-porten (OIDC) | MockPorten (`altinn-authentication/samples/mockporten`, fork PR #4 adds RFC 9207 `iss`) | Authorization-code + PKCE, nonce, signature, issuer & audience validation in Authentication |
| Maskinporten (machine tokens) | MockPorten JWT-bearer grant with a registered synthetic client key | Signed RFC 7523 assertion, scope check, real `authentication/exchange/maskinporten` |
| Azure Key Vault | Lowkey Vault (`keyvault.local.altinn.cloud:8443`) | Real Azure SDK clients, managed-identity challenge flow, cert lookup for access-token validation |
| Azure Service Bus | Microsoft Service Bus emulator | Real Wolverine/ASB client code in Storage and Events |
| Azure Blob/Queue/Table | Azurite | Real Azure Storage SDK clients |
| FREG / Enhetsregisteret / Altinn 2 roles (Register upstream sources) | `infra/register/seed.sql` synthetic parties/roles written directly into Register's schema | Register API, Register→Access Management sync, PDP decisions |
| Altinn 2 / SBL bridge (Storage `A2` endpoints) | disabled (`GeneralSettings__DisableA2Endpoints=true`) | — |
| altinncdn.no org list | `infra/static/orgs/altinn-orgs.json` via gateway | — |
| Downstream event consumer | `subscriber/subscriber.py` | Events subscription validation + webhook delivery workers |
| Azure Monitor / App Insights | OTel collector → file | OTLP export from app and workflow engine |

## 11. Unresolved limitations

- **Licensing**: `altinn-storage`, `altinn-authentication`, `altinn-profile` and the Register service
  tree carry no license grant (only Studio, Events, Auth are MIT). Fine for local evaluation; not
  resolved for redistribution.
- **Resource Registry and Notifications** are not run. Nothing on the acceptance path calls them; a
  flow that needs them (resource-based delegations, notifications on submission) would need
  `altinn-resource-registry` / `altinn-notifications` added.
- **Service Bus emulator** is published only as `latest` and is distroless (no in-container health
  check); readiness is a TCP wait in `up.sh`.
- **Register is run as a plain container** (its migrations + seed), not through the upstream Aspire
  AppHost; the vendored Register copy in `altinn-auth` is a placeholder template and is not run.
- **Synthetic seed writes directly into Register's tables** instead of arriving through its upstream
  import jobs; role/party shapes follow the schema at the pinned revision and must be revisited if
  Register migrations change.
- **Upstream unit-test suites of Storage/Events/Auth/Register/Profile were not executed** here (no
  source change in those forks; see §7).
- **HTTP/2 is disabled** in the shared dotnet env of this compose file, so the app exports OTLP over
  HTTP/protobuf (4318) instead of gRPC.
- **`app/publish` is a build artifact** (ignored); `scripts/build-app.sh` must be re-run after changing
  the app or `Altinn.App` libraries.
- Platform access-token *private* keys are dev-only PFX files mounted from `certs/`; there is no
  rotation.
