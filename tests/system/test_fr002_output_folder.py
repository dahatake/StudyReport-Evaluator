"""FR-002 AC-055: one ledger case, native UI boundary + unchanged durable contracts."""

from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[2]
UI = "tests/system/ui"
APP = "tests/StudyReportEvaluator.App.Tests"
UI_NAMES = (
    "FR002_AC055_Settings_has_only_common_category_and_common_controls_remain",
    "FR002_AC055_Design_entry_buttons_never_point_to_removed_settings_categories",
    "FR002_AC055_Native_selection_cancel_failure_save_restore_and_default_are_one_journey",
    "FR002_AC055_Picker_and_reset_during_run_preserve_snapshot_and_reserved_paths",
)
# Existing, unchanged regression oracles implement §9.1/11.6/11.8:
# actual final workbook, invalid override, immutable request, reserved checkpoint paths.
CONTRACT_NAMES = (
    "Saved_output_preference_survives_new_instances_and_input_change_before_real_final_output",
    "Unavailable_output_override_blocks_start_without_fallback_or_directory_creation",
    "Cancel_saves_a_real_partial_and_new_instances_resume_without_repeating_completed_AI_or_reference",
)


def dotnet(project, names, *, discover):
    selection = "|".join(f"FullyQualifiedName~{name}" for name in names)
    command = ["dotnet", "test", project, "--filter", selection, "--verbosity", "minimal"]
    command += ["--list-tests"] if discover else ["--no-build"]
    result = subprocess.run(
        command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8",
        errors="replace", timeout=300, check=False,
    )
    output = result.stdout + result.stderr
    assert result.returncode == 0, f"{command!r}\n{output}"
    if discover:
        for name in names:
            assert name in output, f"Zero tests discovered for {name}\n{output}"


# FR-002 AC-055: no live credentials, native platform storage/AI boundaries are synthetic.
def test_fr002_ac055_output_folder_ui_and_preserved_run_contracts():
    # Discover/build BOTH projects before executing: no stale --no-build binary can pass.
    dotnet(UI, UI_NAMES, discover=True)
    dotnet(APP, CONTRACT_NAMES, discover=True)
    dotnet(UI, UI_NAMES, discover=False)
    dotnet(APP, CONTRACT_NAMES, discover=False)
