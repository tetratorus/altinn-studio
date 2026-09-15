# Dependency and compatibility inventory

Milestone 1 deliverable for the local Altinn reporting runtime (architecture evaluation of an
IRAS-like reporting system). This document records *what was found*, not what will be built.
Decisions and unresolved items are listed at the end.

Inventory date: 2026-09-15. Machine: Ubuntu 22.04.5 LTS, x86_64, 8 vCPU, 31 GiB RAM, ~108 GiB free.

## 1. Repository and revision manifest

| Component | Repository (checkout) | Commit | Branch / date | License in repo | GitHub license metadata |
| --- | --- | --- | --- | --- | --- |
| Studio / app runtime / CLI / Localtest / sample apps | `tetratorus/altinn-studio` | `0d026001e43086bc41917bad94b2ed355c527f2f` | `main`, 2026-09-15 | `LICENSE` — MIT (Digdir 2017) | MIT |
| Storage | `tetratorus/altinn-storage` | `daa7654a72ef89a0be0c51587dacdfe2f4e1ccdd` | `main`, 2026-09-15 | **none** | **none** |
| Events | `tetratorus/altinn-events` | `fcc0a70a06eb594968a52fedee8ef37052e61d16` | `main`, 2026-09-15 | `LICENSE.md` — MIT (Altinn 2022) | MIT |
| Authentication | `tetratorus/altinn-authentication` | `b43b87eb32e4241ca43e9f03f67c09c24470516e` | `main`, 2026-09-14 | **none** | **none** |
| Authorization + Access Management | `tetratorus/altinn-auth` | `1bd7288df37e9fc8cdd3c24f12e8f850606616b7` | `main`, 2026-09-15 | `LICENSE` — MIT (Altinn 2024) | MIT |
| Register | `tetratorus/altinn-register` | `1bdb4789d7a8abd726abe6bf92ef7da9fd4cbb55` | `main`, 2026-09-09 | **no LICENSE file**; `Directory.Build.props` sets `PackageLicenseExpression=MIT` for published NuGet packages only | **none** |
| Profile | `tetratorus/altinn-profile` (forked during inventory; identical to upstream head) | `156d8c10b280f02b6b71ca28492ee4587cef8d71` | `main`, 2026-09-10 | **none** | **none** |

All revisions are the current `main` heads of each fork; no cross-repo release tags pin them
together (Altinn deploys each service independently from `main`). Compatibility is therefore
established by shared *contract packages* (section 3), not by matching tags.

### Licensing verification (per repository, not inherited)

- MIT: `altinn-studio`, `altinn-events`, `altinn-auth`.
- **No license grant found**: `altinn-storage`, `altinn-authentication`, `altinn-profile`
  (no LICENSE file, no SPDX header, GitHub reports no license). Register grants MIT only for its
  NuGet packages (`Altinn.Register.Contracts` etc.), not the service source tree.
  Default copyright applies: the source is public but no redistribution/modification right is
  granted. Running the code locally for evaluation is a low-risk use, but **forking and
  publishing modified versions of these four repositories is not covered by an explicit license**.
  Flagged for the user; not a technical blocker for the local runtime.

### Repository access

`tetratorus/altinn-profile` did not exist at the start of the inventory; the user created the fork
during the inventory and the local checkout now tracks it (same commit as upstream `main`).
No other first-party repository from the required list is missing; see section 9 for first-party
services that are *referenced* but not in the list.

## 2. Toolchain and container runtime

| Tool | Required by | Pinned / documented | Installed |
| --- | --- | --- | --- |
| .NET SDK | all services | `altinn-studio/src/App/backend/global.json` 10.0.203 (latestFeature); `src/Runtime/localtest/global.json` 10.0.302; `altinn-auth/global.json` 10.0.107 (latestMajor); `altinn-register/global.json` 10.0.203 + Aspire AppHost SDK 13.4.6; Storage/Events/Authentication/Profile: no `global.json`, Dockerfiles use `sdk:10.0` | 10.0.401 (`~/.dotnet`) — satisfies all pins via roll-forward |
| Go | `studioctl` (`src/cli/go.mod`: `go 1.26.4`) | 1.26.4 | 1.26.4 (`~/go-sdk/go`) |
| Node.js / Yarn | `src/App/frontend` (dev server), Studio repo scripts (`yarn docs:validate`, spellcheck) | Node LTS per `README.md` | Node 22.23.2, Yarn 1.22.22 (corepack) |
| Docker Engine + Compose | all infra + service containers | — | Engine 29.7.2, Compose v5.4.0, daemon reachable |
| Azure Functions Core Tools | `altinn-events/src/Events.Functions` (README) | v4 | not installed — alternative: run Functions host container `mcr.microsoft.com/azure-functions/dotnet-isolated` |
| Register README | says ".NET 8" | **stale**: `global.json` pins 10.0.203 — treat `global.json` as authoritative |

### Container images referenced by the repositories

| Image | Used by |
| --- | --- |
| `mcr.microsoft.com/dotnet/sdk:10.0-alpine3.24` / `aspnet:10.0-alpine3.24` | Storage Dockerfile |
| `mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.23` / `aspnet:10.0.12-alpine3.23` | Events Dockerfile |
| `mcr.microsoft.com/dotnet/sdk:10.0-alpine` / `aspnet:10.0-alpine` | Authentication, Register Dockerfiles |
| `mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:d8ee3981…` / `aspnet:10.0-alpine@sha256:27b6b84b…` | Authorization, Access Management Dockerfiles |
| `mcr.microsoft.com/dotnet/sdk:10.0.301-alpine3.23` / `aspnet:10.0.12-alpine3.23` | Profile Dockerfile |
| `postgres:16` (Storage compose), Postgres via Aspire (Register), `postgres` (auth compose) | databases |
| `mcr.microsoft.com/azure-storage/azurite` | Storage, Events, Authentication, Auth, Profile |
| `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` + `mcr.microsoft.com/mssql/server:2022-latest` | Events (Wolverine transport) — Storage also uses Wolverine/ASB |
| `valkey/valkey` | Auth compose (Access Management cache) |
| `rabbitmq` (management plugin) | Register (MassTransit bus) |
| `dpage/pgadmin4` | Storage/Auth compose (optional) |
| `ghcr.io/altinn/localtest`, `ghcr.io/altinn/pdf3`, workflow-engine image (or built locally with `STUDIOCTL_INTERNAL_DEV=true`) | Studio `studioctl env up` |

## 3. Shared contract packages (compatibility check)

| Package | Storage | Events | Authentication | Auth (Authz/AM) | Register | Profile | App runtime (`Altinn.App.*`) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Altinn.Common.PEP` | 4.2.3 | 4.2.3 | — | — | — | 4.2.2/4.2.3 | 4.2.3 |
| `Altinn.Common.AccessToken` | 5.1.12 | 5.1.9 | 5.1.11/5.1.12 | 5.1.11 | — | 5.1.9 | — |
| `Altinn.Common.AccessTokenClient` | 3.2.12 | 3.2.8 | 3.2.x | 1.1.5 | — | 3.2.x | 3.2.x |
| `Altinn.Register.Contracts` | — | — | 1.7.0 | 1.7.0/1.8.0 | source | 1.7.0 | — |
| `Altinn.Platform.Storage.Interface` | 4.7.1-pr.2231 (experimental) | — | — | — | — | — | same experimental version |
| `Altinn.Authorization.ServiceDefaults` | — | — | — | 5.5.0 | source/5.5.0 | — | — |

All services are on the same major versions of the token/PEP libraries, and the app runtime uses
the same experimental Storage.Interface as Storage. No incompatibility found at the contract level.
Endpoint-level compatibility (e.g. Register v2 internal API used by Authentication and Profile) will
be verified at runtime in milestone 2.

## 4. Services, ports and outbound dependencies

Launch-profile ports (from each `launchSettings.json`); these match the cross-references in the
services' checked-in `appsettings.json` and are used as the local port plan.

| Service | Path | Port | Datastore | Outbound HTTP dependencies (checked-in local config) | Messaging / other |
| --- | --- | --- | --- | --- | --- |
| Reporting app (`ttd/frontend-test`) | `altinn-studio/src/test/apps/frontend-test` | 5005 via studioctl | — | Storage, Register, Profile, Authentication (OpenID), Authorization, Events, Access Management, PDF3, workflow-engine (all injected by `studioctl`, `src/cli/internal/cmd/app/env.go`, overridable via env because they are `setDefault`) | — |
| Storage | `altinn-storage/src/Storage` | 5010 | Postgres `storagedb`; Azurite blob | Authorization (`:5050`), Register (`:5101`→ retarget `:5020`), Authentication OpenID (`local.altinn.cloud`) | Wolverine → Azure Service Bus (emulator); Azurite queue `file-scan-inbound` |
| Events API | `altinn-events/src/Events` | 5080 | Postgres `eventsdb` | Register, Profile, Authorization (`:5101` → retarget) | Wolverine → ASB emulator queues `altinn.events.*`; Azurite queues |
| Events Functions (workers) | `altinn-events/src/Events.Functions` | Functions host | — | Events API, Key Vault (**must be disabled locally**) | ASB listeners: outbound + subscription-validation |
| Authentication | `altinn-authentication/src/Authentication` | 5040 | Postgres `authentication` | Profile (SSN → user lookup), Register v2 internal (self-identified users, org lookup), Access Management, Authorization, Resource Registry (system-user flows only) | Azurite queue (event log); signing cert `jwtselfsignedcert.pfx` |
| Authorization (PDP) | `altinn-auth/src/apps/Altinn.Authorization` | 5050 | Postgres `authorizationdb`; Azurite blob (policies) | Register `:5020`, Storage `:5010`, Profile `:5030`, Access Management `:5117`, Resource Registry `:5100`, OED (probate) | — |
| Access Management | `altinn-auth/src/apps/Altinn.AccessManagement` | 5117 | Postgres (auth DB); Valkey | Authentication `:5040`, Authorization `:5050`, Profile `:5030`, Register `:5020`, Resource Registry `:5100` | Azurite |
| Register | `altinn-register/src/apps/Altinn.Register` | 5020 | Postgres `register` | Authentication, Access Management, **FREG, SIRE, SIRE-events, Altinn 2 / SBL bridge** | RabbitMQ (MassTransit) |
| Profile | `altinn-profile/src/Altinn.Profile` | 5030 | Postgres `profiledb` | Register `:5020`, Authorization, Notifications (`:5090`), **KRR, KOF, SBL bridge** | — |
| PDF3, workflow-engine | `altinn-studio/src/Runtime/{pdf3,workflow-engine}` | studioctl topology (`local.altinn.cloud:8000`) | workflow-engine: Postgres | app callback | — |

### Dependency classification

**Real Altinn application/service (run from source):** reporting app + frontend, Storage, Events
API, Events Functions workers, Authentication, Authorization, Access Management, Register, Profile,
PDF3 and workflow-engine (required by the sample app's process).

**Local infrastructure / emulator:** PostgreSQL (one per service DB: `storagedb`, `eventsdb`,
`authentication`, `authorizationdb`, `register`, `profiledb`, workflow-engine), Azurite (blob +
queue), Azure Service Bus Emulator + MSSQL backing store, RabbitMQ, Valkey, Traefik/host routing
from `studioctl env up`, OTel collector/Grafana stack from `studioctl` (logs/traces).

**External-government-system substitutes (explicit, synthetic):**

| External system | Consumer | Substitute | Notes |
| --- | --- | --- | --- |
| ID-porten (upstream OIDC IdP) | Authentication | **MockPorten** (`altinn-authentication/samples/mockporten`) — synthetic-only OIDC provider validating Tenor-style PIDs (month 81–92, mod-11) | first-party, explicitly synthetic; configured as an `OidcProviders` entry |
| FREG (population register) | Register person import | **Synthetic seed** of person parties written through Register's own import/DB path; FREG client disabled | Register has no built-in FREG stub |
| SIRE / CCR (company register) | Register org + role import | **Synthetic seed** of org parties and external roles (`dagl`, `regna`, …); SIRE listen/enrich disabled (AppHost already defaults these off) | roles drive Access Management → Authorization decisions |
| Altinn 2 / SBL bridge | Register (party/user ids, profiles), Profile, Authentication (legacy cookies) | disabled (`PartyImport__A2__*=false`, `Party__CreatePartyId=true`); Authentication ADR-0004 documents SBL decommission | user ids must be assigned locally by Register |
| KRR (contact/reservation register), KOF | Profile | jobs disabled (`JobSettings.KrrSyncEnabled=false`, `OrgSyncEnabled=false`); contact info seeded synthetically or absent | Profile still serves `users/…` from Register data |
| Maskinporten | Register (FREG/SIRE client credentials), Authentication (org tokens) | not needed once FREG/SIRE disabled; Maskinporten org-token exchange out of scope | no live call |
| Azure Key Vault | Events Functions, all services' prod config | local config values / env vars | no Azure resources |
| Resource Registry (`altinn-resource-registry`, first-party but **not in the provided fork list**) | Authorization, Access Management, Authentication | **not run** — app flows use `org/app` policies pushed to Authorization's policy blob store, not resource-registry resources | outbound calls to it must be shown as unused or explicitly disabled at runtime; flagged as a possible access request |
| Notifications (`altinn-notifications`, first-party, not in list) | Profile, Authentication | not run; Profile notification-settings import disabled | not on the acceptance path |
| OED / probate | Authorization | not run | only used for probate roles |

## 5. Register decision

`altinn-auth/src/apps/Altinn.Register` is **not** a Register implementation: its `Program.cs` is
the .NET `WebApplication` weather-forecast template, and the csproj references only
`Microsoft.AspNetCore.OpenApi`. It is a placeholder from a planned repository consolidation.
Authorization and Access Management consume Register over HTTP (`ApiRegisterEndpoint`,
`Altinn.Register.Contracts` 1.7/1.8), which is exactly what standalone `altinn-register` serves.

**Decision:** run one Register instance from `tetratorus/altinn-register` on `:5020`; the vendored
copy in `altinn-auth` is not built or run.

## 6. Identity, signing and trust

- Authentication signs Altinn JWTs with its own certificate (dev: `jwtselfsignedcert.pfx`, checked
  in) and publishes JWKS at `authentication/api/v1/openid/.well-known/openid-configuration`.
- Storage, Events, Authorization, Access Management, Register and Profile validate tokens with
  `Altinn.Common.AccessToken` / `IPublicSigningKeyProvider` against Authentication's OpenID
  metadata; the app validates the runtime cookie against the same endpoint. All services must be
  pointed at the local Authentication OpenID URL (checked-in values point to AT22 and
  `platform.at22.altinn.cloud` — every such value is overridden locally, none is removed).
- Platform access tokens (service-to-service, `Altinn.Common.AccessTokenClient`) are signed with a
  per-issuer certificate; the local issuer set (`platform`, `ttd` app) must be configured in each
  service's `AccessTokenSettings`. Studio Localtest's `JWTValidationCert.cer` /
  `jwtselfsignedcert.pfx` are the reference dev key pair.
- Browser sign-in path: app → `GET authentication/api/v1/authentication?goto=…` → Authentication
  redirects to MockPorten → callback → Authentication resolves the PID via **Profile → Register**
  (`IdentifyOrCreateAltinnUser`, `OidcServerService.cs`) → session cookie. The synthetic person
  must therefore exist in Register (with a user id) before login; that is the FREG/Altinn 2
  substitute above.
- No middleware is removed and no allow-all policy is introduced; authorization is decided by
  Authorization's XACML PDP using the app's `policy.xml` and roles from Access Management.

## 7. Reporting application selection

`ttd/frontend-test` (`altinn-studio/src/test/apps/frontend-test`): structured data model with
validation, file-upload components, process `data → confirmation → pdfIfRequested`, and a role-based
`policy.xml` (`regna`, `dagl`, `seln`). It references `Altinn.App.Api` by project reference, so the
app runtime is built from the same Studio revision. It covers input validation, draft/save (instance
data PUT), submission (process next), attachments and receipt without adding any tax logic.

## 8. Upstream test fixtures reviewed

- Storage: DB-backed xUnit tests need Postgres `storagedb` (`Password`), Verify snapshots.
- Authentication: Testcontainers Postgres (Docker required); mock outbound clients.
- Events: xUnit + Postgres; ASB emulator config in `emulator/config.json` (queue list).
- Auth: per-app test projects under `src/apps/*/test`; Testcontainers.
- Register: Aspire AppHost + Testcontainers; `data/` fixtures.
- Studio: Localtest testdata (`src/Runtime/localtest/testdata`) provides synthetic parties, users
  and org profiles that can be reused as the synthetic seed source; Cypress/Playwright E2E for
  `frontend-test` under `src/App/frontend/test`.

## 9. Blockers and open questions before milestone 2

1. ~~Profile fork missing~~ — resolved, fork created.
2. **Licensing gaps** — Storage, Authentication, Register (service), Profile have no license.
3. **Resource Registry / Notifications** are first-party dependencies configured in Authorization,
   Access Management, Authentication and Profile but are not in the provided list. Plan: run
   without them and prove they are not on the acceptance path; if a required call surfaces at
   runtime, request `tetratorus/altinn-resource-registry` rather than mocking it.
4. **Synthetic seeding into Register** (persons, orgs, roles, user ids) is the central substitute;
   Access Management must then sync those from Register (its `RegisterSync` feature flags) so the
   PDP sees roles. This is real-pipeline work, not a mock, but it is the highest-risk item.
5. Register README/.NET mismatch and Aspire-based dev host: Register will be run as a plain
   container (Dockerfile) with its `register-init` migration step, not through Aspire, to fit the
   single compose orchestration.
6. Events workers require the Azure Service Bus emulator (EULA acceptance env var) and an Azure
   Functions host; both are free, local, and license-acceptable for development.

Feasibility verdict: **feasible with the substitutes above**, subject to items 2–4.
