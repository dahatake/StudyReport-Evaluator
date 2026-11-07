"""FR-121 AC-101 — workflow authority and untrusted inputs; no external writes."""
import copy
import re

import yaml

from harness import ROOT, GitHub, fixture, invoke, stopped


def load(name):
    return yaml.safe_load((ROOT / ".github" / "workflows" / name).read_text(encoding="utf-8"))


def permissions(workflow, job):
    value = job.get("permissions", workflow.get("permissions", {}))
    assert isinstance(value, dict), "No read-all/write-all permissions"
    assert not {k for k, v in value.items() if v == "write"} - {"contents"}
    return value


def test_FR121_AC101_least_privilege_approval_and_input_boundary(tmp_path):
    # Permissions are job-scoped: a build job sharing a release writer token is not safe.
    mutation_groups = []
    for name in ("ci.yml", "release.yml", "publish-release.yml"):
        workflow = load(name)
        assert isinstance(workflow.get("permissions", {}), dict)
        assert not [v for v in workflow.get("permissions", {}).values() if v == "write"]
        seen_driver = False
        for job in workflow["jobs"].values():
            steps = job.get("steps", [])
            perms = permissions(workflow, job)
            writers = []
            readers = []
            for step in steps:
                script = step.get("run", "")
                # All untrusted expressions must go through env/typed action arguments.
                assert not re.search(r"\$\{\{\s*(?:inputs\.|github\.event\.)", script)
                text = script + "\n" + str(step.get("uses", ""))
                if "invoke-release-automation.ps1" in script:
                    seen_driver = True
                if re.search(r"gh\s+(?:api[^\n]*(?:POST|PATCH|DELETE)|release\s+(?:create|upload|edit|delete))",
                             script, re.I) or "invoke-release-automation.ps1" in script:
                    writers.append(step)
                if re.search(r"gh\s+run|actions/runs|download-artifact", text, re.I):
                    readers.append(step)
                assert not re.search(r"\$\{\{\s*secrets\.(?!GITHUB_TOKEN\b)[^}]+\}\}", str(step)), (
                    "No new PAT/secret dependency in release automation")
            builds = any(re.search(
                r"dotnet\s+(?:build|test|publish)|(?:publish|package|test)-windows|"
                r"build-platform-release-matrix", s.get("run", ""), re.I) for s in steps)
            if name == "ci.yml" or builds:
                assert "write" not in perms.values(), "Build/validation must not inherit Release write"
            if perms.get("contents") == "write":
                assert writers, "Release write authority must be limited to a mutation job"
                concurrency = job.get("concurrency", workflow.get("concurrency", {}))
                assert isinstance(concurrency, dict) and concurrency.get("cancel-in-progress") is False
                group = concurrency.get("group", "")
                assert "inputs.tag" in group, "Serialize by target tag, not candidate run"
                assert not re.search(r"github\.(?:workflow|run_id)|candidate_run_id", group)
                mutation_groups.append(group)
            if readers:
                assert perms.get("actions") == "read"
            if perms.get("actions") == "read":
                # The shared driver also verifies a different candidate run.
                assert readers or any("invoke-release-automation.ps1" in s.get("run", "") for s in steps)
            if name == "publish-release.yml" and writers:
                environment = job.get("environment")
                assert (environment.get("name") if isinstance(environment, dict) else environment) == "publish"
        if name != "ci.yml":
            assert seen_driver, "Real workflows must call the same driver exercised against fake GitHub"
    assert len(mutation_groups) >= 2
    assert len(set(mutation_groups)) == 1, "Candidate/publish must share the same per-tag serialization"

    request, assets = fixture(tmp_path / "input")
    denied = copy.deepcopy(request)
    denied["protected_approval"] = False
    with GitHub(request, assets) as gh:
        before = gh.snapshot()
        stopped(invoke(tmp_path / "unapproved", denied, gh))
        assert gh.snapshot() == before and not gh.public_calls()
    # Data, not shell text: none of these tag values may create the sentinel.
    marker = tmp_path / "INJECTION_OCCURRED"
    for index, tag in enumerate((
        f"v0.8.1'; Set-Content -LiteralPath '{marker}' injected; #",
        f'v0.8.1$(New-Item -ItemType File -Path "{marker}")',
        "v0.8.1\n--draft=false", "../v0.8.1", "v0.8.1;echo do-not-execute",
    )):
        bad = copy.deepcopy(request)
        bad["identity"]["tag"] = tag
        with GitHub(request, assets) as gh:
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"injection-{index}", bad, gh))
            assert not marker.exists()
            assert not gh.public_calls() and gh.snapshot() == before
