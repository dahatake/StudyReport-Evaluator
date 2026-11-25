"""FR-121 AC-028 — §13.6/13.7; synthetic EXEs are never executed."""
import copy
import json

from pathlib import Path

from harness import (EXE, ZIP, KINDS, PUBLIC, REQUIRED, GitHub, digest, fixture, invoke,
                     downloaded_names, no_destructive_calls, stopped, success)
from runtime_harness import runtime_contract


def test_FR121_AC028_exact_bytes_and_closed_required_gate(tmp_path):
    request, assets = fixture(tmp_path / "input")
    # Positive: all required tests PASS.
    with GitHub(request, assets) as gh:
        result = success(invoke(tmp_path / "valid", request, gh))
        assert len(gh.public_calls()) == 1
        assert set(gh.assets) == set(PUBLIC)
        assert gh.assets == assets
        assert downloaded_names(gh) == set(PUBLIC), "Verify downloaded bytes, not API digest alone"
        matrix = json.loads(Path(result["matrix_path"]).read_text(encoding="utf-8-sig"))
        assert matrix["schemaVersion"] == 2
        rows = matrix["rows"]
        assert len(rows) == 3 and {r["artifactKind"] for r in rows} == set(KINDS)
        for row in rows:
            msix = row["artifactKind"] == "windows-development-msix"
            assert row["publish"] is (not msix)
            assert row["status"] == ("PASS_MECHANISM" if msix else "PASS_REQUIRED")
        no_destructive_calls(gh)

    # FR-121 AC-028: a new local candidate may share a policy-selected product
    # version, but cannot reuse package proof for older ZIP bytes.
    # Only the local fake GitHub is mutated. No real candidate is published.
    for index, change in enumerate(("zip-bytes",)):
        fresh = copy.deepcopy(request)
        remote = copy.deepcopy(assets)
        name = ZIP
        remote[name] += b"new candidate bytes"
        remote[name + ".sha256"] = f"{digest(remote[name])}  {name}\n".encode()
        for filename, value in remote.items():
            path = tmp_path / f"revised-{index}" / filename
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(value)
            fresh["asset_paths"][filename] = str(path)
        with GitHub(fresh, remote) as gh:
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"stale-proof-{index}", fresh, gh))
            assert not gh.public_calls()
            assert gh.snapshot() == before

    # Every required package ID is fail-closed, not just one representative flag.
    mutations = []
    for group, keys in (("package", REQUIRED),):
        for key in keys:
            # Check every required key for FAIL/missing; other status classes once
            # per gate, avoiding a redundant full Cartesian product.
            statuses = ("FAIL", None, "NOT_RUN", "cancelled", "skipped") if key == keys[0] else ("FAIL", None)
            for status in statuses:
                mutations.append((f"{group}-{key}-{status}", group, key, status))
    for index, (_, group, key, status) in enumerate(mutations):
        bad = copy.deepcopy(request)
        statuses = bad[group]["required"]
        if status is None:
            del statuses[key]
        else:
            statuses[key] = status
        # Match the altered evidence stamp: refusal must be due to the gate,
        # not an incidental provenance-body mismatch.
        with GitHub(bad, assets) as gh:
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"required-{index}", bad, gh))
            assert not gh.public_calls()
            assert gh.snapshot() == before

    for index, change in enumerate(("missing-sidecar", "bad-sidecar", "changed-remote-bytes",
                                     "unexpected-public-msix", "candidate-package-fail")):
        bad = copy.deepcopy(request)
        remote = copy.deepcopy(assets)
        if change == "missing-sidecar":
            del remote[EXE + ".sha256"]
        elif change == "bad-sidecar":
            remote[EXE + ".sha256"] = b"0" * 64 + b"  " + EXE.encode() + b"\n"
        elif change == "changed-remote-bytes":
            remote[EXE] = b"NZ" + remote[EXE][2:]  # Same size; API digest below lies.
        elif change == "unexpected-public-msix":
            remote["internal.unsigned.test.msix"] = b"must not publish"
        else:
            bad["phase"] = "candidate"
            bad["package"]["required"]["msix-mechanism"] = "FAIL"
        with GitHub(request, remote, existing=bad["phase"] != "candidate") as gh:
            gh.reported_digests = {n: digest(v) for n, v in assets.items()}
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"boundary-{index}", bad, gh))
            assert not gh.public_calls()
            assert gh.snapshot() == before
            no_destructive_calls(gh)


def test_FR121_AC030_AC033_bundled_runtime_and_authentication_system_boundary(tmp_path):
    # One build/session per profile. Never real login/AI proof.
    runtime_contract(tmp_path / "runtime")
