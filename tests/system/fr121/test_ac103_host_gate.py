"""FR-121 AC-103: native local gate is not x64 publication evidence.

Boundary: existing verify.commands[name=app-unit].run, with the standard
dotnet test --results-directory/--logger arguments forwarded unchanged.
No new API/driver is required. This contract does not certify a synthetic
package or this ARM64 host as a successful real-x64 packaging/performance run.
"""
import copy
import json
import os
import platform
import re
import subprocess
import xml.etree.ElementTree as ET

import yaml

from harness import ROOT, GitHub, fixture, invoke, stopped


def _workflow(name):
    return yaml.safe_load(
        (ROOT / ".github" / "workflows" / name).read_text(encoding="utf-8")
    )


def _required_windows_runs(workflow):
    jobs = [
        job for job in workflow["jobs"].values()
        if str(job.get("runs-on", "")).lower() in
        {"windows-latest", "windows-2022", "windows-2025"}
    ]
    assert jobs, "Real Windows x64 CI/candidate/publish runner must remain"
    runs = []
    for job in jobs:
        assert not job.get("continue-on-error", False)
        for step in job.get("steps", []):
            run = step.get("run", "")
            if run:
                # Required test/package validation must fail closed.
                if any(token in run for token in
                       ("dotnet test", "package-windows", "test-windows",
                        "build-platform-release-matrix", "prepare-release-automation")):
                    assert not step.get("continue-on-error", False)
                    assert "|| true" not in run and "exit 0" not in run.lower()
                runs.append(run)
    return "\n".join(runs)


def test_FR121_AC103_native_profile_and_x64_evidence_boundary(tmp_path):
    # FR-121 AC-103: run the configured local gate, not a duplicated selection.
    config = json.loads((ROOT / "scripts" / "ebak.config.json").read_text("utf-8"))
    entries = [
        entry for entry in config["verify"]["commands"]
        if isinstance(entry, dict) and entry.get("name") == "app-unit"
    ]
    assert len(entries) == 1, "Keep the existing app-unit verify entry"
    results = tmp_path / "local-trx"
    command = (
        entries[0]["run"]
        + f' --results-directory "{results}" --logger "trx;LogFileName=ac103.trx"'
    )
    run = subprocess.run(
        command, cwd=ROOT, shell=True, capture_output=True,
        encoding="utf-8", errors="replace", timeout=1800,
    )
    output = run.stdout + "\n" + run.stderr
    assert run.returncode == 0, output[-16000:]
    native_arch = os.environ.get(
        "PROCESSOR_ARCHITEW6432",
        os.environ.get("PROCESSOR_ARCHITECTURE", platform.machine()),
    ).lower()
    assert native_arch in {"arm64", "aarch64", "amd64", "x86_64"}, native_arch
    arm64 = native_arch in {"arm64", "aarch64"}
    assert re.search(r"\b(?:ARM64|aarch64)\b" if arm64 else r"\b(?:x64|AMD64|x86_64)\b",
                     output, re.I), "Local gate must identify its selected host profile"

    trx_files = list(results.rglob("*.trx"))
    assert trx_files, "Exit 0 or a skip-only report is not execution evidence"
    observed = [
        (node.get("testName", ""), node.get("outcome", ""))
        for path in trx_files
        for node in ET.parse(path).getroot().iter()
        if node.tag.rsplit("}", 1)[-1] == "UnitTestResult"
    ]
    # Representative existing portable Release contracts must really execute.
    for suite in ("ReleaseWorkflowContractTests", "ReleaseMatrixContractTests"):
        outcomes = [outcome for name, outcome in observed if suite in name]
        assert outcomes and all(value == "Passed" for value in outcomes), (suite, outcomes)
    assert not any(outcome in {"Failed", "Error", "Aborted"} for _, outcome in observed)
    if arm64:
        assert re.search(
            r"(?im)^(?=[^\n]*x64)(?=[^\n]*(?:NOT[_ -]?RUN|未実行))[^\n]+$", output
        ), "ARM64 success must explicitly report required x64 tests as unexecuted"
        assert not re.search(
            r"(?im)^\s*x64[^\n]*(?:status\s*[:=]\s*PASS|publishable\s*[:=]\s*true)",
            output,
        ), "Unexecuted x64 validation must not become PASS/publication evidence"

    # Preserve the actual x64 CI/candidate/protected-publish gate wiring.
    ci = _required_windows_runs(_workflow("ci.yml"))
    candidate = _required_windows_runs(_workflow("release.yml"))
    publish = _required_windows_runs(_workflow("publish-release.yml"))
    assert "dotnet test" in ci and "package-windows-singlefile.ps1" in ci
    for token in ("dotnet test", "release_test_selection.py",
                  "package-windows-singlefile.ps1", "test-windows-singlefile.ps1"):
        assert token in candidate, f"Required x64 candidate validation lost: {token}"
    assert "build-platform-release-matrix.ps1" in publish
    assert "-CleanHostEvidencePath" in publish and "-Mode Final" in publish
    readme = (ROOT / "README.md").read_text("utf-8")
    assert re.search(r"ARM64", readme, re.I)
    assert re.search(r"NOT[_ -]?RUN|未実行", readme, re.I)
    assert not re.search(
        r"StudyReportEvaluator-win-arm64\.(?:exe|zip)",
        ci + candidate + publish,
        re.I,
    ), "AC-103 adds no ARM64 distribution"

    # Negative: a locally successful portable gate cannot authorize publication
    # when representative required x64 package/clean-host evidence is NOT_RUN.
    request, assets = fixture(tmp_path / "input")
    for index, group in enumerate(("package", "clean_host")):
        bad = copy.deepcopy(request)
        statuses = bad[group]["required" if group == "package" else "tests"]
        for key in statuses:
            statuses[key] = "NOT_RUN"
        with GitHub(bad, assets) as gh:
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"not-run-{index}", bad, gh))
            assert not gh.public_calls()
            assert gh.snapshot() == before
