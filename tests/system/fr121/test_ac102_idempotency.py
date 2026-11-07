"""FR-121 AC-102 — independently observed replay/race/partial/uncertain states."""
import copy
from concurrent.futures import ThreadPoolExecutor
import threading

from harness import (EXE, PUBLIC, GitHub, fixture, invoke, no_destructive_calls,
                     stopped, success)


def test_FR121_AC102_immutable_replay_and_race_fail_closed(tmp_path):
    request, assets = fixture(tmp_path / "input")
    for phase in ("candidate", "publish"):
        for public in (False, True):
            same = copy.deepcopy(request)
            same["phase"] = phase
            with GitHub(same, assets, public=public) as gh:
                before = gh.snapshot()
                if phase == "publish" and not public:
                    success(invoke(tmp_path / f"first-{phase}-{public}", same, gh))
                    before = gh.snapshot()
                    gh.journal.clear()
                success(invoke(tmp_path / f"repeat-{phase}-{public}", same, gh), noop=True)
                assert gh.snapshot() == before and not gh.mutations()

    for index, change in enumerate(("partial", "different-bytes", "unknown-origin",
                                     "different-evidence", "extra-asset", "different-source")):
        for phase in ("candidate", "publish"):
            for public in (False, True):
                same = copy.deepcopy(request)
                same["phase"] = phase
                with GitHub(request, assets, public=public) as gh:
                    if change == "partial":
                        del gh.assets[EXE + ".sha256"]
                    elif change == "different-bytes":
                        gh.assets[EXE] += b"unexpected"
                    elif change == "unknown-origin":
                        gh.release["body"] = ""
                    elif change == "different-evidence":
                        same["package"]["cli_sha256"] = "d" * 64
                    elif change == "extra-asset":
                        gh.assets["unapproved.txt"] = b"extra"
                    else:
                        gh.release["target_commitish"] = "d" * 40
                    before = gh.snapshot()
                    stopped(invoke(tmp_path / f"existing-{index}-{phase}-{public}", same, gh))
                    assert gh.snapshot() == before and not gh.mutations()
                    no_destructive_calls(gh)

    # Candidate failure mid-upload leaves partial state. A retry MUST stop, not repair.
    with GitHub(request, assets) as gh:
        del gh.assets[PUBLIC[-1]]
        before = gh.snapshot()
        candidate = copy.deepcopy(request)
        candidate["phase"] = "candidate"
        stopped(invoke(tmp_path / "interrupted-retry", candidate, gh))
        assert gh.snapshot() == before and not gh.mutations()

    # Simultaneous clients: one transition at most, the other is noop or explicit stop.
    for phase in ("candidate", "publish"):
        same = copy.deepcopy(request)
        same["phase"] = phase
        with GitHub(same, assets, existing=phase == "publish") as gh:
            start = threading.Barrier(2)

            def contender(number):
                # Synchronize clients, not API reads: a correct tag lock may serialize
                # the entire operation, so blocking both reads would deadlock it.
                start.wait(timeout=10)
                return invoke(tmp_path / f"race-{phase}-{number}", same, gh)

            with ThreadPoolExecutor(max_workers=2) as pool:
                futures = [pool.submit(contender, n) for n in range(2)]
                outcomes = [f.result() for f in futures]
            assert any(p.returncode == 0 for p, _ in outcomes)
            for outcome in outcomes:
                if outcome[0].returncode:
                    stopped(outcome)
                else:
                    success(outcome, noop=outcome[1]["outcome"] == "noop")
            assert gh.assets == assets
            assert len(gh.public_calls()) <= (1 if phase == "publish" else 0)
            assert len([c for c in gh.mutations()
                        if c["method"] == "POST" and c["path"].endswith("/releases")]) <= 2
            # A single unique release; no delete/recreate or asset replacement.
            no_destructive_calls(gh)

    # HTTP read failure or ambiguous publish response cannot become an unchecked success.
    for fault in ("read", "publish-response"):
        with GitHub(request, assets) as gh:
            gh.fault = fault
            outcome = invoke(tmp_path / fault, request, gh)
            if fault == "read" or outcome[0].returncode:
                stopped(outcome)
            else:
                success(outcome)
                last_patch = max(i for i, c in enumerate(gh.journal) if c["method"] == "PATCH")
                assert any(c["method"] == "GET" and "/releases/" in c["path"]
                           for c in gh.journal[last_patch + 1:]), "Reconcile uncertain server state"
                assert gh.release["draft"] is False and gh.assets == assets
            no_destructive_calls(gh)
