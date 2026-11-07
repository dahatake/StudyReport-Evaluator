# FR-121 System Test — implementation handoff

Oracle: approved FR-121 and §3.2, 11.10, 13.1, 13.6, 13.7
(current revision: run `202611071752`). These ACs are now explicitly system-level;
the original verification-level activation conflict has already been resolved.
Existing `ReleaseWorkflowContractTests` / `ReleaseMatrixContractTests` remain
regressions; this suite adds observable orchestration, not another matrix validator.

## CH03 correction increment: AC-028 / AC-030 / AC-033

IT-006 is reused, retaining its command and canary flag, and extending its AC set
to AC-028/030/033. No duplicate ledger case is added. Both pytest functions in
`test_ac028_gate.py` must pass; C# test discovery/results must be nonempty and all
three named system methods must have Passed TRX results (skip is not success).

`runtime_harness.py` builds a local tripwire CLI once and the existing UI test
project once per compilation profile, in private temporary artifact directories.
`CopilotCliBinaryPath` supplies only that local executable; no CLI download,
credential read, real login, browser launch, real SDK Start/Ping or AI is needed.
Distribution compilation uses `CopilotSkipCliDownload=false`, development uses
`true`. This exercises the existing App/SDK build-property seam, not a new
production CLI abstraction. The tripwire's version is CLI 1.0.79, **not** a
constraint on the policy-selected product/candidate version. Public NuGet restore
is permitted during compilation; runtime processes get refusal proxies and no
ambient token variables. This is not an OS network sandbox.

Coverage within the existing case:

| AC | Observations and negative boundaries |
|---|---|
| AC-028 | Existing closed required gates retained. Same product version with changed EXE bytes and stale CH proof; changed ZIP bytes with stale package proof; mismatched source; CH-06 PASS claim with CH-01–05 NOT_RUN all stop without public mutation. |
| AC-030 | Default SDK/login composition chooses validated absolute bundled path, not fake PATH; distribution manifest deletion with fake PATH still rejects through **both** default consumers. Choice must be compiled/configured independently of manifest existence. Explicit real resolver rejects missing/corrupt CLI/manifest and RID/SDK/CLI-version/path/hash mismatch before SDK creation or login start. Development with a manifest still uses PATH. |
| AC-033 | Observe default authentication/normal/reference/special SDK consumer factories against the same CLI path/hash, without creating sessions. Real authentication service → execution boundary → startup state handles available/valid unauthenticated/Start/Ping failure/timeout/incompatibility/invalid auth/cancellation. Only valid unauthenticated auto-starts login; suppression leaves manual command usable. Process exit is not authentication; normal completion rechecks auth/models, manual check can observe later authentication. One-shot startup cannot restart login. Owned-process cancellation and overlapping-login refusal; no AI run. One headless GUI remains usable and loads synthetic Excel/navigates design after cancellation. |

Private field reflection only finds the **existing** login resolver and SDK
factory wiring; expected paths, refusal and states come from the ACs. It does not
assert private field names or introduce an alternative resolver policy.

### Real candidate acceptance — still required, never certified by fake PASS

After implementation, build a **new local** candidate using the existing ADR
version policy, keeping the old v0.8.7/6AF… candidate untouched. Record actual
source, chosen product version, final EXE/ZIP bytes, sizes, SHA-256 and sidecars.
Run existing real packaging checks plus IT-006 against that source. Record all
CH-01–06 against the exact new EXE; a historical PASS or user CH-06 statement is
not new evidence. Real Start/Ping is CH-03, user authentication is CH-06, real AI
is ADV-01. The current-native synthetic fixture (including ARM64 hosts) proves
none of those real-x64 outcomes. Missing/failed/cancelled/skipped CH remains
NOT_RUN/failed in the actual evidence; fake TRX must never populate it. The
run's product version is intentionally not pinned here. No actual Release,
push, deployment or publication is authorized by this test design.

## Production entry point (to implement)

```powershell
pwsh -NoLogo -NoProfile -File scripts/invoke-release-automation.ps1 `
  -RequestPath request.json -ResultPath result.json -GitHubApiUrl http://127.0.0.1:<ephemeral>
```

The existing candidate and protected publish workflows must use this **same
production orchestration**, not a separate test-only policy. No new workflow,
storage service, PAT, real Release, push, Actions dispatch or deployment is needed.
The tests do not implement this entry point. Missing implementation fails at test
execution, not collection. Runtime prerequisites: Python, pytest, PyYAML, PowerShell 7.

`harness.fixture()` defines the normalized adapter request. This is a dependency
seam for observations from existing build/package/version validators, NOT a new
public evidence schema and NOT a replacement for their closed JSON validation.
Production must derive these observations from actual build outputs/validators;
accepting arbitrary request booleans from a public workflow input is not allowed.
Synthetic EXE/ZIP bytes are never executed and do not demonstrate CH-01–06.
The existing P07/C02/C03/package/clean-host validators and required tests must still
run against real artifacts, preserving closed matrix v2 and the existing MSIX
descriptor-only boundary. No clean-host automation or new install scope is added.

### Input fields

- `phase`: `candidate` or `publish`.
- `identity`: repository, workflow, run_id, tag, annotated tag object SHA, source
  commit, main_tip_at_start, product_version. Resolve main through GitHub at
  candidate start; record it once. At publish compare the recorded tag object and
  commit, checkout, run metadata, evidence and EXE/ZIP versions. Do not compare to
  a newly advanced main or rebuild at publish.
- `checkout_commit`, `product_version`, `version_verified`, `changelog_verified`:
  observations from existing checkout / `dev/version.ps1 verify` / CHANGELOG gates.
- `asset_paths`: four exact final local files; `package`: candidate-bound required
  test observations, artifact descriptors, EXE/ZIP product versions, tool versions,
  internal MSIX descriptor/status. `clean_host`: candidate-bound EXE descriptor,
  host boundary, CH-01–06 and optional advisory observations. Null clean-host is
  valid for candidate only. Candidate must not emit a publishable final matrix.
- `protected_approval`: observation of the existing protected `publish` environment,
  **not** a substitute for that environment. In production, the publish job only
  runs after approval; an untrusted request cannot grant itself approval.

### Fake GitHub transport

`GitHubApiUrl` is the injected base for **every** GitHub call. The in-process HTTP
server implements ordinary GitHub REST subsets: main commit/ref, annotated tag
ref/object, Actions run metadata, release by tag/id, asset list/download/upload,
release create/PATCH. See `GitHub.route` for exact paths. Its downloads carry actual
fixture bytes, independent of the API digest fields. No `gh` fallback is permitted.
API 404 means absence; 503/unknown state is not absence. The fake deliberately
allows unsafe DELETE/PATCH and records attempts; safety is enforced by assertions,
not by a fake backend that silently makes dangerous clients safe.

Release `body` is a canonical JSON provenance stamp from `harness.binding(request)`:
candidate identity, SHA-256 of canonical normalized package evidence, and four
asset descriptors. It binds reuse to the candidate/evidence as well as bytes.
Do not treat matching names, count, release body or API digests alone as proof:
download all four assets and compare exact bytes/sidecars. No internal evidence,
matrix, secret, MSIX binary or unmeasured production claim is a public asset.

Create a draft only after required candidate checks; publish only after all gates.
Reuse complete matching draft/public results without writes. Missing/unknown/
mismatched existing state stops without repair/delete/overwrite. Candidate never
publishes. Public releases never become draft. Concurrency must serialize mutations
for the same repository/tag across candidate/publish; `cancel-in-progress: false`.
Concurrent callers may return unchanged success or explicit conflict stop.
Read failures stop. A lost publish response either stops with context or reconciles
the server state and all assets before reporting success.

### Result (always write, including refusal)

UTF-8 JSON with `outcome` (`success`, `noop`, `stopped`), `tag`, `commit`,
`candidate_run_id`, nonempty `stage`; `reason` is nonempty for `stopped`.
Exit 0 only for confirmed success/noop; nonzero for stopped.
Candidate success includes recorded `identity`; no final `matrix_path`.
Publish success includes absolute `matrix_path` to the **existing schemaVersion 2**
matrix (its three exact artifact kinds, publish flags and required statuses are
asserted). Internal evidence/matrix outputs belong under the request's temporary
directory, not the repository or Release.

The harness clears inherited credentials, uses synthetic credentials and a `gh`
refusal shim, points GitHub and proxy URLs to loopback, and uses no paid services.
This is an offline integration seam, not an OS network sandbox; production must
honor the injected transport and never invoke external endpoints.

## Independent commands / minimal cases

```text
python -m pytest tests/system/fr121/test_ac028_gate.py -q
python -m pytest tests/system/fr121/test_ac100_identity.py -q
python -m pytest tests/system/fr121/test_ac101_authority.py -q
python -m pytest tests/system/fr121/test_ac102_idempotency.py -q
```

One ledger case per independently executable boundary; IT-006 groups three
related ACs. Decision-table rows loop inside each case, avoiding a ledger
explosion. Files are separate because the four
independently executable contracts isolate gate, identity, authority and mutable
remote-state failures. AC-028 is the canary. All scenarios use private temporary
data and ephemeral loopback ports; no serial execution is required.

Workflow YAML assertions check effective job permissions, protected environment,
untrusted input transport, absence of new PAT dependencies and real-driver wiring.
GitHub environment protection configuration itself is external and cannot be
proven by a local YAML test; it remains a protected deployment prerequisite.
