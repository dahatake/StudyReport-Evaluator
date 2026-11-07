"""Private production adapter for FR-121. Inputs are existing validated artifacts.

Snapshot is taken before building. Candidate observations are persisted in an
immutable run artifact and consumed unchanged at publication. Relocate only
changes local paths after Actions artifact transport, never package identity.
"""
import argparse
import copy
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

from release_automation import (CH, EXE, ZIP, PUBLIC, REQUIRED, WORKFLOW, GitHub,
                                descriptor, read_json, require, write_json)
from release_test_selection import GROUPS, REQUIRED_X64_METHODS, required_group

ROOT = Path(__file__).resolve().parents[1]


def command(*args):
    process = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=600)
    require(process.returncode == 0, "required local validator failed")
    return process.stdout.strip()


def git(*args):
    return command("git", "--no-pager", *args)


def verify_version(tag):
    command("pwsh", "-NoLogo", "-NoProfile", "-File", str(ROOT / "dev/version.ps1"),
            "verify", "-Tag", tag)
    require(re.search(rf"(?m)^## \[{re.escape(tag[1:])}\] - \d{{4}}-\d{{2}}-\d{{2}}\s*$",
                      (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")), "missing dated changelog")


def validate_controls(root, identity, final=False):
    parameter = "-MatrixPath" if final else "-CandidateRecordPath"
    name = "platform-release-matrix.json" if final else "release-candidate-record.json"
    response = command("pwsh", "-NoLogo", "-NoProfile", "-File",
                       str(ROOT / "scripts/validate-platform-release-matrix.ps1"),
                       parameter, str(root / name), "-ArtifactDirectory", str(root),
                       "-ExpectedProductVersion", identity["product_version"],
                       "-ExpectedSourceCommit", identity["commit"],
                       "-ExpectedRepository", identity["repository"],
                       "-ExpectedCandidateRunId", identity["run_id"])
    import json
    result = json.loads(response)
    require(result["status"] == ("PASS" if final else "PASS_CANDIDATE") and result["rowCount"] == 3,
            "C02 did not validate the exact closed candidate")


def deterministic_results(directory):
    """Require actual passing Core/App executions, not a workflow-input flag."""
    groups = {group: [] for group in GROUPS}
    native = {name: [] for name in REQUIRED_X64_METHODS}
    for path in directory.rglob("*.trx"):
        root = ET.parse(path).getroot()
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        methods = {v.attrib["id"]: v.find("t:TestMethod", ns).attrib
                   for v in root.findall(".//t:UnitTest", ns)}
        for result in root.findall(".//t:UnitTestResult", ns):
            method = methods.get(result.attrib["testId"], {})
            group = required_group(method.get("className", ""), method.get("name", ""))
            name = f"{method.get('className', '')}.{method.get('name', '')}"
            if name in native:
                native[name].append(result.attrib["outcome"])
            if group is not None:
                groups[group].append(result.attrib["outcome"])
    for outcomes in groups.values():
        require(outcomes and all(value == "Passed" for value in outcomes),
                "Core/App required tests missing, skipped or failed")
    for outcomes in native.values():
        require(outcomes and all(value == "Passed" for value in outcomes),
                "Required real x64 startup/performance TRX evidence missing, skipped or failed")


def copy_public(source, destination):
    for name in PUBLIC:
        shutil.copyfile(source / name, destination / name)
    return {name: str((destination / name).resolve()) for name in PUBLIC}


def relocate(directory):
    path = directory / "request.json"
    request = read_json(path)
    request["asset_paths"] = {name: str((directory / name).resolve()) for name in PUBLIC}
    if request["phase"] == "publish":
        request["final_matrix_path"] = str((directory / "platform-release-matrix.json").resolve())
    write_json(path, request)


def prepare(args):
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if args.mode == "relocate":
        relocate(output)
        return
    tag = os.environ["RELEASE_TAG"]
    require(re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", tag),
            "stable tag required")
    if args.mode == "snapshot":
        api = GitHub(os.environ.get("GITHUB_API_URL", "https://api.github.com"))
        repository = os.environ["GITHUB_REPOSITORY"]
        main, _ = api.call("GET", f"/repos/{repository}/commits/main")
        ref, _ = api.call("GET", f"/repos/{repository}/git/ref/tags/{tag}")
        require(ref["object"]["type"] == "tag", "annotated tag required")
        tag_object, _ = api.call("GET", f"/repos/{repository}/git/tags/{ref['object']['sha']}")
        commit = git("rev-parse", "HEAD")
        require(main["sha"] == tag_object["object"]["sha"] == commit and
                tag_object["object"]["type"] == "commit" and tag_object["tag"] == tag,
                "release source is not main tip at candidate start")
        identity = {"repository": repository, "workflow": WORKFLOW, "run_id": os.environ["GITHUB_RUN_ID"],
                    "tag": tag, "tag_object_sha": ref["object"]["sha"], "commit": commit,
                    "main_tip_at_start": main["sha"], "product_version": tag[1:]}
        write_json(output / "identity.json", identity)
        return
    verify_version(tag)
    root = args.artifacts.resolve()
    if args.mode == "candidate":
        identity = read_json(args.snapshot)
        require(identity["tag"] == tag and identity["run_id"] == os.environ["GITHUB_RUN_ID"] and
                identity["repository"] == os.environ["GITHUB_REPOSITORY"] and
                identity["commit"] == git("rev-parse", "HEAD"), "snapshot/checkout changed")
        validate_controls(root, identity)
        deterministic_results(args.results)
        record = read_json(root / "release-candidate-record.json")
        require(record["requiredTests"] == "PASS", "required package tests failed")
        rows = {r["artifactKind"]: r for r in record["rows"]}
        exe = read_json(root / rows["windows-singlefile-exe"]["evidence"]["fileName"])
        zip_evidence = read_json(root / rows["windows-zip"]["evidence"]["fileName"])
        msix = read_json(root / rows["windows-development-msix"]["evidence"]["fileName"])
        require(exe["status"] == zip_evidence["status"] == "PASS_REQUIRED" and
                msix["status"] == "PASS_MECHANISM", "package/mechanism status failed")
        require(all(v is True for v in exe["checks"].values()) and
                all(v is True for v in zip_evidence["checks"].values()), "package check incomplete")
        runtime = exe["runtime"]
        package = {
            "identity": copy.deepcopy(identity), "required": dict.fromkeys(REQUIRED, "PASS"),
            "assets": [descriptor(name, (root / name).read_bytes()) for name in PUBLIC],
            "exe_product_version": exe["productVersion"], "zip_product_version": zip_evidence["productVersion"],
            "sdk_version": runtime["dotnetSdk"], "runtime_version": runtime["dotnetRuntime"],
            "cli_version": runtime["cliVersion"], "cli_sha256": runtime["cliSha256"].lower(),
            "msix": {"publish": False, "status": msix["status"],
                     "artifact": rows["windows-development-msix"]["artifact"],
                     "sidecar": rows["windows-development-msix"]["sidecar"]},
            # Bind *all* original C03 evidence, not merely the projected booleans.
            "candidate_record": descriptor(record_name := "release-candidate-record.json",
                                           (root / record_name).read_bytes()),
        }
        request = {"phase": "candidate", "identity": identity, "checkout_commit": git("rev-parse", "HEAD"),
                   "recorded_start": copy.deepcopy(identity),
                   "product_version": tag[1:], "version_verified": True, "changelog_verified": True,
                   "protected_approval": False, "package": package, "clean_host": None,
                   "asset_paths": copy_public(root, output)}
    else:
        request = read_json(args.candidate)
        identity = request["identity"]
        require(identity["tag"] == tag and identity["run_id"] == os.environ["CANDIDATE_RUN_ID"] and
                identity["repository"] == os.environ["GITHUB_REPOSITORY"] and
                identity["commit"] == git("rev-parse", "HEAD"), "candidate/checkout mismatch")
        validate_controls(root, identity, final=True)
        require(request["package"]["candidate_record"] ==
                descriptor("release-candidate-record.json", (root / "release-candidate-record.json").read_bytes()),
                "original candidate record changed")
        clean = read_json(root / "windows-singlefile-clean-host.evidence.json")
        require(clean["candidate"] == {"repository": identity["repository"],
                "workflow": WORKFLOW, "runId": identity["run_id"]} and
                clean["sourceCommit"] == identity["commit"], "clean-host candidate mismatch")
        tests = {v["id"]: v["status"] for v in clean["tests"]}
        host = clean["host"]
        request.update(phase="publish", checkout_commit=git("rev-parse", "HEAD"),
                       product_version=tag[1:], version_verified=True, changelog_verified=True,
                       # Authority is the protected writer job, never this observation alone.
                       protected_approval=True,
                       asset_paths=copy_public(root, output),
                       final_matrix_path=str(output / "platform-release-matrix.json"),
                       clean_host={"identity": copy.deepcopy(identity), "exe": clean["package"],
                                   "product_version": clean["productVersion"],
                                   "tests": {key: tests[key] for key in CH},
                                   "host": {"os": host["osName"], "architecture": host["architecture"],
                                            "fresh": host["fresh"], "standard_user": host["standardUser"],
                                            "extra_dependencies": any(host["additionalDependencies"].values())}})
        shutil.copyfile(root / "platform-release-matrix.json", output / "platform-release-matrix.json")
    write_json(output / "request.json", request)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", required=True, choices=("snapshot", "candidate", "publish", "relocate"))
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--artifacts", type=Path)
    parser.add_argument("--snapshot", type=Path)
    parser.add_argument("--candidate", type=Path)
    parser.add_argument("--results", type=Path)
    try:
        prepare(parser.parse_args())
    except Exception:
        # Evidence/HTTP responses may contain private content. Do not print them.
        raise SystemExit("Release observation validation failed; no mutation attempted.") from None
