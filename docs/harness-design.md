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
4. **Result capture and labelling** (built 19th to 24th Sept):
   ~~extends `TestRunReport`~~ **actually built as** a step inside the
   harness workflow that, after each iteration, parses the `.trx` and
   appends one row per scenario to `data/harness-results.csv` in the
   repo, joined with the run's metadata (fault profile, magnitude,
   target category, iteration, run id and URL, commit, timestamp), then
   commits it back to the branch. Run metadata reaches the script as
   environment variables; the original JSON sidecar was dropped (see
   build log, 24th Sept). Guards refuse to append if the header no
   longer matches the schema, and repair a missing trailing newline
   before appending.
5. **Labelled training dataset**: the CSV above is the dataset. It
   exists and is growing, but is not yet large or balanced enough to
   train a classifier on (see build log, 24th Sept and 1st Oct). The
   next piece of work is generating it at scale through a magnitude
   sweep.

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
- Exact `FAULT_MAGNITUDE` ranges per profile. Boundaries found so far:
  Timing and Latency pass at 500 and 2000 and fail at 12000; Retry
  passes at 0 and fails at 1; Load passes at 500 and fails at 2000.
  This is now actively needed rather than optional: the classifier
  needs examples across the range, especially near each failure
  threshold, not only at the extremes. Sweep in progress (see build
  log, 1st Oct).
- Why did Load at magnitude 750 with 5 repeats run for over two hours
  before being cancelled? Undiagnosed, as nothing survived to inspect.
  Not reproduced on 3rd Oct: 750 at 1 repeat, and 1000 and 1250 at 5
  repeats, all finished normally. Probably a one-off.
- ~~Why did the sweep runs for Timing and Latency at 4000 to 10000 and
  Retry leave no rows?~~ Resolved 3rd Oct: a failing `dotnet test`
  ended the loop script before the append (see build log). Load at 750
  is a separate, still undiagnosed hang.
- Do first iterations of a run fail more often than later ones (a
  warm-up effect)? Suggestive but unproven, see the 3rd Oct entry on
  the verified loop fix. Settle with more data from the planned sweep
  before deciding whether to flag, filter or warm up.
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
- **19th Sept 2026**: corrected the architecture description. The
  original plan had the harness extending `TestRunner.Web`'s
  `TestRunnerService` and `TestRunReport`; a search confirmed nothing
  in the harness code ever touched `TestRunner.Web`. The orchestrator
  was in practice the GitHub Actions workflow, and result capture and
  labelling had never been built, so none of the earlier runs were
  usable as labelled data (the `.trx` records outcome and duration, but
  not which fault profile or magnitude was active). Decisions: treat
  all earlier ad-hoc runs as validation rather than data (they were not
  retained, so no retrofitting); store results as a single accumulating
  CSV in the repo, committed back by the workflow after each run,
  rather than a folder of files or an external database (no new
  service, and it protects the timeline). Architecture section above
  updated to match.
- **20th Sept 2026** — dataset accumulation pipeline confirmed working
  via 3 real runs: metadata correctly joined per row, commits landing
  as expected. But the data itself surfaced a real problem: at
  `magnitude: 0` (a guaranteed no-op — `RetryFaultProfile.ApplyAsync`
  returns before touching the page), `PasswordReset` (15.3s) and
  `CancelPasswordReset` (18.0s) both failed. Across the three runs,
  `CancelPasswordReset` shows 17.1s(fail)/18.0s(fail)/4.0s(pass) — the
  same signature already found twice before (`LoginFieldValidation`
  "empty", `GivenTheUserIsLoggedIn` during a Latency run): an
  occasional multi-second stall somewhere in the flow, unrelated to any
  injected fault. Three independent locations now — this is a genuine,
  systemic characteristic of REL, not a one-off.
  Consequence for the dataset: a row reading `fault_magnitude: 0,
  outcome: Failed` is currently ambiguous — it could mean the mechanism
  broke (it can't, the code guarantees a no-op) or REL was just slow
  that moment. Training anything on these labels without accounting for
  this would mean learning environmental noise alongside genuine
  signal. Not yet resolved — options are an empirical baseline-noise
  characterisation (repeated `magnitude: 0` runs to get an actual rate)
  or documenting it as a known dataset limitation and proceeding as-is,
  given timeline pressure. Decision pending.
- **24th Sept 2026**: added a `repeat_count` input to the workflow so
  one trigger can run the same configuration N times in sequence (for
  baseline noise characterisation). A small verification run (2
  repeats) appended no rows and uploaded no metadata file. This was
  first diagnosed as a problem with two nested heredocs in the loop, and
  "fixed" by passing metadata as environment variables instead of a
  JSON file. That diagnosis was wrong (corrected 3rd Oct, see below):
  the run had failing tests, and the real cause was the failing
  `dotnet test` ending the script before the append. The re-run passed
  because nothing failed, not because of the change. The environment
  variable approach is simpler and has been kept, but it was not the
  fix. The small check first was still worth it, as it surfaced the
  symptom before the full sweep.
  Two operational notes from the same session. Input fields in the
  `on: workflow_dispatch: inputs:` block are read from `main`'s copy of
  the workflow whatever branch is selected, so a change to the inputs
  needs syncing to `main`, whereas a change to the job steps does not.
  And because the workflow commits to the working branch after each
  run, a local branch diverges from the remote unless a pull comes
  before any local edit.
- **24th Sept 2026**: two data-quality incidents in the dataset CSV,
  both noticed because GitHub's CSV preview stopped rendering as a
  table. First, a schema mismatch: the `iteration` column was added
  after the file already existed with an older 10-column header, so new
  rows had 11 fields against a 10-field header. Fixed with a one-off
  migration (header rewritten, `iteration` backfilled as 1 for earlier
  rows, since each was genuinely a single execution). Second, a
  missing trailing newline: a local edit stripped the file's final
  newline, so the next appended row fused onto the previous line and
  produced one 21-column row, which was split back into two. The
  workflow's append step now refuses to append when the existing
  header does not match the current schema, and adds a trailing
  newline if one is missing. Edits to the CSV are best applied with a
  terminal copy rather than an editor, which can strip the final
  newline.
- **24th Sept 2026**: baseline noise characterisation complete: 10
  repeats of `magnitude: 0` across all four categories, 262 baseline
  rows. Login, Navigation and Documents: 0 failures in 210 rows.
  PasswordReset: 2 failures in 52 rows (3.8%), both in `PasswordReset`
  and `CancelPasswordReset` (1 in 13 each, 7.7%); `PasswordResetValidation`
  passed every repeat. Overall 2 in 262 (0.76%), though that average
  hides how concentrated the noise is. This is the same intermittent
  REL slowness seen earlier in other scenarios. It resolves the
  pending decision above: characterised rather than accepted as-is.
  Baseline labels from Login, Navigation and Documents can be trusted
  as they stand; only `PasswordReset` and `CancelPasswordReset` carry a
  quantified background noise rate to account for when interpreting
  their failure labels. See the 3rd Oct entry for a survivorship caveat
  on these figures.
- **24th Sept 2026**: first step for the flaky test classifier
  (Objective 2): defined the label. A single row's outcome does not
  indicate flakiness; a condition is flaky only when repeats of the same
  scenario, profile and magnitude give mixed outcomes. A test that
  always fails at a high magnitude is deterministically broken by the
  fault, not flaky. Applied across the whole dataset (parameterised
  examples grouped by scenario), only 13 conditions had enough repeats
  to assess: 3 flaky (all `PasswordReset` or `CancelPasswordReset`), 9
  deterministic passes and 1 deterministic fail. That is too few and
  too imbalanced to train on. The dataset was built to prove the
  profiles and characterise baseline noise, not to train a classifier.
  Next step is a deliberate sweep across magnitude levels between each
  profile's known pass and fail points, each repeated enough to label
  reliably, concentrating near each threshold where genuine flakiness
  is most likely.
- **1st Oct 2026**: first pass of the magnitude sweep, mostly not
  captured. Only three runs persisted rows: Timing at 2000 (10 repeats,
  0 failures) and Latency at 2000 (two runs, 3 and 10 repeats, 0
  failures). Both pass cleanly at 2000, consistent with the earlier
  boundaries (clean at 500, failing at 12000). Nothing persisted for
  Timing or Latency at 4000 to 10000, for Retry, or for Load. Watched
  live, most of those runs were failing, but without surviving rows that
  cannot be confirmed or characterised. Load at 750 with 5 repeats ran
  for over two hours and was cancelled; the cause is undiagnosed. The
  only hypothesis is that 750 (about 4,500 documents) sits in a zone
  slower than the clean 500 case but not failing fast the way 2000 does
  through a bounded click timeout, compounding across 8 scenarios and 5
  repeats. Why the other runs left no rows is also unconfirmed.
  Candidates: runs cancelled part way (the workflow then committed only
  after the whole loop, so cancellation discarded everything); a push
  rejected after overlapping runs; or queued triggers superseded by the
  workflow's concurrency group (GitHub keeps one pending run per group
  and cancels earlier pending ones). The status of each run in the
  Actions history would settle which. Resolved on 3rd Oct: none of
  these, see below.
- **3rd Oct 2026**: workflow changed to commit and push after every
  iteration instead of once after the whole loop, with a pull and
  rebase before each push, so a run that is cancelled or cut short
  keeps every iteration completed so far. This protects against
  cancellation but, as found later the same day, it was not the cause
  of the 1st Oct losses. It only touches the job steps,
  not the inputs, so it does not need syncing to `main`. Plan: re-run
  the Timing and Latency levels from 4000 to 10000, Retry and the Load
  levels, one trigger at a time and letting each commit land before the
  next; retest Load at 750 with a single repeat and time one iteration
  before scaling up. Objective 2's original mid-September target has
  passed because the harness and dataset work ran longer than planned,
  for the reasons recorded above.
- **3rd Oct 2026**: root cause of the missing sweep data found, and it
  is not what the entries above suspected. The workflow's loop step
  runs under `bash -e`, and `dotnet test` exits non-zero whenever any
  test fails, so the first iteration containing a failure ended the
  whole script before that iteration's rows were appended or committed.
  A log from one of the lost runs (Retry, PasswordReset) shows exactly
  this: iteration 1 of 10, one test failed (the expected result of the
  Retry fault, via the aborted POST), then "Process completed with exit
  code 1" with no iteration 2. Reproduced locally with a stub that
  fails like `dotnet test`: the loop stopped after iteration 1 and no
  CSV was written. This explains every missing run in the sweep at
  once: Timing and Latency at 4000 to 10000 and Retry at 1 all fail by
  design, so each stopped at iteration 1 and recorded nothing. It also
  means the harness could not record failures in repeated mode at all,
  which is the data the classifier needs most. Runs where every test
  passed completed normally, which is why only those persisted (Timing
  and Latency at 2000, and the baseline sweeps).
  Fix: the loop records the test exit code and carries on, a failed git
  pull or push is no longer fatal, and the step exits with the recorded
  code at the end so a run containing failures still shows red.
  Verified locally with stubs: a failing test now gives every iteration
  recorded, every row written and exit code 1, while an all-pass run
  gives exit 0. The earlier same-day change (commit after every
  iteration) was not the fix for this and only helps against
  cancellation.
  Consequences for existing data. First, the baseline sweeps stand as
  observations: each ran all 10 iterations, so no iteration in them
  failed. Second, there is survivorship bias in the noise figures: a
  baseline run that failed at iteration 1 left no rows. One is known,
  the first Retry verification run on 24th Sept (magnitude 0), where
  PasswordReset and CancelPasswordReset failed and nothing was
  recorded. The 2 baseline failures in the dataset both come from a
  single run on 20th Sept, and the failing runs cluster early (the 20th,
  then the morning of the 24th) while later runs were clean, which looks
  more like an episodic bad spell on REL than independent noise per
  scenario. The 0.76% overall and 7.7% per-scenario figures are best
  read as lower bounds. Third, Latency at 2000 has a run (36879788575)
  triggered with 5 repeats that stopped after 3. Confirmed from its log:
  iterations 1 to 3 passed 8 of 8 and were recorded, iteration 4 had one
  failure (`NavigationAfterLogin` "My profile": URL still the base page
  after the 5000ms assertion window, `GET /profile` ending in
  `net::ERR_ABORTED`, the delay-induced signature) and the script ended
  there, so iteration 4's eight results and iteration 5 were lost. This
  is a genuine failure at a magnitude that otherwise passed: across the
  14 iterations observed at Latency 2000 (3 and 1 in this run, 10 in the
  later run), 1 had a failure. By the mixed-outcome definition that
  makes this condition flaky, and it puts the start of the threshold
  zone at around 2000. To be recovered from the run's uploaded artifact
  (the final iteration's `.trx`).
- **3rd Oct 2026**: loop fix verified on a real run. Retry at
  magnitude 1 with 10 repeats ran all ten iterations despite failures,
  committed each iteration separately, and ended red as intended (40
  rows). `PasswordReset` failed 10 of 10, the expected deterministic
  result of aborting its first request; the two validation scenarios
  passed 20 of 20. Retry at 1 is therefore now a clean
  deterministic-fail example.
  Observation to follow up: `CancelPasswordReset`, which the Retry
  fault never touches, failed in iteration 1 of this run and then
  passed nine times. Across all runs it has failed in 3 of 6 first
  iterations and 0 of 19 later ones (if three failures were scattered
  at random across those 25 executions, the chance of all landing in
  the six first-iteration slots is under 1%). Across every baseline row
  in every category, first iterations failed 2 of 33 and later ones 0 of
  229. The failure modes seen are page-settling ones (a wait for the
  "Forgotten password?" link timing out, and a click blocked by an
  overlay from the page's portal root). Hypothesis, not established: a
  warm-up effect, where the first execution of a run meets a slower REL
  or a page that has not settled, which would also fit the earlier
  episodic pattern (every recorded baseline failure was in a first
  iteration). The Latency 2000 failure at iteration 4 is a different,
  delay-driven mechanism. The sample is small and several ways of
  slicing it were tried, so this is suggestive only. The `iteration`
  column is already recorded, so it can be used as a feature or to flag
  first iterations. Whether to add an unrecorded warm-up iteration is
  undecided; the effect is itself a realistic form of flakiness (a cold
  start), so filtering it out is not obviously right.
- **3rd Oct 2026**: first full magnitude sweep with the fixed loop. All
  runs recorded every iteration. Findings per condition (failed / total
  runs of a scenario): Timing and Latency pass up to 2000, give mixed
  results at 3000, and fail deterministically from 4000 (Latency fails
  the same five scenarios in all ten iterations at both 4000 and 5000;
  Timing's valid login fails from 4000 and invalid login from 5000).
  Mixed cells at 3000: Timing valid login 1/5, Latency Cookie policy 2/5
  and Terms and conditions 2/5. Load: 750 and 1000 clean, 1250 had one
  failure (`Oldest first`, 1 iteration of 5), 1500 not run. Scenarios
  that never reach the injection point (Login field validation, Logout,
  and the navigation scenarios that do not click through a menu) never
  fail at any magnitude.
  Two runs used the wrong profile because the dropdown reset to Timing:
  a second Timing at 8000 (meant to be Latency) and Timing at 1500
  (meant to be Load). Both are valid extra Timing data, but Latency at
  8000 and Load at 1500 were not run. Latency at 8000 is not needed as
  4000 and above already fail deterministically; Load at 1500 is.
  Label counts, using full scenario name, profile and magnitude as the
  unit with at least 5 repeats: 107 conditions, of which 7 flaky, 84
  always pass and 16 always fail (up from 13 conditions and 3 flaky on
  24th Sept). Seven positives is still too few for a trustworthy
  precision and recall figure, and many of the 84 passes are scenarios
  the fault cannot affect, so they are easy negatives. Next batch aims
  to add mixed-outcome conditions around the observed band: Timing and
  Latency at 2500 and 3500 plus more repeats at 3000, and Load at 1250
  (repeat), 1500 and 1750.