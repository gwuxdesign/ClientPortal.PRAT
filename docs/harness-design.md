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

Originally scoped as five stages, each extending an existing PRAT
component. Stages 1–2 held; stages 3–4 didn't happen as planned and are
corrected below (19th Sept) once it became clear `TestRunner.Web` was
never actually touched by any of this work.

1. **Existing PRAT scenarios** — Login, Documents, Navigation, PasswordReset
   (the four currently-active Reqnroll features). Unmodified.
2. **Fault injection layer** — five configurable profiles (see below),
   applied via an `IFaultProfile` interface.
3. **Harness orchestrator** — ~~extends `TestRunnerService`~~ **actually
   built as**: `.github/workflows/harness.yml`, a GitHub Actions
   workflow running `dotnet test` directly with `FAULT_PROFILE`/
   `FAULT_MAGNITUDE` set. `TestRunner.Web` is untouched — confirmed by
   grep, no references anywhere in the harness code. This was a
   reasonable substitution (it works, proven across all five profiles)
   but the original plan's wording was never corrected until now.
4. **Result capture and labelling** — ~~extends `TestRunReport`~~ **not
   yet built at all**, this was assumed to be in progress but genuinely
   isn't. Every run so far produced a `.trx` with outcome and duration,
   but nothing records which fault profile or magnitude was active —
   that context has only ever existed in conversation and CI logs, never
   attached to the result itself. None of the runs done so far are
   usable as labelled data as they stand. Real next step: a metadata
   sidecar written alongside each `.trx` (see build log, 19th Sept).
5. **Labelled training dataset** — structured output feeding both the flaky
   test classifier and the failure root-cause classifier. Not started;
   depends on stage 4 actually existing first.

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

- ~~Confirmed thread safety of `CredentialReader` under concurrent
  access?~~ Resolved 18th Sept — see build log.
- ~~Data seeding approach for the load profile (against REL test
  data)?~~ Resolved via response-rewriting rather than real data
  seeding — see build log, 17th–18th Sept.
- Exact `FAULT_MAGNITUDE` ranges per profile, beyond the boundaries
  already found empirically (Timing/Latency: 500 passes, 12000 fails;
  Retry: 0 passes, 1 fails; Load: 500 passes, 2000 fails) — tighter
  bounds could be found later if useful for the training dataset, not
  currently planned.
- ~~Timing for the Concurrency/Load heads-up to REL's security/fraud
  monitoring owner?~~ Decided 18th Sept not to pursue: no clear owner
  given the organisational changes. Concurrency remains unrun against
  REL as a deliberate scope decision — see build log.

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
- **10th Sept 2026** — `FaultProfileRegistry` built and wired into
  `TestWorld` (resolves `FAULT_PROFILE`/`FAULT_MAGNITUDE` in the
  constructor, same convention as `ENVIRON`). Applied in
  `LoginSteps.ThenTheLoginAttemptWas`, before the outcome assertion.
  Confirmed via `.trx` timings that the delay executes (~12s added to
  scenario duration), but it does not cause failures: `Expect(...)`
  polls for its own timeout window regardless of when it starts, so
  delaying the start doesn't shrink that window. `TimingFaultProfile` as
  built can only slow a scenario down, not genuinely induce flakiness.
  Decision: rework `TimingFaultProfile` to intercept the login
  request/response via `Page.RouteAsync` (the same mechanism planned for
  Latency) rather than a flat pre-assertion `Task.Delay`, once Latency's
  implementation establishes the pattern. Latency built first; Timing's
  rework follows as a known piece of fallout, not forgotten.
- **10th Sept 2026** — separately, confirmed `LoginFieldValidation`
  ("empty" case) is intermittently flaky against REL even with the
  visibility-wait fix (occasional >10s delay before `#email` becomes
  visible after the cookie banner). Unrelated to fault injection, this
  scenario never reaches `ThenTheLoginAttemptWas`. Real, naturally-
  occurring flakiness, left as-is for now, a genuine example rather than
  a defect to chase.
- **10th Sept 2026** — `LatencyFaultProfile` confirmed working as
  intended: 5/6 "Navigation after login" outline examples genuinely
  failed under a 12s delay (real assertion timeout is 5000ms, from
  Playwright's web-first assertion default, separate from the context's
  10000ms action timeout). "My notifications" and two scenarios that
  never call `TheUserClicksThe` passed for explicable, non-concerning
  reasons (see run analysis). Confirms Latency succeeds where Timing
  failed: delaying the actual response, not just the assertion's start,
  genuinely contests the timeout window.
  Observation for later: failures surface as `net::ERR_ABORTED`, likely
  because `AfterScenario` closes the context while the delayed route is
  still pending. Possible risk: synthetic timeout failures may carry a
  distinguishable signature (abort vs. genuinely slow-but-completing)
  that real-world timeout flakiness wouldn't share, worth reviewing when
  building the failure-classification labels, so the classifier learns
  genuine timeout characteristics rather than this harness's fingerprint.
- **10th Sept 2026** — `RetryFaultProfile` built (aborts the first
  `magnitude` requests, then allows subsequent ones through; magnitude
  is a count, not a duration, unlike Timing/Latency). Applied in
  `ResetSteps`, before the reset form submission. Confirmed: `magnitude:
  0` passes cleanly (no route registered, matches uninstrumented
  behaviour), `magnitude: 1` fails `PasswordReset` — PRAT's automation
  and, as far as observable, the app itself have no retry on a single
  failed attempt. Clean matched pair on record for this scenario.
- **10th Sept 2026** — `TimingFaultProfile` reworked to extend a new
  shared `NetworkDelayFaultProfile` base (route-interception delay),
  matching Latency's mechanism rather than the original flat
  pre-assertion `Task.Delay`. Application point moved from
  `ThenTheLoginAttemptWas` to `WhenTheUserSubmitsTheLoginForm`, before
  the login click, not before the outcome check. `LatencyFaultProfile`
  refactored onto the same base, removing duplicated logic.
  Confirmed via six total runs: Timing and Latency both pass cleanly at
  `magnitude: 500` and genuinely fail at `magnitude: 12000`; Retry
  passes at `0` and fails at `1`. All three profiles now proven, not
  just designed. This closes the Timing rework item.
- **17th Sept 2026** — resolved the long-open `IFaultProfile` shape
  question for `ConcurrencyFaultProfile`: no interface change needed.
  `IPage.Context.Browser` gives access back to the shared `IBrowser`,
  so a profile can spin up additional contexts/sessions without needing
  anything beyond the existing single-`IPage` signature. Implemented as
  fire-and-forget (launched via `Task.Run`, not awaited within
  `ApplyAsync`), so the parallel sessions run genuinely alongside the
  scenario's own login click rather than completing before it. Uses the
  single configured `goodLogin` account for every session (no separate
  pool configured) — arguably the more relevant test anyway: shared-
  session contention on one account.
  No change needed to `LoginSteps.cs`: the call site added for Timing's
  rework was already profile-agnostic, so it applies whichever profile
  is active without modification.
  Known risk, not yet confirmed: `AfterScenario` closes the shared
  `IBrowser` once the scenario ends, which may kill background sessions
  mid-flight if the scenario's own login resolves first. Needs a real
  run to confirm either way, same as the `ERR_ABORTED` situation.
  **Not yet run against REL** — still pending the security/fraud
  monitoring heads-up per the staged rollout decision above. The
  workflow's dropdown still excludes it; don't bypass that locally.
- **17th Sept 2026** — closed the `ERR_ABORTED` investigation.
  Re-ran Latency at `magnitude: 2000` (well under the 5000ms assertion
  timeout): all 6 scenarios that reach the fault-affected assertion
  passed cleanly, no aborts. Root cause confirmed: `ERR_ABORTED` only
  occurs when the delay meets or exceeds the assertion's own timeout,
  since that guarantees the assertion fails and ends the scenario while
  the delayed route is still holding a request open; `AfterScenario`
  then closes the browser and the still-pending request is cancelled.
  Below that threshold, the request simply completes before the
  scenario has any reason to end. Not a flaw in the profile — it's an
  artefact of magnitude choice relative to the assertion window, worth
  keeping in mind when generating labelled training data (a magnitude
  near the boundary would produce a genuine slow-but-passing case,
  useful contrast against the always-aborts case above it).
  Separately, the same run surfaced 2 unrelated failures (a 10000ms
  timeout in `GivenTheUserIsLoggedIn`, before the fault-affected step is
  ever reached), matching the same intermittent REL slowness pattern
  found earlier in `LoginFieldValidation`'s "empty" case, including the
  same incidental `401` console error. Confirms this is a recurring,
  systemic characteristic of REL rather than a one-off tied to a single
  scenario. Left as-is, a genuine example rather than a defect to chase.
- **17th Sept 2026** — `LoadFaultProfile` built and confirmed working.
  Endpoint identified via manual DevTools inspection:
  `GET .../api/document/all/metadata`, returning `documents` (the
  rendered/filterable list) and `documentsForApproval` (a separate,
  unrelated queue — confirmed only `documents` needed inflating, since
  the "All" filter's count of 6 matches `documents` exactly). Rewrites
  the response body via `Page.RouteAsync` + `FetchAsync`/`FulfillAsync`,
  appending `magnitude` extra copies with fresh GUIDs; strips
  `content-length` before fulfilling, since it would mismatch the
  inflated body. No new step-wiring needed — `Documents.feature` reaches
  the page via the same `MenuSteps.TheUserClicksThe` call site already
  wired for the other profiles.
  Unlike Concurrency, Load does not need the security/fraud-monitoring
  heads-up: it generates no extra requests to REL, the response is
  rewritten client-side after the real (small) response is fetched, so
  REL sees identical traffic to any normal run. Added to the workflow's
  `fault_profile` choices directly.
  `DocumentsSteps.cs`'s two assertions were hardcoded to the original
  6-document baseline and needed to become magnitude-aware:
  `ThenTheUserShouldOnlySeeTheRight` now expects `count × (magnitude +
  1)`; `ThenTheOfDocuments` repeats each expected title in place
  (magnitude + 1) times. The in-place repetition was confirmed, not
  assumed, via a real run: duplicated titles sit adjacent to their
  original rather than interleaved, since each clone keeps the same
  `dateCreated`, so a stable sort keeps them grouped. Both fixes
  confirmed via two runs at `magnitude: 1`: first run validated the
  count fix (no more count-mismatch errors, only the not-yet-fixed
  title-list assertion failing exactly as expected); second run's data
  informed the title-list fix. A third run should confirm both together.
- **18th Sept 2026** — Load's genuine failure boundary found. Clean
  passes at `magnitude: 1`, `30`, `500` (up to 3,006 documents); a real
  failure at `2000` (12,006 documents), bracketing a real threshold
  between the two. Unlike the earlier count/title fixes, this is not a
  test-logic defect — the "All" filter's own click action timed out at
  10000ms, with the target element already confirmed visible, enabled,
  stable and scrolled into view; both assertions after it were skipped,
  never reached. "All" is the only scenario in the run rendering the
  full unfiltered set (12,006 items) from a fresh filter-triggered
  render; every other scenario that passed either filters down to a
  smaller subset or (for the sort options) very plausibly re-orders
  already-rendered DOM rather than re-rendering from scratch. Likely
  cause: the browser's main thread genuinely congested rendering that
  many elements, delaying the click's own event handling past the
  action timeout — a real UI responsiveness ceiling, not an artefact of
  the test harness. Treated as the conclusion of the Load investigation:
  a matched pass/fail pair now exists (500 clean, 2000 fails), the same
  shape as Timing/Latency/Retry's data. No further magnitude escalation
  planned — pushing toward the original 12000 ceiling would very likely
  just make an already-understood failure more dramatic, or start
  conflating real app slowness with the browser's own rendering limits,
  a different and less useful finding.
- **18th Sept 2026** — closed both remaining open questions from the
  harness's original design.
  `CredentialReader` thread safety: confirmed safe as actually used —
  `reqnroll.json` sets `testThreadCount: 1` (scenarios never run in
  parallel), and within any scenario the credential store is always
  loaded synchronously before `ConcurrencyFaultProfile`'s background
  sessions ever start, so the only real risk (a first-load race) is
  never reachable in practice. That safety was implicit rather than
  guaranteed by the code, though, so hardened it anyway: replaced the
  manual null-check lazy-init with `Lazy<T>`, which guarantees
  exactly-once initialisation regardless of test parallelism settings,
  removing the fragility rather than just documenting around it.
  Concurrency/security heads-up: decided not to pursue this further —
  no clear owner identified given the ongoing organisational changes,
  and chasing one down isn't a good use of time against the project
  timeline. Concurrency therefore remains built and unit-reasoned-about
  but never run against REL; this is a deliberate, documented scope
  decision, not an oversight. No environment currently exists where it
  could safely run instead (QA2/DEV block on the unresolved Twilio 2FA
  gap). This is the honest final status for Concurrency in the Project
  Report: implemented and design-validated, not empirically proven,
  with the reasoning for that gap stated plainly rather than hidden.