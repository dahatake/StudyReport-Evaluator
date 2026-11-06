"""FR-121 AC-028: one selection for deterministic execution and TRX evidence."""

GROUPS = ("StudyReportEvaluator.Core.Tests.", "StudyReportEvaluator.App.Tests.")

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


if __name__ == "__main__":
    print(test_filter())
