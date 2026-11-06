"""FR-121 unit regressions complement the independent fake-GitHub contracts."""
import copy
import json
from pathlib import Path
import sys
import subprocess

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import release_automation as driver
import prepare_release_automation as adapter


@pytest.fixture
def observation(tmp_path, monkeypatch):
    identity = {"repository": "owner/repository", "workflow": driver.WORKFLOW,
                "run_id": "123", "tag": "v1.2.3", "tag_object_sha": "b" * 40,
                "commit": "a" * 40, "main_tip_at_start": "a" * 40, "product_version": "1.2.3"}
    monkeypatch.setenv("GITHUB_REPOSITORY", identity["repository"])
    assets = {driver.EXE: b"unit-only executable observation", driver.ZIP: b"unit-only archive observation"}
    for name in (driver.EXE, driver.ZIP):
        assets[name + ".sha256"] = f"{driver.sha(assets[name])}  {name}\n".encode()
    paths = {}
    for name, value in assets.items():
        path = tmp_path / name
        path.write_bytes(value)
        paths[name] = str(path)
    package = {"identity": copy.deepcopy(identity), "required": dict.fromkeys(driver.REQUIRED, "PASS"),
               "assets": [driver.descriptor(name, assets[name]) for name in driver.PUBLIC],
               "exe_product_version": "1.2.3", "zip_product_version": "1.2.3",
               "msix": {"publish": False, "status": "PASS_MECHANISM"}}
    request = {"phase": "candidate", "identity": identity, "checkout_commit": "a" * 40,
               "product_version": "1.2.3", "version_verified": True, "changelog_verified": True,
               "asset_paths": paths, "package": package, "clean_host": None}
    return request


# FR-121 AC-028: the normalizer boundary is not permission to accept other bytes.
def test_candidate_exact_descriptors_and_uppercase_existing_c03_hashes(observation):
    for value in observation["package"]["assets"]:
        value["sha256"] = value["sha256"].upper()
    assert set(driver.validate_request(observation)) == set(driver.PUBLIC)
    observation["package"]["assets"][0]["bytes"] += 1
    with pytest.raises(driver.Stop, match="bytes/evidence"):
        driver.validate_request(observation)


# FR-121 AC-028/AC-100: truthy strings do not substitute for validator results.
@pytest.mark.parametrize("field", ["version_verified", "changelog_verified"])
@pytest.mark.parametrize("value", [False, "true", 1, None])
def test_validator_observations_are_boolean(observation, field, value):
    observation[field] = value
    with pytest.raises(driver.Stop):
        driver.validate_request(observation)


# FR-121 AC-100: stable source metadata remains bound across the transport seam.
def test_snapshot_is_not_redefined_when_main_advances(observation):
    observation["recorded_start"] = copy.deepcopy(observation["identity"])

    class API:
        def call(self, method, path):
            assert not path.endswith("/commits/main")
            i = observation["identity"]
            if "/git/ref/tags/" in path:
                return {"object": {"type": "tag", "sha": i["tag_object_sha"]}}, None
            if "/git/tags/" in path:
                return {"tag": i["tag"], "sha": i["tag_object_sha"],
                        "object": {"type": "commit", "sha": i["commit"], "url": "api metadata"}}, None
            return {"id": 123, "path": driver.WORKFLOW + "@refs/tags/v1.2.3",
                    "repository": {"full_name": i["repository"]}, "head_sha": i["commit"],
                    "event": "workflow_dispatch", "status": "in_progress", "conclusion": None}, None

    driver.verify_identity(API(), observation, snapshot=True)
    observation["recorded_start"]["commit"] = "d" * 40
    with pytest.raises(driver.Stop, match="recorded"):
        driver.verify_identity(API(), observation, snapshot=True)


# FR-121 AC-101: credentials cannot follow a server-supplied arbitrary upload URL.
def test_transport_rejects_non_github_origins(monkeypatch):
    monkeypatch.setenv("GH_TOKEN", "unit-not-a-secret")
    api = driver.GitHub("http://127.0.0.1:12345")
    with pytest.raises(driver.Stop, match="untrusted API URL"):
        api.call("POST", "https://example.invalid/upload", b"private bytes")
    with pytest.raises(driver.Stop):
        driver.GitHub("https://example.invalid")


# FR-121 AC-102: duplicate JSON is ambiguous provenance, not last-write-wins.
def test_duplicate_provenance_fields_stop(tmp_path):
    path = tmp_path / "ambiguous.json"
    path.write_text('{"identity":{"commit":"one","commit":"two"}}')
    with pytest.raises(driver.Stop, match="duplicate"):
        driver.read_json(path)


# FR-121 AC-028: Core/App must both have actual executed PASS results.
@pytest.mark.parametrize("bad", ["missing-app", "NotExecuted", "Failed"])
def test_production_trx_gate_refuses_missing_or_nonpassing_groups(tmp_path, bad):
    classes = [("core", "StudyReportEvaluator.Core.Tests.Required", "Passed"),
               ("app", "StudyReportEvaluator.App.Tests.Required", "Passed")]
    if bad == "missing-app":
        classes.pop()
    else:
        classes[1] = ("app", classes[1][1], bad)
    definitions = "".join(f'<UnitTest id="{key}"><TestMethod className="{name}"/></UnitTest>'
                          for key, name, _ in classes)
    results = "".join(f'<UnitTestResult testId="{key}" outcome="{status}"/>'
                      for key, _, status in classes)
    (tmp_path / "required.trx").write_text(
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
        f"<TestDefinitions>{definitions}</TestDefinitions><Results>{results}</Results></TestRun>")
    with pytest.raises(driver.Stop, match="Core/App"):
        adapter.deterministic_results(tmp_path)


# FR-121 AC-100/AC-102: relocation never rewrites candidate identity or evidence.
def test_actions_transport_changes_paths_only(tmp_path, observation):
    original = copy.deepcopy(observation)
    driver.write_json(tmp_path / "request.json", observation)
    adapter.relocate(tmp_path)
    actual = driver.read_json(tmp_path / "request.json")
    assert actual["identity"] == original["identity"]
    assert actual["package"] == original["package"]
    assert actual["asset_paths"] == {name: str((tmp_path / name).resolve()) for name in driver.PUBLIC}


# FR-121 AC-100/AC-101: execute the actual production PowerShell handoff, not
# merely a Python helper, and transport a path containing shell metacharacters.
def test_powershell_transport_uses_argument_data(tmp_path, observation):
    directory = tmp_path / "space & 'quoted' observations"
    directory.mkdir()
    driver.write_json(directory / "request.json", observation)
    process = subprocess.run(
        ["pwsh", "-NoLogo", "-NoProfile", "-File", str(ROOT / "scripts/prepare-release-automation.ps1"),
         "-Mode", "Relocate", "-OutputDirectory", str(directory)],
        capture_output=True, text=True, timeout=30)
    assert process.returncode == 0, process.stderr
    result = driver.read_json(directory / "request.json")
    assert result["identity"] == observation["identity"]
    assert result["asset_paths"][driver.EXE] == str((directory / driver.EXE).resolve())


# FR-121 AC-028: production must invoke existing closed C02 with explicit identity.
def test_adapter_uses_existing_c02_before_projecting(observation, tmp_path, monkeypatch):
    calls = []

    def command(*args):
        calls.append(args)
        return json.dumps({"status": "PASS_CANDIDATE", "rowCount": 3})

    monkeypatch.setattr(adapter, "command", command)
    adapter.validate_controls(tmp_path, observation["identity"])
    args = calls[0]
    assert str(ROOT / "scripts/validate-platform-release-matrix.ps1") in args
    assert "-CandidateRecordPath" in args
    assert "-ExpectedRepository" in args and "-ExpectedCandidateRunId" in args
    monkeypatch.setattr(adapter, "command", lambda *args: '{"status":"FAIL","rowCount":3}')
    with pytest.raises(driver.Stop):
        adapter.validate_controls(tmp_path, observation["identity"])
