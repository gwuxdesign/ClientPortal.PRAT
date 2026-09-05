# PRAT test-execution harness: design notes

Status: in progress, started 3rd September 2026.

## Purpose

PRAT hasn't yet been used at enough volume to have a meaningful history of
real flaky test failures. Rather than relying on synthetic or fabricated
data, this harness deliberately introduces conditions known to cause test
flakiness (timing, concurrency, load, latency, retry behaviour) across a
subset of PRAT's existing scenarios, producing genuine, self-labelled
pass/fail data with known ground truth.

## Architecture

Five stages, each extending an existing PRAT component rather than adding a
separate system:

1. **Existing PRAT scenarios** — Login, Documents, Navigation, PasswordReset
   (the four currently-active Reqnroll features). Unmodified.
2. **Fault injection layer** — five configurable profiles (see below),
   applied via an `IFaultProfile` interface.
3. **Harness orchestrator** — extends `TestRunnerService` to run a given
   scenario/profile pair repeatedly. Runs against an isolated environment
   (QA2 or DEV) with a dedicated test account pool, not REL.
4. **Result capture and labelling** — extends `TestRunReport` with fields
   for the fault profile and magnitude applied, so every run is
   self-labelling.
5. **Labelled training dataset** — structured output feeding both the flaky
   test classifier and the failure root-cause classifier.

## Fault profiles

| Profile | Target scenario | Mechanism |
|---|---|---|
| Timing/race | Login | Configurable delay before the outcome assertion |
| Concurrency | Login | Parallel `BrowserContext` sessions against a shared account pool |
| Environment load | Documents | Inflated document fixture data before the scenario runs |
| Network latency | Navigation | `Page.RouteAsync`, delayed `ContinueAsync()` |
| Retry behaviour | PasswordReset | `Page.RouteAsync`, `AbortAsync()` on first matching request |

## Key decisions

- **Configuration follows PRAT's existing convention** — `FAULT_PROFILE`
  and `FAULT_MAGNITUDE` alongside the existing `ENVIRON`, `HEADED`,
  `BROWSER` parameters, rather than a separate configuration mechanism.
- **Harness runs outside PRAT's normal CI gate.** Repeated fault-injected
  runs are slow and, by design, sometimes fail. Mixing them into the
  pipeline that gates releases would make that signal noisy for everyone
  else using PRAT. A separate, scheduled pipeline feeds harness results
  into the training dataset on its own cadence.
- **Concurrency and load profiles have external dependencies.**
  `CredentialReader`/`Credentials` needs to be confirmed thread-safe under
  parallel access. Data seeding for the load profile needs an approach
  (API, direct DB seed, or otherwise), to confirm with line manager
  alongside environment access.

## Login authentication on QA2/DEV

QA2 and DEV both enforce Twilio 2FA on every login, including test
accounts. `LoginSteps.cs` and `Credentials.cs` currently have no OTP
handling at all, only email/password, so Login cannot presently complete
on QA2/DEV.

Twilio integration to automate OTP retrieval is deferred: the person who
normally manages that integration has moved to a different project.
Revisit if capacity becomes available later in the project; not on the
critical path for now.

**Decision: target REL instead**, for all five profiles. REL uses
dedicated test accounts and test data, not real customer data, which
resolves the main data-risk concern. `LoginSteps.cs` already completes
against REL as-is (no OTP step needed), so this unblocks all four active
scenarios immediately.

Residual risk: REL is publicly reachable and likely sits behind the same
fraud detection, WAF, or rate-limiting as the live application, even for
dummy test accounts. A burst of concurrent login attempts (Concurrency
profile) can resemble credential stuffing from the outside. Staged
rollout to manage this:

- **Timing, Latency, Retry** — start immediately. All three are
  client-side (an artificial delay, or Playwright intercepting responses
  in the browser), no unusual server-side traffic pattern, nothing that
  would trip monitoring.
- **Concurrency, Load** — hold until whoever owns security/fraud
  monitoring for REL has a heads-up on timing, so a controlled test isn't
  mistaken for an incident. Not a formal sign-off process, just advance
  notice.

## Pipeline

REL is customer-facing and reachable from the public internet, so a
GitHub Actions workflow using GitHub-hosted runners can reach it
directly, no self-hosted runner needed. This keeps the harness pipeline
off company Azure DevOps infrastructure entirely, consistent with the CI
isolation decision above, and mirrors the approach already used for the
personal website project's GitHub Actions pipeline.

`.github/workflows/harness.yml` added: manual (`workflow_dispatch`)
trigger for now, with `fault_profile` (Timing/Latency/Retry — Concurrency
and Load excluded pending the monitoring-owner heads-up) and
`fault_magnitude` inputs. Generates `appsettings.local.json` and
`credentials.local.json` at runtime from GitHub Secrets (never
committed), builds, installs Playwright browsers, then runs
`dotnet test` filtered to the relevant feature tag with `ENVIRON=REL`,
`FAULT_PROFILE`, `FAULT_MAGNITUDE` set. Results uploaded as a `.trx`
artifact, a placeholder until the "result capture and labelling" stage
(extending `TestRunReport`) is built.

Required GitHub Secrets (repo settings, not yet added):
`REL_BASE_URL`, `REL_GOOD_EMAIL`, `REL_GOOD_PASSWORD`, `REL_BAD_EMAIL`,
`REL_BAD_PASSWORD`.

Note: the workflow is ready to pass `FAULT_PROFILE`/`FAULT_MAGNITUDE`
through, but nothing in the codebase resolves them yet. Running it today
exercises the existing scenarios normally with no fault actually
injected, pending the profile registry and the Latency/Retry
implementations (next up).

Move to a scheduled trigger once Timing/Latency/Retry are all proven
working reliably via manual runs.

## Open questions

- Confirmed thread safety of `CredentialReader` under concurrent access?
- Data seeding approach for the load profile (against REL test data)?
- Exact `FAULT_MAGNITUDE` ranges per profile (to be set empirically once
  each profile is running)?
- Timing for the Concurrency/Load heads-up to REL's security/fraud
  monitoring owner?

## Build log

- **3rd Sept 2026** — architecture agreed. `IFaultProfile` interface and
  `TimingFaultProfile` (first implementation) sketched.
- **3rd Sept 2026** — confirmed QA2/DEV are publicly reachable (GitHub
  Actions viable without a self-hosted runner). Found QA2/DEV enforce
  Twilio 2FA with no automation support yet in place.
- **3rd Sept 2026** — pivoted to targeting REL for all profiles, given
  Twilio integration isn't feasible right now and REL uses dedicated test
  data. Staged rollout agreed: Timing/Latency/Retry first, Concurrency/
  Load pending a heads-up to REL's monitoring owner.
- **3rd Sept 2026** — `.github/workflows/harness.yml` drafted: manual
  trigger, credentials/config generated from GitHub Secrets at runtime,
  results uploaded as a `.trx` artifact. Not yet meaningful until the
  profile registry and Latency/Retry implementations land.