"""FR-121 AC-028/100/101/102: fail-closed, non-replacing Release transaction.

The normalized request is an internal adapter boundary, not public evidence.
Production derives it only after P07/C02/C03 and deterministic tests succeed.
No shell, gh fallback, rebuild, delete, asset replacement or draft reset exists.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
import urllib.error
import urllib.parse
import urllib.request

EXE = "StudyReportEvaluator-win-x64.exe"
ZIP = "StudyReportEvaluator-win-x64.zip"
PUBLIC = (EXE, EXE + ".sha256", ZIP, ZIP + ".sha256")
KINDS = ("windows-singlefile-exe", "windows-zip", "windows-development-msix")
REQUIRED = ("core", "app", "exe-package", "zip-package", "zip-clean-extract",
            "zip-launch", "zip-cli", "zip-data-protection", "msix-mechanism")
CH = tuple(f"CH-{n:02}" for n in range(1, 7))
WORKFLOW = ".github/workflows/release.yml"


class Stop(Exception):
    pass


def require(condition, reason):
    if not condition:
        raise Stop(reason)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def sha(value):
    return hashlib.sha256(value).hexdigest()


def descriptor(name, value):
    return {"fileName": name, "bytes": len(value), "sha256": sha(value)}


def strict_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON property")
        result[key] = value
    return result


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=strict_object)


def write_json(path, value):
    Path(path).write_bytes(canonical(value) + b"\n")


@contextmanager
def transaction_lock(api, identity):
    """Serialize local callers too; Actions concurrency serializes runner hosts.

    An ETag rejection is not a substitute: even a rejected second PATCH is an
    unnecessary mutation attempt. Always reread state after obtaining the lock.
    """
    key = sha(canonical([api, identity.get("repository"), identity.get("tag")]))
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateMutexW.argtypes = [ctypes.c_void_p, wintypes.BOOL, wintypes.LPCWSTR]
        kernel.CreateMutexW.restype = wintypes.HANDLE
        kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        kernel.WaitForSingleObject.restype = wintypes.DWORD
        kernel.ReleaseMutex.argtypes = [wintypes.HANDLE]
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        handle = kernel.CreateMutexW(None, False, "Local\\StudyReportEvaluator.Release." + key)
        require(bool(handle), "release lock unavailable")
        held = False
        try:
            status = kernel.WaitForSingleObject(handle, 60000)
            require(status in (0, 0x80), "release transaction busy")
            held = True
            yield
        finally:
            if held:
                kernel.ReleaseMutex(handle)
            kernel.CloseHandle(handle)
    else:
        import fcntl
        # Persistent inode: unlinking a lock file would allow a third caller to
        # lock a different inode while an earlier waiter still holds the old one.
        with open(Path(tempfile.gettempdir()) / ("sre-release-" + key + ".lock"), "a+b") as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Stop("release transaction busy") from None
            try:
                yield
            finally:
                fcntl.flock(lock, fcntl.LOCK_UN)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class GitHub:
    def __init__(self, base):
        self.base = base.rstrip("/")
        url = urllib.parse.urlsplit(self.base)
        require(not url.username and not url.password and not url.query and not url.fragment,
                "invalid API endpoint")
        require(self.base == "https://api.github.com" or
                (url.scheme == "http" and url.hostname in ("127.0.0.1", "localhost") and
                 url.path == ""), "API must be GitHub or injected loopback transport")
        self.opener = urllib.request.build_opener(NoRedirect())
        self.token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
        require(bool(self.token), "GitHub token unavailable")

    def call(self, method, path, body=None, *, binary=False, absent=False, etag=None):
        url = self.base + path if path.startswith("/") else path
        origin = urllib.parse.urlsplit(url)
        base_origin = urllib.parse.urlsplit(self.base)
        require((origin.scheme, origin.netloc) == (base_origin.scheme, base_origin.netloc) or
                (self.base == "https://api.github.com" and origin.scheme == "https" and
                 origin.netloc == "uploads.github.com"), "untrusted API URL")
        headers = {"Authorization": "Bearer " + self.token,
                   "Accept": "application/octet-stream" if binary else "application/vnd.github+json",
                   "X-GitHub-Api-Version": "2022-11-28",
                   "User-Agent": "StudyReportEvaluator-release"}
        if etag:
            headers["If-Match"] = etag
        data = body if isinstance(body, bytes) else canonical(body) if body is not None else None
        if data is not None:
            headers["Content-Type"] = "application/octet-stream" if isinstance(body, bytes) else "application/json"
        request = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            response = self.opener.open(request, timeout=60)
        except urllib.error.HTTPError as error:
            if absent and error.code == 404:
                error.close()
                return None, None
            # Asset API redirects are followed without credentials, never to arbitrary hosts.
            if binary and method == "GET" and error.code == 302 and self.base == "https://api.github.com":
                location = error.headers.get("Location", "")
                target = urllib.parse.urlsplit(location)
                require(target.scheme == "https" and target.hostname == "release-assets.githubusercontent.com"
                        and not target.username and not target.password, "unsafe asset redirect")
                error.close()
                try:
                    response = self.opener.open(location, timeout=60)
                except Exception:
                    raise Stop("asset download failed") from None
            else:
                code = error.code
                error.close()
                raise Stop(f"GitHub {method} failed (HTTP {code}); state not confirmed") from None
        except Exception:
            raise Stop(f"GitHub {method} communication failed; state not confirmed") from None
        with response:
            value = response.read()
            return (value if binary else json.loads(value, object_pairs_hook=strict_object),
                    response.headers.get("ETag"))


def validate_request(request):
    identity = request["identity"]
    require(request["phase"] in ("candidate", "publish"), "invalid phase")
    require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9][A-Za-z0-9._-]*",
                         identity["repository"]), "invalid repository")
    require(identity["repository"] == os.environ.get("GITHUB_REPOSITORY"), "repository boundary mismatch")
    require(identity["workflow"] == WORKFLOW and
            re.fullmatch(r"[1-9][0-9]{0,19}", identity["run_id"]), "invalid candidate run")
    require(re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)",
                         identity["tag"]), "stable tag required")
    for key in ("commit", "tag_object_sha", "main_tip_at_start"):
        require(re.fullmatch(r"[0-9a-f]{40}", identity[key]), "invalid source identity")
    version = identity["tag"][1:]
    require(identity["product_version"] == version == request["product_version"],
            "tag/product version mismatch")
    require(request["checkout_commit"] == identity["commit"] == identity["main_tip_at_start"],
            "checkout/main snapshot mismatch")
    require(request["version_verified"] is True and request["changelog_verified"] is True,
            "version/changelog gate failed")
    package = request["package"]
    require(package["identity"] == identity, "package candidate mismatch")
    require(package["exe_product_version"] == package["zip_product_version"] == version,
            "EXE/ZIP version mismatch")
    require(set(package["required"]) == set(REQUIRED) and
            all(package["required"][key] == "PASS" for key in REQUIRED), "required test incomplete")
    require(package["msix"]["publish"] is False and
            package["msix"]["status"] == "PASS_MECHANISM", "internal MSIX boundary")
    require(set(request["asset_paths"]) == set(PUBLIC), "exact public file set required")
    assets = {}
    for name in PUBLIC:
        path = Path(request["asset_paths"][name])
        require(path.name == name and path.is_file() and not path.is_symlink(),
                "missing or unsafe public file")
        require(0 < path.stat().st_size <= 2 * 1024**3, "public file size limit")
        assets[name] = path.read_bytes()
    expected = package["assets"]
    require(len(expected) == 4 and {v["fileName"] for v in expected} == set(PUBLIC),
            "package descriptor file set mismatch")
    for value in expected:
        # Existing C03 descriptors use uppercase hex; normalize only the hex, not identity.
        actual = descriptor(value["fileName"], assets[value["fileName"]])
        require(value["bytes"] == actual["bytes"] and
                value["sha256"].lower() == actual["sha256"], "local bytes/evidence mismatch")
    for name in (EXE, ZIP):
        sidecar = assets[name + ".sha256"].decode("utf-8")
        require(re.fullmatch(rf"{sha(assets[name])}  {re.escape(name)}\r?\n?",
                             sidecar, re.I), "sidecar mismatch")
    if request["phase"] == "publish":
        require(request.get("protected_approval") is True, "protected publish approval required")
        clean = request.get("clean_host")
        require(isinstance(clean, dict), "exact EXE clean-host evidence required")
        require(clean["identity"] == identity and clean["product_version"] == version,
                "clean-host candidate mismatch")
        actual = descriptor(EXE, assets[EXE])
        require(clean["exe"]["fileName"] == EXE and clean["exe"]["bytes"] == actual["bytes"] and
                clean["exe"]["sha256"].lower() == actual["sha256"], "clean-host EXE mismatch")
        require(all(clean["tests"].get(key) == "PASS" for key in CH), "clean-host required test incomplete")
        host = clean["host"]
        require(host["os"] in ("Windows 10", "Windows 11") and host["architecture"].lower() == "x64" and
                host["fresh"] is True and host["standard_user"] is True and
                host["extra_dependencies"] is False, "clean-host boundary mismatch")
    return assets


def verify_identity(api, request, *, snapshot=False):
    i = request["identity"]
    prefix = "/repos/" + i["repository"]
    if snapshot and request.get("recorded_start") is not None:
        # Production captures this with the read-only token before any build.
        # Do not redefine "latest" using a main branch advanced during the build.
        require(request["recorded_start"] == i, "recorded candidate start mismatch")
    elif snapshot:
        main, _ = api.call("GET", prefix + "/commits/main")
        require(main["sha"] == i["main_tip_at_start"] == i["commit"], "not main tip at candidate start")
    ref, _ = api.call("GET", prefix + "/git/ref/tags/" + i["tag"])
    require(ref["object"]["type"] == "tag" and ref["object"]["sha"] == i["tag_object_sha"],
            "annotated tag changed")
    tag, _ = api.call("GET", prefix + "/git/tags/" + i["tag_object_sha"])
    require(tag["tag"] == i["tag"] and tag["sha"] == i["tag_object_sha"] and
            tag["object"]["type"] == "commit" and tag["object"]["sha"] == i["commit"], "tag commit changed")
    run, _ = api.call("GET", prefix + "/actions/runs/" + i["run_id"])
    path = run["path"].split("@", 1)
    require(path[0] == WORKFLOW and (len(path) == 1 or
            re.fullmatch(r"refs/(heads|tags)/[A-Za-z0-9._/-]+", path[1]) and
            not re.search(r"(^|/)\.\.?(/|$)|//|\.lock(/|$)", path[1])), "candidate workflow mismatch")
    require(str(run["id"]) == i["run_id"] and run["repository"]["full_name"] == i["repository"] and
            run["event"] == "workflow_dispatch" and run["head_sha"] == i["commit"],
            "candidate run source mismatch")
    # Candidate mutation is a downstream job of this still-running candidate run.
    completed = run["status"] == "completed" and run["conclusion"] == "success"
    active_candidate = request["phase"] == "candidate" and run["status"] == "in_progress" and run["conclusion"] is None
    require(completed or active_candidate, "candidate run not successful")


def stamp(request):
    return {"candidate": request["identity"], "package_sha256": sha(canonical(request["package"])),
            "assets": request["package"]["assets"]}


def verify_release(api, request, assets, release):
    i = request["identity"]
    require(release["tag_name"] == i["tag"] and release["target_commitish"] == i["commit"] and
            release["prerelease"] is False and type(release["draft"]) is bool, "existing release identity mismatch")
    try:
        body = json.loads(release["body"], object_pairs_hook=strict_object)
    except (ValueError, TypeError):
        raise Stop("unknown release provenance") from None
    require(body == stamp(request), "existing release evidence/source mismatch")
    prefix = "/repos/" + i["repository"]
    listing, _ = api.call("GET", prefix + f"/releases/{release['id']}/assets?per_page=100")
    require(len(listing) == 4 and {v["name"] for v in listing} == set(PUBLIC), "existing release incomplete/extra assets")
    for value in listing:
        downloaded, _ = api.call("GET", prefix + f"/releases/assets/{value['id']}", binary=True)
        require(downloaded == assets[value["name"]], "existing release bytes differ")


def matrix(request, directory):
    """Project the adapter observations; production supplies C03's validated matrix."""
    i = request["identity"]
    if request.get("final_matrix_path"):
        path = Path(request["final_matrix_path"])
        value = read_json(path)
    else:
        # Same closed row set at the normalized boundary. No fabricated host measurements.
        value = {"schemaVersion": 2, "productVersion": i["product_version"],
                 "sourceCommit": i["commit"],
                 "candidate": {"repository": i["repository"], "workflow": i["workflow"], "runId": i["run_id"]},
                 "rows": [{"artifactKind": kind, "platform": "windows", "runtimeIdentifier": "win-x64",
                           "publish": kind != KINDS[2],
                           "status": "PASS_MECHANISM" if kind == KINDS[2] else "PASS_REQUIRED"}
                          for kind in KINDS]}
        path = directory / "platform-release-matrix.json"
        write_json(path, value)
    require(value["schemaVersion"] == 2 and value["sourceCommit"] == i["commit"] and
            value["productVersion"] == i["product_version"] and
            value["candidate"] == {"repository": i["repository"], "workflow": i["workflow"], "runId": i["run_id"]},
            "final matrix identity mismatch")
    rows = value["rows"]
    require(len(rows) == 3 and {row["artifactKind"] for row in rows} == set(KINDS), "closed matrix kinds")
    for row in rows:
        internal = row["artifactKind"] == KINDS[2]
        require(row["publish"] is (not internal) and
                row["status"] == ("PASS_MECHANISM" if internal else "PASS_REQUIRED"), "matrix not publishable")
        if request.get("final_matrix_path") and not internal:
            for key, name in (("artifact", EXE if row["artifactKind"] == KINDS[0] else ZIP),
                              ("sidecar", (EXE if row["artifactKind"] == KINDS[0] else ZIP) + ".sha256")):
                expected = next(v for v in request["package"]["assets"] if v["fileName"] == name)
                observed = row[key]
                require(observed["fileName"] == expected["fileName"] and
                        observed["bytes"] == expected["bytes"] and
                        observed["sha256"].lower() == expected["sha256"].lower(),
                        "final matrix artifact mismatch")
    if request.get("final_matrix_path"):
        require(value["candidateRecord"]["sha256"].lower() ==
                request["package"]["candidate_record"]["sha256"].lower() and
                value["candidateRecord"]["bytes"] == request["package"]["candidate_record"]["bytes"],
                "final matrix candidate evidence mismatch")
    return str(path.resolve())


def execute(request, api, directory, result):
    result["stage"] = "local-gates"
    assets = validate_request(request)
    result["stage"] = "candidate-identity"
    verify_identity(api, request, snapshot=request["phase"] == "candidate")
    if request["phase"] == "publish":
        result["matrix_path"] = matrix(request, directory)
    prefix = "/repos/" + request["identity"]["repository"]
    result["stage"] = "read-release"
    release, etag = api.call("GET", prefix + "/releases/tags/" + request["identity"]["tag"], absent=True)
    if release is None:
        require(request["phase"] == "candidate", "draft candidate missing")
        verify_identity(api, request)
        result["stage"] = "create-draft"
        release, etag = api.call("POST", prefix + "/releases", {
            "tag_name": request["identity"]["tag"], "target_commitish": request["identity"]["commit"],
            "name": "StudyReport Evaluator " + request["identity"]["product_version"],
            "draft": True, "prerelease": False, "body": canonical(stamp(request)).decode()})
        upload = release["upload_url"].split("{", 1)[0]
        for name in PUBLIC:
            result["stage"] = "upload-" + name
            api.call("POST", upload + "?name=" + urllib.parse.quote(name, safe=""), assets[name])
        result["stage"] = "confirm-draft"
        release, _ = api.call("GET", prefix + f"/releases/{release['id']}")
        verify_release(api, request, assets, release)
        verify_identity(api, request)
        require(release["draft"] is True, "candidate unexpectedly public")
        result["outcome"] = "success"
    else:
        result["stage"] = "verify-existing"
        verify_release(api, request, assets, release)
        if request["phase"] == "candidate" or release["draft"] is False:
            result["outcome"] = "noop"
        else:
            result["stage"] = "pre-publication-identity"
            verify_identity(api, request)
            # Re-read complete state immediately before the single permitted transition.
            release, etag = api.call("GET", prefix + f"/releases/{release['id']}")
            verify_release(api, request, assets, release)
            require(release["draft"] is True, "release changed concurrently")
            verify_identity(api, request)
            result["stage"] = "publish"
            api.call("PATCH", prefix + f"/releases/{release['id']}", {"draft": False}, etag=etag)
            result["stage"] = "confirm-public"
            release, _ = api.call("GET", prefix + f"/releases/{release['id']}")
            verify_release(api, request, assets, release)
            verify_identity(api, request)
            require(release["draft"] is False, "public state not confirmed")
            result["outcome"] = "success"
    result["identity"] = request["identity"]
    result["stage"] = "confirmed"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--request", required=True, type=Path)
    parser.add_argument("--result", required=True, type=Path)
    parser.add_argument("--api", required=True)
    args = parser.parse_args()
    result = {"outcome": "stopped", "tag": "", "commit": "", "candidate_run_id": "", "stage": "request"}
    try:
        request = read_json(args.request)
        i = request.get("identity", {})
        result.update(tag=i.get("tag", ""), commit=i.get("commit", ""), candidate_run_id=i.get("run_id", ""))
        result["stage"] = "transaction-lock"
        with transaction_lock(args.api.rstrip("/"), i):
            execute(request, GitHub(args.api), args.request.resolve().parent, result)
    except Stop as error:
        result.update(outcome="stopped", reason=str(error))
    except Exception:
        # Never echo HTTP bodies, credential values, untrusted evidence or exception URLs.
        result.update(outcome="stopped", reason="invalid observations or operation failed; state not confirmed")
    args.result.parent.mkdir(parents=True, exist_ok=True)
    write_json(args.result, result)
    return 0 if result["outcome"] in ("success", "noop") else 1


if __name__ == "__main__":
    raise SystemExit(main())
