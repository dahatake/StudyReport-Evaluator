"""system_test.prebuild: ケース実行前に 1 回だけ走り、失敗なら exit 2 で中断する。"""
import argparse
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))

import ledger  # noqa: E402


def _args(tmp_path):
    return argparse.Namespace(
        cases="C1", select="changed", base=None, canary_first=False, stop_on_canary_fail=False,
        max_minutes=0, case_timeout=60, jobs="1", no_record=True, no_batch=True,
        results=str(tmp_path / "results.json"), rerun_failed=False)


def _setup(monkeypatch, tmp_path, cases, prebuild_rc):
    calls = []
    data = {"version": 1, "ac_digests": {}, "cases": cases}
    monkeypatch.setattr(ledger.h, "load_ledger", lambda root, cfg: data)

    def fake_shell(cmd, cwd, timeout):
        calls.append(cmd)
        if cmd == "PRE":
            return prebuild_rc, "build output"
        return 0, "ok"

    monkeypatch.setattr(ledger, "run_shell", fake_shell)
    monkeypatch.setattr(ledger.h, "apply_resource_env", lambda root, cfg: None)
    return calls


CFG = {**ledger.h.load_config(Path(__file__).resolve().parents[2]), "system_test": {"prebuild": "PRE"}}
CASE = {"id": "C1", "command": "CASE1", "status": "not_run", "ac_ids": []}


def test_prebuild_failure_aborts_with_exit_2(monkeypatch, tmp_path, capsys):
    calls = _setup(monkeypatch, tmp_path, [dict(CASE)], 1)
    rc = ledger.cmd_run(_args(tmp_path), tmp_path, CFG)
    assert rc == 2
    assert calls == ["PRE"]
    assert "prebuild 失敗のため中断" in capsys.readouterr().out
    assert list((tmp_path).rglob("prebuild-*.log"))


def test_prebuild_runs_once_before_cases(monkeypatch, tmp_path):
    calls = _setup(monkeypatch, tmp_path, [dict(CASE)], 0)
    ledger.cmd_run(_args(tmp_path), tmp_path, CFG)
    assert calls[0] == "PRE" and calls.count("PRE") == 1 and "CASE1" in calls


def test_prebuild_not_run_without_targets(monkeypatch, tmp_path):
    calls = _setup(monkeypatch, tmp_path, [{"id": "C1", "status": "not_run", "ac_ids": []}], 0)
    ledger.cmd_run(_args(tmp_path), tmp_path, CFG)
    assert "PRE" not in calls
