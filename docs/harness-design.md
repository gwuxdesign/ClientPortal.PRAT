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

## Open questions

- Confirmed thread safety of `CredentialReader` under concurrent access?
- Data seeding approach for the load profile?
- Exact `FAULT_MAGNITUDE` ranges per profile (to be set empirically once
  each profile is running)?

## Build log

- **3rd Sept 2026** — architecture agreed. `IFaultProfile` interface and
  `TimingFaultProfile` (first implementation) sketched.
