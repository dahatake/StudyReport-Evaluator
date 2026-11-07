"""FR-121 AC-030/033: compile once, isolate build products, never authenticate.

The CLI is a local tripwire. Its success cannot certify CH-03, CH-06 or ADV-01.
No real candidate or old release is modified by this harness.
"""
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from harness import ROOT


def _run(arguments, env, timeout=600):
    result = subprocess.run(arguments, cwd=ROOT, env=env, shell=False,
                            capture_output=True, text=True, encoding="utf-8",
                            errors="replace", timeout=timeout)
    assert result.returncode == 0, (result.stdout + result.stderr)[-18000:]
    return result


def runtime_contract(directory: Path):
    directory.mkdir()
    # Keep SDK/OS essentials, but do not expose ambient authentication to tests.
    allowed = {"PATH", "PATHEXT", "SYSTEMROOT", "WINDIR", "COMSPEC", "TEMP", "TMP",
               "USERPROFILE", "HOME", "APPDATA", "LOCALAPPDATA", "PROGRAMFILES",
               "PROGRAMFILES(X86)", "PROGRAMDATA", "DOTNET_ROOT", "NUGET_PACKAGES",
               "PROCESSOR_ARCHITECTURE", "PROCESSOR_ARCHITEW6432"}
    env = {k: v for k, v in os.environ.items() if k.upper() in allowed}
    journal = directory / "forbidden-cli.txt"
    env.update(FR121_FORBIDDEN_CLI_JOURNAL=str(journal),
               STUDY_REPORT_EVALUATOR_COPILOT_LIVE_SMOKE="0",
               STUDY_REPORT_EVALUATOR_AUTO_COPILOT_LOGIN="0",
               DOTNET_CLI_TELEMETRY_OPTOUT="1")
    fake_project = ROOT / "tests/system/fr121/fake-cli/FakeCli.csproj"
    _run(["dotnet", "build", str(fake_project), "--artifacts-path",
          str(directory / "fake-build"), "-v:q"], env)
    payloads = list((directory / "fake-build/bin").rglob("copilot.exe"))
    assert len(payloads) == 1, payloads
    env["FR121_TRIPWIRE_CLI"] = str(payloads[0])
    # Download is explicitly bypassed by a local CLI binary, not a weakened hash
    # validator. false exercises distribution compilation; true the source profile.
    project = ROOT / "tests/system/ui/StudyReportEvaluator.SystemTests.Ui.csproj"
    for profile, skip in (("development", "true"), ("distribution", "false")):
        results = directory / profile / "trx"
        env["FR121_BUILD_PROFILE"] = profile
        properties = ["--artifacts-path", str(directory / profile / "build"),
                      f"-p:CopilotSkipCliDownload={skip}",
                      f"-p:CopilotCliBinaryPath={payloads[0]}"]
        _run(["dotnet", "build", str(project), *properties, "-v:q"], env)
        runtime_env = dict(env, HTTP_PROXY="http://127.0.0.1:1",
                           HTTPS_PROXY="http://127.0.0.1:1",
                           ALL_PROXY="http://127.0.0.1:1",
                           NO_PROXY="127.0.0.1,localhost")
        _run(["dotnet", "test", str(project), *properties, "--no-build", "--no-restore",
              "--filter", "FullyQualifiedName~BundledRuntimeSystemTests",
              "--results-directory", str(results), "--logger", "trx;LogFileName=runtime.trx",
              "-v:q"], runtime_env, timeout=1200)
        trx = list(results.glob("*.trx"))
        assert trx, f"{profile}: exit zero without test execution is not a pass"
        observed = [n for p in trx for n in ET.parse(p).getroot().iter()
                    if n.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
        for name in ("Default_composition", "Integrity_and_login", "Authentication_and_GUI"):
            matches = [n for n in observed if name in n.get("testName", "")]
            assert matches and all(n.get("outcome") == "Passed" for n in matches), name
    assert not journal.exists(), "A PATH/bundled tripwire actually started"
