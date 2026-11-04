"""FR-116 ローカル対照評価の System Test（契約: tests/system/fr116/CONTRACT.md）。

期待値は要求定義書 FR-116 の受入基準と §7.5 の指標定義から、実装とは独立に計算する（独立 oracle）。
合成データのみを使い、実 AI・credential・外部書込・課金 API を使わない。
"""

from __future__ import annotations

import copy
import hashlib
import json
import os
import shlex
import subprocess
import unicodedata
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
DATA = Path(__file__).resolve().parent / "data"
DATASET = DATA / "control-dataset-v1.json"
SPLIT = DATA / "split-v1.json"
RULES = DATA / "rules-v1.json"

LABELS = ("追加指示なし", "追加指示あり", "抜粋", "編集", "自力")
POSITIVE_LABELS = {"追加指示なし", "追加指示あり", "抜粋", "編集"}
CLASSES = {"要確認", "非該当", "保留"}
COUNT_KEYS = ("tp", "fp", "tn", "fn", "hold", "n", "tpr", "fpr")
CREDENTIAL_ENV = (
    "GH_TOKEN", "GITHUB_TOKEN", "COPILOT_GITHUB_TOKEN", "OPENAI_API_KEY",
    "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_ENDPOINT", "AZURE_CLIENT_SECRET",
)


# ---------- 独立 oracle（§7.5） ----------

def normalize(text: str) -> str:
    text = unicodedata.normalize("NFKC", text).lower()
    return "".join(
        ch for ch in text
        if not ch.isspace() and unicodedata.category(ch)[0] not in ("Z", "P", "S", "C")
    )


def longest_common_substring(a: str, b: str) -> int:
    best = 0
    prev = [0] * (len(b) + 1)
    for i in range(1, len(a) + 1):
        cur = [0] * (len(b) + 1)
        for j in range(1, len(b) + 1):
            if a[i - 1] == b[j - 1]:
                cur[j] = prev[j - 1] + 1
                best = max(best, cur[j])
        prev = cur
    return best


def oracle_metrics(answer: str | None, reference: str) -> dict | None:
    if answer is None or answer.strip() == "":
        return None
    s, r = normalize(answer), normalize(reference)
    if not s:
        return None
    lcs = longest_common_substring(s, r)
    return {
        "raw_exact": answer == reference,
        "normalized_exact": s == r,
        "student_coverage": round(lcs / len(s), 4),
        "reference_coverage": round(lcs / len(r), 4) if r else 0.0,
    }


def oracle_class(metric_value, threshold) -> str:
    if metric_value is None or threshold is None:
        return "保留"
    if isinstance(metric_value, bool):
        metric_value = 1 if metric_value else 0
    return "要確認" if metric_value >= threshold else "非該当"


def oracle_counts(rows: list[tuple[str, str]]) -> dict:
    """rows: (expected 対照陽性/対照陰性, classification)。保留は正解に数えない。"""
    c = {"tp": 0, "fp": 0, "tn": 0, "fn": 0, "hold": 0, "n": len(rows)}
    for expected, cls in rows:
        if cls == "保留":
            c["hold"] += 1
        elif expected == "対照陽性":
            c["tp" if cls == "要確認" else "fn"] += 1
        else:
            c["fp" if cls == "要確認" else "tn"] += 1
    c["tpr"] = None if c["tp"] + c["fn"] == 0 else c["tp"] / (c["tp"] + c["fn"])
    c["fpr"] = None if c["fp"] + c["tn"] == 0 else c["fp"] / (c["fp"] + c["tn"])
    return c


# ---------- 実行 ----------

def tool_command() -> list[str]:
    override = os.environ.get("SRE_CONTROL_EVAL_CMD")
    if override:
        return shlex.split(override, posix=os.name != "nt")
    return ["dotnet", "run", "--project", "src/StudyReportEvaluator.ControlEvaluation", "--"]


def run_tool(tmp: Path, name: str, *, dataset: Path = DATASET, split: Path = SPLIT,
             rules: Path = RULES) -> tuple[int, dict, str]:
    out = tmp / name
    out.mkdir(parents=True, exist_ok=True)
    env = {k: v for k, v in os.environ.items() if k not in CREDENTIAL_ENV}
    env["COPILOT_CLI_PATH"] = str(tmp / "no-copilot-cli.exe")  # 実 AI へ到達できない
    cmd = tool_command() + ["--dataset", str(dataset), "--split", str(split),
                            "--rules", str(rules), "--output", str(out)]
    proc = subprocess.run(cmd, cwd=ROOT, env=env, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", timeout=900, check=False)
    log = f"{cmd!r}\nexit={proc.returncode}\n{proc.stdout}\n{proc.stderr}"
    record_path = out / "evaluation-record.json"
    assert record_path.is_file(), f"評価記録が出力されない（評価不成立でも記録を書く）\n{log}"
    return proc.returncode, json.loads(record_path.read_text(encoding="utf-8")), log


def write_json(path: Path, value) -> Path:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def baseline(tmp_path_factory):
    tmp = tmp_path_factory.mktemp("fr116")
    code, record, log = run_tool(tmp, "base")
    return {"tmp": tmp, "code": code, "record": record, "log": log}


def items_by_id(record: dict) -> dict:
    return {it["id"]: it for it in record["items"]}


# ---------- core: AC-086 / AC-087 / AC-088 ----------

# FR-116 AC-086: ラベル別データ・作成方法・元回答との関係・グループ・データ版／hash を確認できる
def test_fr116_core_ac086_labels_creation_relation_group_and_dataset_hash(baseline):
    record, log = baseline["record"], baseline["log"]
    assert baseline["code"] == 0, log
    assert record["status"] == "評価済み", log
    dataset = load(DATASET)
    assert record["dataset_version"] == dataset["dataset_version"]
    assert record["dataset_sha256"] == sha256(DATASET)
    groups = {q["id"]: q["group"] for q in dataset["questions"]}
    got = items_by_id(record)
    assert set(got) == {it["id"] for it in dataset["items"]}
    for src in dataset["items"]:
        it = got[src["id"]]
        for key in ("label", "creation_method", "relation", "source_item_id", "question_id"):
            assert it[key] == src[key], (src["id"], key)
        assert it["group"] == groups[src["question_id"]]
    assert {it["label"] for it in record["items"]} == set(LABELS)


# FR-116 AC-087: 同一設問の派生回答は同じ側。manifest・順序／seed を記録。空 holdout・重複・漏れ・holdout 調整は評価不成立
def test_fr116_core_ac087_split_manifest_and_invalid_splits(baseline):
    record = baseline["record"]
    split = load(SPLIT)
    for key in ("tuning_groups", "holdout_groups", "seed", "order"):
        assert record["split"][key] == split[key], key
    assert record["split_sha256"] == sha256(SPLIT)
    got = items_by_id(record)
    for it in record["items"]:
        expected_partition = "holdout" if it["group"] in split["holdout_groups"] else "tuning"
        assert it["partition"] == expected_partition, it["id"]
        if it["source_item_id"]:
            assert got[it["source_item_id"]]["partition"] == it["partition"], "派生回答が両群へ漏れた"

    tmp: Path = baseline["tmp"]
    empty = write_json(tmp / "split-empty.json", {**split, "tuning_groups": ["G1", "G2", "G3"], "holdout_groups": []})
    overlap = write_json(tmp / "split-overlap.json", {**split, "holdout_groups": ["G2", "G3"]})
    tuned = write_json(tmp / "rules-tuned-on-holdout.json", {**load(RULES), "tuned_on_groups": ["G1", "G2", "G3"]})
    leaked = copy.deepcopy(load(DATASET))
    for it in leaked["items"]:
        if it["id"] == "G3-EXC":
            it["source_item_id"] = "G1-NONE"  # holdout の派生回答が調整群の回答を元にする
    leaked_path = write_json(tmp / "dataset-leak.json", leaked)

    for name, kwargs in (("empty", {"split": empty}), ("overlap", {"split": overlap}),
                         ("tuned", {"rules": tuned}), ("leak", {"dataset": leaked_path})):
        code, bad, log = run_tool(tmp, f"invalid-{name}", **kwargs)
        assert code != 0, f"{name}: 評価不成立は非 0 で終了する\n{log}"
        assert bad["status"] == "評価不成立", name
        assert bad["invalid_reasons"], name


# FR-116 AC-088: 対照陽性／陰性の対応、要確認／非該当／保留、tp/fp/tn/fn/hold/n と分母を全体・ラベル別・分割別に報告
def test_fr116_core_ac088_classification_and_confusion_counts_by_label_and_partition(baseline):
    record = baseline["record"]
    dataset = load(DATASET)
    refs = {q["id"]: q["references"][0] for q in dataset["questions"]}
    rules = {r["id"]: r for r in load(RULES)["rules"]}
    assert set(record["counts"]) == set(rules)

    for it in record["items"]:
        assert it["expected"] == ("対照陽性" if it["label"] in POSITIVE_LABELS else "対照陰性"), it["id"]
        assert set(it["classifications"]) == set(rules)
        assert set(it["classifications"].values()) <= CLASSES
        src = next(s for s in dataset["items"] if s["id"] == it["id"])
        expected_metrics = oracle_metrics(src["answer"], refs[src["question_id"]])
        if expected_metrics is None:
            assert it["metrics"] is None or all(v is None for v in it["metrics"].values()), it["id"]
            assert set(it["classifications"].values()) == {"保留"}, f"{it['id']}: 欠損は保留"
            continue
        for key, value in expected_metrics.items():
            assert it["metrics"][key] == pytest.approx(value, abs=1e-4), (it["id"], key)
        legacy = it["metrics"]["legacy_composite"]
        assert 0 <= legacy <= 1
        if expected_metrics["normalized_exact"]:
            assert legacy == pytest.approx(1.0)
        for rid, rule in rules.items():
            metric = rule.get("metric")
            known = metric in ("legacy_composite", "student_coverage", "reference_coverage", "normalized_exact")
            value = it["metrics"][metric] if known else None
            want = oracle_class(value, rule.get("threshold"))
            assert it["classifications"][rid] == want, (it["id"], rid)

    for rid in rules:
        def rows(pred):
            return [(it["expected"], it["classifications"][rid]) for it in record["items"] if pred(it)]
        scopes = [("overall", record["counts"][rid]["overall"], rows(lambda _: True))]
        for label in LABELS:
            scopes.append((label, record["counts"][rid]["by_label"][label], rows(lambda it, l=label: it["label"] == l)))
        for part in ("tuning", "holdout"):
            scopes.append((part, record["counts"][rid]["by_partition"][part],
                           rows(lambda it, p=part: it["partition"] == p)))
        for scope, actual, scope_rows in scopes:
            want = oracle_counts(scope_rows)
            for key in COUNT_KEYS:
                if want[key] is None:
                    assert actual[key] is None, f"{rid}/{scope}/{key}: 分母なしの率は未算出"
                else:
                    assert actual[key] == pytest.approx(want[key]), (rid, scope, key)
    # ルール欠損・不正は保留（正解に数えない）
    for rid in ("R-NOTHRESHOLD", "R-UNKNOWN"):
        overall = record["counts"][rid]["overall"]
        assert overall["hold"] == overall["n"] and overall["tpr"] is None and overall["fpr"] is None


# ---------- record: AC-089 / AC-090 ----------

def strip_volatile(record: dict) -> dict:
    record = copy.deepcopy(record)
    record.pop("generated_at", None)
    record.pop("output_path", None)
    return record


# FR-116 AC-089: 同条件のオフライン再実行で同じ結果。hash・分割・条件・比較版・ルール・結果を記録。従来値と改善診断を並べる
def test_fr116_record_ac089_offline_rerun_is_reproducible_and_self_describing(baseline):
    record = baseline["record"]
    assert baseline["code"] == 0, baseline["log"]
    _, again, log = run_tool(baseline["tmp"], "rerun")
    assert strip_volatile(again) == strip_volatile(record), "同じデータ・分割・ルールで結果が変わった"
    assert record["rules_sha256"] == sha256(RULES)
    assert record["rules"] == load(RULES)["rules"]
    assert isinstance(record["conditions"], dict)
    versions = record["comparison_versions"]
    assert versions["legacy"] and versions["improved"] and versions["legacy"] != versions["improved"]
    for it in record["items"]:
        if it["metrics"] is not None and it["metrics"].get("student_coverage") is not None:
            # 同じ対照群で、従来の単独参照合成値と改善した診断（全文／部分／被覆率）を並べる
            for key in ("legacy_composite", "raw_exact", "normalized_exact", "student_coverage", "reference_coverage"):
                assert key in it["metrics"], (it["id"], key)


# FR-116 AC-090: 限界の明記、holdout 結果を隠さない、確率・不正判定を持たない、学生の点数・保存済み結果を変えない
def test_fr116_record_ac090_limitations_and_no_side_effects(tmp_path):
    results_dir = tmp_path / "saved-results"
    results_dir.mkdir()
    saved = results_dir / "student-scores.xlsx"
    saved.write_bytes(b"PK\x03\x04 synthetic saved results - must stay unchanged")
    inputs = [DATASET, SPLIT, RULES, saved]
    before = {p: sha256(p) for p in inputs}

    code, record, log = run_tool(tmp_path, "limits")
    assert code == 0, log
    text = record["limitations"]
    for phrase in ("合成対照データでの評価", "実学生について未検証", "履歴", "非決定", "短文", "見逃し", "model", "設問"):
        assert phrase in text, f"limitations に「{phrase}」がない"
    holdout = record["counts"]["R-REFCOV"]["by_partition"]["holdout"]
    assert holdout["n"] == sum(1 for it in record["items"] if it["partition"] == "holdout")

    flat = json.dumps(record, ensure_ascii=False).lower()
    for forbidden in ("probability", "確率\":", "cheat", "不正判定\":", "ai_use"):
        assert forbidden not in flat, f"評価記録に {forbidden} の項目を持たない"
    assert {p: sha256(p) for p in inputs} == before, "入力・保存済み結果を変更した"
    assert sorted(x.name for x in results_dir.iterdir()) == ["student-scores.xlsx"]
