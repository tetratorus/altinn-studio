# Altinn 3 platform: threat model

Status: written from the code, plus the owner's answers to the threat-model questions (2026-11-02). It covers all seven `tetratorus/altinn-*` repos, and Devin security scans start from it, so update it when the deployment, the attackers or the scope change. Facts marked **[owner]** come from the owner. Everything else is a reading of the code.

## 0. The deployment that matters [owner]

**The team's own `local-reporting-runtime` stack**, not Altinn's real production. It's one Docker Compose host (`altinn-studio/local-reporting-runtime/docker-compose.yml`), deployed by `scripts/deploy-remote.sh` and `.github/workflows/local-reporting-runtime.yml`. MockPorten stands in for the real ID-porten/Maskinporten login services.
- Public ingress: only the nginx `gateway` (`ports: ["80:80", "443:443"]`), with the vhosts `local.altinn.cloud` (platform services), `app.local.altinn.cloud`, `mockporten.local.altinn.cloud` and `subscriber.local.altinn.cloud`. Postgres, Azurite, RabbitMQ, Lowkey Vault and Service Bus bind to `127.0.0.1` only.
- Not deployed in this stack: Studio Designer, Gitea, the Kubernetes operator, Flux/Helm, the Azure Terraform (`altinn-auth/infra`), Resource Registry, and the SBL bridge (`DisableA2Endpoints=true`). Code that runs only there can't be reached in the deployment the owner cares about.
- MockPorten will issue a login for any synthetic user it's asked for. The owner accepts this as mock code [owner]. So "anonymous internet user" and "logged-in user" are nearly the same attacker here: anyone can get a valid user token for some party. What has to hold is the separation *between* parties.

## 1. System overview and trust boundaries

Altinn 3 is Norway's shared public-sector platform. Citizens and businesses log in, fill in forms and submit them. Government agencies ("service owners", the `org` claim, e.g. `ttd`) build the apps in Altinn Studio and deploy them to a Kubernetes cluster for each agency. The apps run on shared platform services, and all service-to-service calls go through one gateway (`platform.<env>.altinn.no`; locally, `local-reporting-runtime/gateway/nginx.conf`).

Trust boundaries:
1. **Internet → gateway/app.** Anonymous browser traffic, and API clients with tokens from ID-porten or Maskinporten.
2. **App (one agency's tenant) → platform services.** The app holds the org token plus a PlatformAccessToken. Each platform service decides again for itself, by calling the Authorization PDP.
3. **Platform service → platform service.** Internal endpoints require a `PlatformAccessToken` signed with each service's own certificate (public certs come from Key Vault).
4. **Agency A ↔ agency B.** Tenant isolation: one org's token or app must not reach another org's instances, data or policies.
5. **Studio developer → Designer/Gitea → deploy pipeline → runtime cluster.** App code written by a developer gets built and run in production.
6. **Platform → external systems.** ID-porten and Maskinporten (in the local stack, MockPorten stands in for both), the Altinn 2 SBL bridge, KRR/FREG/ER, and webhook subscribers (outbound, URLs that the subscriber controls).

## 2. Components

| Name | Code |
|---|---|
| **App runtime** | altinn-studio `src/App` (Altinn.App backend + frontend), `src/Runtime/workflow-engine*`, `src/Runtime/pdf3` |
| **Authentication** | altinn-authentication (OIDC front channel, token exchange `/exchange/maskinporten`, `/refresh`, introspection, system users, `src/jwtcookie`) |
| **Authorization** | altinn-auth `src/apps/Altinn.Authorization` (PDP, XACML policies), `src/apps/Altinn.AccessManagement` (delegations, access packages, connections), `src/apps/Altinn.ResourceRegistry`, `src/pkgs/Altinn.Authorization.ABAC` |
| **Storage** | altinn-storage (instances, data elements, process, sign, SBL bridge, `InstanceSyncAdapterScope`) |
| **Register & Profile** | altinn-register (parties, CCR/ER import, roles, MassTransit to Access Management), altinn-profile (user profiles, contact points, KRR, support dashboard) |
| **Events** | altinn-events (CloudEvents publish, subscriptions, webhook push, Events.Functions workers) |
| **Studio** | altinn-studio `src/Designer`, `src/gitea`, `src/gitea-proxy`, `src/cli` (studioctl), `src/AI`, `src/tools` (deployer/releaser) |
| **Infrastructure** | altinn-auth `infra/` (Terraform: hub/spoke, App Gateway with a public IP, Key Vault, Postgres Flexible Server), altinn-studio `infra/` + `charts/` (Flux, Helm), `src/Runtime/operator`, `src/Runtime/gateway`, `src/load-balancer`, `.github/workflows`, `local-reporting-runtime/` |

## 3. Entry points and who can reach them

- **Anonymous:** app `[AllowAnonymous]` controllers (`HomeController`, `StatelessDataController`, `PaymentController` callbacks, `NotificationCallbackController`, `RedirectController`), Authentication `AuthenticationController`/`OpenIdController`/`LogoutController` (login redirects, JWKS), Storage `ApplicationsController` (metadata), Register `DebugController`, Designer `StudioOidcController`/`StudioctlAuthController`/`ContactController`, Events `WebhookReceiverController`.
- **End user (ID-porten JWT / `AltinnStudioRuntime` cookie):** app instance APIs, Storage instance/data reads (PEP → PDP), Access Management end-user APIs (delegate/revoke), Profile user APIs, Events subscriptions.
- **Enterprise/system user (Maskinporten token + scopes):** Storage (`altinn:serviceowner/*`, `InstanceSyncAdapterScope` *bypasses the normal authorization checks*), Events publish (`PublishScopeOrAccessTokenRequirement`), Access Management enterprise/maskinporten APIs, Authentication system-user and change-request APIs.
- **Platform-internal (PlatformAccessToken):** Register `/api/v2/internal`, Profile `UserProfileInternalController`, Storage SBL bridge, Events inbound/outbound/storage controllers, Access Management internal APIs.
- **Studio developer:** Designer APIs, Gitea via gitea-proxy (API key limited to git and `/api/v1/`), deploy pipeline, studioctl.

## 4. Assets and how sensitive they are

1. **Instance data and attachments** (Storage blob + Postgres): personal and business data, often confidential. Highest.
2. **Authorization state:** XACML policies, delegations, access packages, system-user rights, roles. Changing these leads to every other compromise.
3. **Token signing keys and credentials:** Authentication's JWT signing cert, platform access-token certs, Maskinporten JWKs, Key Vault, DB passwords, Gitea/Designer tokens.
4. **Identity and person data:** national identity numbers (SSN), party/role data in Register, KRR contact info and addresses in Profile.
5. **App code and deploy pipeline:** Gitea repos, the deployer/operator, cluster credentials (a path to run code inside agency tenants).
6. **Events and webhooks:** event payloads (they reveal instance metadata) and subscriber endpoints (SSRF from the platform network).

## 5. Attackers that matter [owner]

In scope:
- **A. Anonymous internet user** reaching the gateway's 80/443.
- **B. Logged-in citizen or company user** (through MockPorten, any synthetic party) reaching *other* parties' instances, data or rights (IDOR on `instanceOwnerPartyId`/`instanceGuid`, delegation abuse, giving themselves rights).
- **D. Vendor/system user** with Maskinporten access going beyond its delegated rights (system-user/client-delegation scope creep, `InstanceSyncAdapterScope` misuse).

Ruled out by the owner (not chosen):
- C. Malicious service owner/agency reaching another agency (the stack runs a single org's app, `ttd/frontend-test`).
- E. Malicious app developer in Studio (Designer, Gitea, deploy pipeline).
- F. Compromised platform service or network-adjacent insider misusing internal endpoints. PlatformAccessToken flaws still count when attacker A, B or D can reach them from outside.

## 6. Controls already in place

- JWT validation against Authentication's JWKS (`GeneralSettings__OpenIdWellKnownEndpoint`) in every service. Scopes are enforced per endpoint (`ScopeAuthorizationMiddleware` in the app).
- XACML PEP/PDP (`Altinn.Common.PEP`, Authorization `decision`), called again in Storage for every instance read or write (`StorageAccessHandler`, `ProcessAuthorizer`).
- PlatformAccessToken on internal endpoints. Certificates live in Key Vault.
- gitea-proxy limits API-key/basic auth to git and the REST API (`src/gitea-proxy/auth.js`).
- Azure hub/spoke network with a firewall (`altinn-auth/infra/deploy/hub/firewall.tf`), Postgres in a delegated private subnet, App Gateway as the only public ingress.
- Idempotency keys on Events, VersionPreconditions on Storage writes, and file malware-scan status (`FileScanResult`).

## 7. Out of scope / accepted risk [owner]

- **Test and mock code:** MockPorten (`altinn-authentication/samples/mockporten`), `src/Runtime/localtest`, `src/test/apps`, `test/` projects, k6 scripts, fixtures. This includes MockPorten letting anyone log in as any synthetic user, and its fixed test keys.
- **Altinn Studio Designer and its AI agent:** `altinn-studio/src/Designer`, `src/AI`.
- **Denial of service and rate limiting:** resource exhaustion, missing throttling, ReDoS.
- **Legacy Altinn 2 / SBL bridge endpoints:** Storage `SblBridgeController`, `MessageboxInstancesController` A2 paths, Register/Profile A2 fallbacks.

## 8. Tags on findings [owner]

`Component` (names in section 2), `Repository` (the `tetratorus/…` repo), `CWE` (`CWE-<n>`), `CVE` (only for vulnerable dependencies).

## 9. Open questions

1. Code that runs only in real Altinn production (Azure Terraform, the Kubernetes operator, Flux/Helm, Gitea/gitea-proxy, studioctl): this model treats it as unreachable in the deployment that matters and ranks its findings lower. Note here if any of it should count.
