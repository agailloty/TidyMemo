namespace TidyMemo.Models;

public enum VideoCompressionJobStatus
{
    Queued,
    Processing,
    Done,
    Failed,
    Skipped,
    Cancelled
}
