"""FR-121 AC-028: one selection for deterministic execution and TRX evidence."""

import os
from pathlib import Path
import platform
import subprocess
import sys

GROUPS = ("StudyReportEvaluator.Core.Tests.", "StudyReportEvaluator.App.Tests.")

# FR-121 AC-103: these observations require real Windows x64, not emulation.
# The first two remain mandatory deterministic candidate TRX evidence.
REQUIRED_X64_METHODS = (
    "StudyReportEvaluator.App.Tests.E2E.WindowsX64PerformanceEvidenceTests."
    "Synthetic_531_row_read_write_validation_median_is_recorded_and_within_target",
    "StudyReportEvaluator.App.Tests.E2E.WindowsLocalApplicationTests."
    "Normal_release_framework_dependent_app_starts_without_office_or_com_runtime",
)
NATIVE_X64_METHODS = REQUIRED_X64_METHODS + (
    "StudyReportEvaluator.App.Tests.Packaging.WindowsPublishPackageTests."
    "Windows_publish_and_unsigned_package_are_safe_launchable_and_reproducible",
)
PRIVATE_SAMPLE_METHOD = (
    "StudyReportEvaluator.App.Tests.E2E.SampleWorkbookStructuralTests."
    "Repository_sample_is_opened_read_only_and_only_structural_metadata_drives_mapping"
)

# These suites have separate artifact/private-input validation steps.
EXCLUDED_CLASSES = (
    "StudyReportEvaluator.App.Tests.E2E.SampleWorkbookStructuralTests",
    "StudyReportEvaluator.App.Tests.Packaging.WindowsPublishPackageTests",
    "StudyReportEvaluator.App.Tests.Packaging.WindowsSingleFilePackageTests",
)

# Exclude only the opt-in methods, retaining their deterministic sibling tests.
EXCLUDED_METHODS = (
    "StudyReportEvaluator.App.Tests.Copilot.SystemCopilotCliPathResolverTests."
    "Installed_cli_supports_sdk_authentication_and_model_discovery_without_AI_AC033",
    "StudyReportEvaluator.App.Tests.Packaging.WindowsSingleFileArtifactTests."
    "Opted_in_P02_artifact_is_packaged_by_the_actual_script_with_identical_bytes_and_versions",
)


def required_group(class_name, method_name):
    qualified_name = f"{class_name}.{method_name}"
    if class_name in EXCLUDED_CLASSES or qualified_name in EXCLUDED_METHODS:
        return None
    return next((group for group in GROUPS if class_name.startswith(group)), None)


def test_filter():
    return "&".join(
        [f"FullyQualifiedName!~{name}." for name in EXCLUDED_CLASSES]
        + [f"FullyQualifiedName!={name}" for name in EXCLUDED_METHODS])


def local_filter(architecture):
    """Local only: do not reuse this exclusion in candidate/publication evidence."""
    if architecture in {"arm64", "aarch64"}:
        return "&".join(f"FullyQualifiedName!={name}" for name in NATIVE_X64_METHODS)
    if architecture in {"amd64", "x86_64", "x64"}:
        return None
    raise ValueError(f"Unsupported development host architecture: {architecture}")


def private_sample_available(root):
    """Match E02RepositoryLayout's repository/configured/main-worktree search."""
    directories = [root / "sample"]
    configured = os.environ.get("STUDYREPORT_SAMPLE_DIR", "").strip().strip('"')
    if configured:
        directories.append(Path(configured))
    dotgit = root / ".git"
    if dotgit.is_file():
        line = next((line for line in dotgit.read_text().splitlines()
                     if line.startswith("gitdir:")), "")
        if line:
            gitdir = (root / line[len("gitdir:"):].strip()).resolve()
            common = gitdir / "commondir"
            if common.is_file():
                main = (gitdir / common.read_text().strip()).resolve().parent
                directories.append(main / "sample")
    return any((directory / "SampleReport.xlsx").is_file() for directory in directories)


def run_local(arguments):
    # Do not let a caller replace the selected profile with another filter.
    if any(arg == "--filter" or arg.startswith("--filter=") for arg in arguments):
        raise ValueError("app-unit owns its host profile filter")
    architecture = os.environ.get(
        "PROCESSOR_ARCHITEW6432",
        os.environ.get("PROCESSOR_ARCHITECTURE", platform.machine()),
    ).lower()
    selected_filter = local_filter(architecture)
    root = Path(__file__).resolve().parents[1]
    print(f"Local app-unit host profile: {architecture}; not publication evidence", flush=True)
    if selected_filter:
        for name in NATIVE_X64_METHODS:
            print(f"x64 NOT_RUN (requires real Windows x64): {name}", flush=True)
    if not private_sample_available(root):
        print(f"Private input NOT_RUN (SampleReport.xlsx unavailable): {PRIVATE_SAMPLE_METHOD}",
              flush=True)
        private_filter = f"FullyQualifiedName!={PRIVATE_SAMPLE_METHOD}"
        selected_filter = "&".join(filter(None, (selected_filter, private_filter)))
    if architecture in {"amd64", "x86_64", "x64"}:
        # Required by the normal framework-dependent startup acceptance test.
        build = subprocess.run(
            ["dotnet", "build", "src/StudyReportEvaluator.App", "-c", "Release"],
            cwd=root,
        )
        if build.returncode:
            return build.returncode
    command = ["dotnet", "test", "tests/StudyReportEvaluator.App.Tests", "-c", "Debug"]
    if selected_filter:
        command += ["--filter", selected_filter]
    # Forward standard dotnet test arguments as separate, unchanged arguments.
    return subprocess.run(command + arguments, cwd=root).returncode


if __name__ == "__main__":
    if sys.argv[1:2] == ["--run-local"]:
        sys.exit(run_local(sys.argv[2:]))
    if len(sys.argv) != 1:
        sys.exit("Use no arguments for the required release selector, or --run-local for app-unit")
    print(test_filter())
