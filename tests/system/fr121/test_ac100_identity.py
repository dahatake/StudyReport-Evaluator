"""FR-121 AC-100 — latest means resolved main at candidate start, not at publish."""
import copy

from harness import (COMMIT, REPO, TAG_OBJECT, GitHub, binding, canonical, fixture, invoke,
                     no_destructive_calls, stopped, success)


def test_FR121_AC100_identity_snapshot_and_no_rebuild(tmp_path):
    request, assets = fixture(tmp_path / "input")
    candidate = copy.deepcopy(request)
    candidate["phase"] = "candidate"
    candidate["clean_host"] = None  # Candidate cannot pretend CH has already run.
    with GitHub(candidate, assets, existing=False) as gh:
        result = success(invoke(tmp_path / "candidate", candidate, gh))
        assert result["identity"]["main_tip_at_start"] == COMMIT
        assert result["identity"]["tag_object_sha"] == TAG_OBJECT
        assert gh.release["draft"] is True and gh.assets == assets
        assert any(c["path"] in (f"/repos/{REPO}/commits/main",
                                f"/repos/{REPO}/git/ref/heads/main") for c in gh.journal)
        assert not gh.public_calls()
        assert not result.get("matrix_path"), "Candidate is non-publishable before exact-EXE CH"

    # main moving AFTER verification does not invalidate or silently rebuild the candidate.
    with GitHub(request, assets) as gh:
        gh.main = "d" * 40
        success(invoke(tmp_path / "main-advanced", request, gh))
        assert gh.assets == assets and len(gh.public_calls()) == 1
        assert not [v for v in gh.mutations() if v["method"] != "PATCH"]

    changes = ("old-main", "lightweight-tag", "moved-tag", "replaced-tag-object",
               "checkout", "package-commit", "package-run", "package-repo", "run-repo",
               "run-workflow", "run-commit", "run-failed", "run-cancelled", "run-skipped",
               "run-incomplete", "product", "exe-version", "zip-version",
               "version-check", "changelog-check", "prerelease", "missing-tag")
    for index, change in enumerate(changes):
        bad = copy.deepcopy(request)
        with GitHub(request, assets) as gh:
            if change == "old-main":
                bad["phase"] = "candidate"
                gh.main = "d" * 40
            elif change == "lightweight-tag":
                gh.annotated = False
            elif change == "moved-tag":
                gh.tag_commit = "d" * 40
            elif change == "replaced-tag-object":
                gh.tag_object = "e" * 40  # Same commit, different annotated tag object.
            elif change == "checkout":
                bad["checkout_commit"] = "d" * 40
            elif change.startswith("package-"):
                key = {"package-commit": "commit", "package-run": "run_id",
                       "package-repo": "repository"}[change]
                bad["package"]["identity"][key] = "wrong"
            elif change.startswith("run-"):
                attr, value = {
                    "run-repo": ("run_repository", "other/repo"),
                    "run-workflow": ("run_workflow", ".github/workflows/ci.yml"),
                    "run-commit": ("run_commit", "d" * 40),
                    "run-failed": ("run_conclusion", "failure"),
                    "run-cancelled": ("run_conclusion", "cancelled"),
                    "run-skipped": ("run_conclusion", "skipped"),
                    "run-incomplete": ("run_status", "in_progress"),
                }[change]
                setattr(gh, attr, value)
            elif change == "product":
                bad["product_version"] = "0.8.2"
            elif change in ("exe-version", "zip-version"):
                bad["package"][change.split("-")[0] + "_product_version"] = "0.8.2"
            elif change == "version-check":
                bad["version_verified"] = False
            elif change == "changelog-check":
                bad["changelog_verified"] = False
            else:
                bad["identity"]["tag"] = "v0.8.1-rc.1" if change == "prerelease" else "v9.9.9"
            # Isolate identity validation from incidental release stamp mismatch.
            gh.release["body"] = canonical(binding(bad)).decode()
            before = gh.snapshot()
            stopped(invoke(tmp_path / f"identity-{index}", bad, gh))
            assert gh.snapshot() == before and not gh.public_calls()
            no_destructive_calls(gh)
