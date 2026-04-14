namespace VeloCenter.Core.Activities;

public sealed record ActivityImportResult(
    ActivitySummary Activity,
    bool WasCreated);
