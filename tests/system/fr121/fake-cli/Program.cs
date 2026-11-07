// FR-121 AC-030 AC-033: tripwire, NOT an SDK/real CLI or clean-host simulator.
string? journal = Environment.GetEnvironmentVariable("FR121_FORBIDDEN_CLI_JOURNAL");
if (!string.IsNullOrEmpty(journal))
    File.AppendAllText(journal, "FORBIDDEN CLI START\n");
return 97;
