"""FR-121: independent HTTP observer; synthetic bytes are NEVER clean-host proof."""
from __future__ import annotations

import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlsplit
import zipfile

ROOT = Path(__file__).resolve().parents[3]
REPO = "fixture-owner/fixture-repo"
TAG = "v0.8.1"
COMMIT = "a" * 40
TAG_OBJECT = "b" * 40
RUN = "71001"
EXE = "StudyReportEvaluator-win-x64.exe"
ZIP = "StudyReportEvaluator-win-x64.zip"
PUBLIC = (EXE, EXE + ".sha256", ZIP, ZIP + ".sha256")
KINDS = ("windows-singlefile-exe", "windows-zip", "windows-development-msix")
CH = tuple(f"CH-{n:02}" for n in range(1, 7))
REQUIRED = ("core", "app", "exe-package", "zip-package", "zip-clean-extract",
            "zip-launch", "zip-cli", "zip-data-protection", "msix-mechanism")


def digest(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def canonical(value) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def descriptor(name, value):
    return {"fileName": name, "bytes": len(value), "sha256": digest(value)}


def fixture(directory: Path):
    """Normalized observations at the existing validator/orchestrator boundary."""
    directory.mkdir(parents=True)
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w") as archive:
        info = zipfile.ZipInfo("StudyReportEvaluator.App.exe", (2026, 1, 1, 0, 0, 0))
        archive.writestr(info, b"MZ synthetic folder app; never execute")
    assets = {EXE: b"MZ synthetic single-file 0.8.1; never execute", ZIP: stream.getvalue()}
    for name in (EXE, ZIP):
        assets[name + ".sha256"] = f"{digest(assets[name])}  {name}\n".encode()
    paths = {}
    for name, value in assets.items():
        path = directory / name
        path.write_bytes(value)
        paths[name] = str(path)
    identity = {
        "repository": REPO, "workflow": ".github/workflows/release.yml",
        "run_id": RUN, "tag": TAG, "tag_object_sha": TAG_OBJECT,
        "commit": COMMIT, "main_tip_at_start": COMMIT, "product_version": "0.8.1",
    }
    clean = {
        "identity": copy.deepcopy(identity), "exe": descriptor(EXE, assets[EXE]),
        "product_version": "0.8.1", "tests": dict.fromkeys(CH, "PASS"),
        "advisory": {"ADV-01": "NOT_RUN", "ADV-02": "NOT_RUN"},
        "host": {"os": "Windows 11", "architecture": "x64", "fresh": True,
                 "standard_user": True, "extra_dependencies": False},
    }
    package = {
        "identity": copy.deepcopy(identity), "required": dict.fromkeys(REQUIRED, "PASS"),
        "assets": [descriptor(n, assets[n]) for n in PUBLIC],
        "exe_product_version": "0.8.1", "zip_product_version": "0.8.1",
        "sdk_version": "10.0.400", "runtime_version": "10.0.11",
        "cli_version": "synthetic-fixed", "cli_sha256": "c" * 64,
        "msix": {"publish": False, "status": "PASS_MECHANISM",
                 "artifact": descriptor("internal.unsigned.test.msix", b"internal-only"),
                 "sidecar": descriptor("internal.unsigned.test.msix.sha256", b"metadata")},
    }
    request = {
        "phase": "publish", "identity": identity, "checkout_commit": COMMIT,
        "product_version": "0.8.1", "version_verified": True,
        "changelog_verified": True, "protected_approval": True,
        "asset_paths": paths, "package": package, "clean_host": clean,
    }
    return request, assets


def binding(request):
    return {
        "candidate": request["identity"],
        "package_sha256": digest(canonical(request["package"])),
        "assets": request["package"]["assets"],
    }


class GitHub:
    """Tiny fake GitHub REST API with an independent, append-only mutation journal.

    It does not enforce the release policy: unsafe PATCH/DELETE/overwrite requests
    are recorded and applied, so a client cannot pass just because the fake rejects
    a dangerous operation. Only GitHub's unique tag/asset and ETag races are modeled.
    """
    def __init__(self, request, assets, *, existing=True, public=False):
        self.lock = threading.RLock()
        self.journal = []
        self.assets = copy.deepcopy(assets) if existing else {}
        self.release = self.release_value(request, public) if existing else None
        self.main = COMMIT
        self.tag_commit = COMMIT
        self.tag_object = TAG_OBJECT
        self.annotated = True
        self.run_repository = REPO
        self.run_workflow = ".github/workflows/release.yml"
        self.run_commit = COMMIT
        self.run_status = "completed"
        self.run_conclusion = "success"
        self.fault = None
        self.reported_digests = {}
        self.etag = 1
        self.server = None

    @staticmethod
    def release_value(request, public=False):
        return {"id": 42, "tag_name": TAG, "target_commitish": COMMIT,
                "draft": not public, "prerelease": False,
                "body": canonical(binding(request)).decode()}

    def snapshot(self):
        with self.lock:
            return copy.deepcopy((self.release, self.assets))

    def mutations(self):
        return [v for v in self.journal if v["method"] not in ("GET", "HEAD")]

    def public_calls(self):
        return [v for v in self.journal if v["method"] == "PATCH"
                and isinstance(v["body"], dict) and v["body"].get("draft") is False]

    def response_release(self):
        value = copy.deepcopy(self.release)
        value["assets"] = [self.asset_info(n) for n in self.assets]
        value["upload_url"] = self.url + f"/repos/{REPO}/releases/42/assets{{?name,label}}"
        return value

    def asset_info(self, name):
        ident = list(self.assets).index(name) + 1
        return {"id": ident, "name": name, "size": len(self.assets[name]),
                "digest": "sha256:" + self.reported_digests.get(name, digest(self.assets[name])),
                "url": self.url + f"/repos/{REPO}/releases/assets/{ident}",
                "browser_download_url": self.url + f"/download/{name}"}

    def __enter__(self):
        state = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                self.handle_request("GET")

            def do_POST(self):
                self.handle_request("POST")

            def do_PATCH(self):
                self.handle_request("PATCH")

            def do_DELETE(self):
                self.handle_request("DELETE")

            def handle_request(self, method):
                size = int(self.headers.get("Content-Length", 0))
                raw = self.rfile.read(size)
                try:
                    body = json.loads(raw) if raw else None
                except (ValueError, UnicodeDecodeError):
                    body = None
                with state.lock:
                    path = unquote(urlsplit(self.path).path)
                    state.journal.append({"method": method, "path": path, "body": body})
                    status, payload = state.route(method, path, self.path, body, raw, self.headers)
                    data = payload if isinstance(payload, bytes) else canonical(payload)
                    etag = state.etag
                self.send_response(status)
                self.send_header("Content-Type", "application/octet-stream" if isinstance(payload, bytes)
                                 else "application/json")
                self.send_header("Content-Length", str(len(data)))
                self.send_header("ETag", f'"{etag}"')
                self.end_headers()
                self.wfile.write(data)

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.url = f"http://127.0.0.1:{self.server.server_port}"
        self.thread = threading.Thread(
            target=lambda: self.server.serve_forever(poll_interval=0.02), daemon=True)
        self.thread.start()
        return self

    def __exit__(self, *_):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)

    def route(self, method, path, query, body, raw, headers):
        prefix = f"/repos/{REPO}"
        if self.fault == "read" and method == "GET":
            return 503, {"message": "synthetic communication failure"}
        if method == "GET":
            if path in (prefix + "/commits/main", prefix + "/git/ref/heads/main"):
                return 200, {"sha": self.main, "object": {"type": "commit", "sha": self.main}}
            if path == prefix + "/git/ref/tags/" + TAG:
                return 200, {"ref": "refs/tags/" + TAG, "object": {
                    "type": "tag" if self.annotated else "commit",
                    "sha": self.tag_object if self.annotated else self.tag_commit}}
            if path == prefix + "/git/tags/" + self.tag_object:
                return 200, {"tag": TAG, "sha": self.tag_object,
                             "object": {"type": "commit", "sha": self.tag_commit}}
            if path == prefix + "/actions/runs/" + RUN:
                return 200, {"id": int(RUN), "path": self.run_workflow,
                             "repository": {"full_name": self.run_repository},
                             "head_sha": self.run_commit, "status": self.run_status,
                             "conclusion": self.run_conclusion, "event": "workflow_dispatch"}
            if path in (prefix + "/releases/tags/" + TAG, prefix + "/releases/42"):
                return (200, self.response_release()) if self.release else (404, {"message": "Not Found"})
            if path == prefix + "/releases/42/assets":
                return 200, [self.asset_info(n) for n in self.assets]
            for name in self.assets:
                if path in (f"/download/{name}",
                            prefix + f"/releases/assets/{self.asset_info(name)['id']}"):
                    return 200, self.assets[name]
        if method == "POST" and path == prefix + "/releases":
            if self.release:
                return 422, {"message": "already_exists"}
            self.release = {"id": 42, **body}
            self.etag += 1
            return 201, self.response_release()
        if method == "POST" and path == prefix + "/releases/42/assets":
            name = parse_qs(urlsplit(query).query).get("name", [""])[0]
            if name in self.assets:
                return 422, {"message": "already_exists"}
            self.assets[name] = raw
            return 201, self.asset_info(name)
        if method == "PATCH" and path == prefix + "/releases/42":
            if not self.release:
                return 404, {"message": "Not Found"}
            if headers.get("If-Match") not in (None, f'"{self.etag}"'):
                return 412, {"message": "ETag conflict"}
            self.release.update(body)
            self.etag += 1
            if self.fault == "publish-response":
                return 503, {"message": "response lost after server applied change"}
            return 200, self.response_release()
        if method == "DELETE":
            if "/releases/assets/" in path:
                ident = int(path.rsplit("/", 1)[1])
                for name in list(self.assets):
                    if self.asset_info(name)["id"] == ident:
                        del self.assets[name]
                        break
            elif path == prefix + "/releases/42":
                self.release, self.assets = None, {}
            return 200, {}
        return 404, {"message": f"Unexpected fake API call: {method} {path}"}


def invoke(directory: Path, request, github: GitHub):
    """Use production's shared driver, never a policy implementation in this test."""
    directory.mkdir(parents=True)
    entry = ROOT / "scripts" / "invoke-release-automation.ps1"
    assert entry.is_file(), (
        "FR-121 production driver missing: see tests/system/fr121/CONTRACT.md; "
        "implement the shared workflow orchestration, not a test-only oracle")
    request_path = directory / "request.json"
    result_path = directory / "result.json"
    request_path.write_bytes(canonical(request))
    # No inherited credentials, real gh fallback, paid APIs or remote GitHub host.
    allow = {"PATH", "PATHEXT", "SYSTEMROOT", "WINDIR", "COMSPEC", "TEMP", "TMP",
             "USERPROFILE", "HOME", "APPDATA", "LOCALAPPDATA", "PROGRAMFILES",
             "PROGRAMFILES(X86)", "PROGRAMDATA", "PSMODULEPATH", "DOTNET_ROOT"}
    env = {k: v for k, v in os.environ.items() if k.upper() in allow}
    shim = directory / "bin"
    shim.mkdir()
    (shim / "gh.cmd").write_text("@echo off\r\nexit /b 97\r\n", encoding="ascii")
    env.update({
        "PATH": str(shim) + os.pathsep + env.get("PATH", ""),
        "GITHUB_API_URL": github.url, "GITHUB_SERVER_URL": github.url,
        "GITHUB_REPOSITORY": REPO, "GH_TOKEN": "synthetic-not-a-credential",
        "HTTP_PROXY": github.url, "HTTPS_PROXY": github.url, "ALL_PROXY": github.url,
        "NO_PROXY": "127.0.0.1,localhost",
        "STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE": "0",
    })
    proc = subprocess.run(
        ["pwsh", "-NoLogo", "-NoProfile", "-File", str(entry),
         "-RequestPath", str(request_path), "-ResultPath", str(result_path),
         "-GitHubApiUrl", github.url],
        cwd=ROOT, env=env, capture_output=True, text=True, timeout=90, shell=False)
    assert result_path.is_file(), f"Driver omitted result (exit {proc.returncode}): {proc.stderr}"
    result = json.loads(result_path.read_text(encoding="utf-8-sig"))
    assert result["tag"] == request["identity"]["tag"]
    assert result["commit"] == request["identity"]["commit"]
    assert str(result["candidate_run_id"]) == request["identity"]["run_id"]
    assert isinstance(result["stage"], str) and result["stage"].strip()
    assert "synthetic-not-a-credential" not in proc.stdout + proc.stderr + result_path.read_text()
    return proc, result


def success(outcome, *, noop=False):
    proc, result = outcome
    assert proc.returncode == 0, (result, proc.stderr)
    assert result["outcome"] == ("noop" if noop else "success"), result
    return result


def stopped(outcome):
    proc, result = outcome
    assert proc.returncode != 0, result
    assert result["outcome"] == "stopped", result
    assert isinstance(result["reason"], str) and result["reason"].strip(), result


def no_destructive_calls(github):
    assert not [v for v in github.journal if v["method"] == "DELETE"]
    assert not [v for v in github.journal if v["method"] == "PATCH"
                and v["body"].get("draft") is True]


def downloaded_names(github):
    names = set()
    for call in github.journal:
        if call["method"] != "GET":
            continue
        for name in github.assets:
            if call["path"] in (f"/download/{name}",
                               f"/repos/{REPO}/releases/assets/{github.asset_info(name)['id']}"):
                names.add(name)
    return names
