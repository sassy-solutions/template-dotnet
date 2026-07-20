# AGENTS.md — working in this service

This repo was scaffolded from `sassy-solutions/template-dotnet`. It is a **.NET 9**
microservice that **consumes Nexus** as its platform and follows the same
**Compendium** architecture as Nexus itself (hexagonal, CQRS + event sourcing,
Result pattern, immutable events). Read the root `README.md` and any `CLAUDE.md`
before editing.

Any coding agent (Claude Code, Cursor, etc.) working here MUST follow the rules
below. They are not style preferences — each one maps to a production incident.

## What this service does / does not do

- ✅ Implements ONE bounded business capability. Defines its own aggregates,
  events, and projections using Compendium primitives. Persists to its OWN
  Postgres schema (`org_{slug}` when multi-tenant).
- ✅ Reads config / feature flags / secrets / identity from **Nexus.Sdk**
  (`AddNexusSdk`). Authenticates to Nexus with its API key.
- ❌ Does NOT reimplement multi-tenancy, identity, secrets, feature flags, or
  billing — those belong to Nexus.
- ❌ Does NOT hardcode tenant IDs or env names, push its own Helm chart, or
  manage its own ArgoCD app — Nexus provisions all of that.

## Day-0 invariants — HARD GATE (the silent-failure class)

If a change touches an **RLS read model, a projection, a minted secret, or a
name that becomes an identifier**, it MUST satisfy every rule below. Each failure
is *silent* (no exception, no error) — it just makes the UI show nothing or the
deploy hang.

1. **RLS reads/writes MUST pin the tenant.** If your schema enables
   `FORCE ROW LEVEL SECURITY` (recommended for tenant tables), every SELECT and
   every projection UPSERT must run with the tenant GUC pinned
   (`set_config('app.current_tenant_id', <org>, false)` on the connection, from
   the ambient tenant context or the event payload's org id — never a raw
   controller param). A pinned WRITE + an unpinned READ = "the UI shows nothing".
   For an inherently cross-tenant auth-time lookup, use a **plpgsql
   `SECURITY DEFINER`** function that sets a sentinel GUC, plus a matching
   sentinel branch in the table's policy — a bare SQL `SECURITY DEFINER` function
   does NOT escape `FORCE RLS`.

2. **Projections must be REGISTERED and idempotent.** Registering a projection in
   DI is not enough — register it with the projection processor. A projection
   that handles one event family but not another silently misses updates. Handlers
   must be idempotent (`INSERT ... ON CONFLICT DO UPDATE`) because events can be
   re-delivered. Never swallow an apply exception and advance the checkpoint.

3. **Minted secrets must be non-empty AND propagated.** Assert non-empty at mint
   (else fail — never write an empty secret). If a secret must reach a pod, write
   it and read it back to confirm.

4. **Names must be valid identifiers.** Raw names become C# namespaces, K8s/DNS
   labels, SQL schemas. All-numeric or digit-leading names are invalid C#
   namespaces. The bootstrap workflow already prefixes `Svc` — do not undo that.

5. **Never tag a release before bootstrap finishes.** The first version tag must
   land AFTER the `.bootstrapped` marker is committed, or the release CD silently
   skips the image build and the deploy ImagePullBackOffs forever.

## Compendium principles (still apply here)

- **Hexagonal**: Core has zero dependencies; adapters live in Infrastructure;
  wire ports in `Program.cs`.
- **Result pattern**: return `Result<T>`; never throw for control flow.
- **Immutability**: events, value objects, DTOs are records.
- **Event back-compat**: when adding a field to an event record, add a secondary
  `[JsonConstructor]` for old payloads.
- **Stub adapters**: `dotnet run` must work locally without Nexus reachable
  (fall back to in-memory config/features when `NEXUS__ApiKey` is missing).

## Feature triplet (scoped to this app)

Every feature ships: **Domain** (aggregate + events + projection) · **HTTP/worker**
endpoint · **Nexus.Sdk usage** (config/flags/secrets) · **Integration test**
(TestContainers Postgres) · **Deploy** (push to `main` → green CI → live).

## Gotchas

- `GITHUB_TOKEN` cannot push files under `.github/workflows/` — the bootstrap
  workflow excludes them from renames on purpose. Don't fight it.
- Do not manually edit the repo's provisioning wiring (branch protection, GitHub
  App install, ArgoCD app). If you must, document it in `CLAUDE.md`.
- Store secrets via `ISecretService`, never in `appsettings*.json`.
