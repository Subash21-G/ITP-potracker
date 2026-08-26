namespace POTracker.Contracts.Api
{
    public sealed class ExcelFilterValueDto
    {
        public string Value { get; init; } =
            string.Empty;

        public string Label { get; init; } =
            string.Empty;

        public int Count { get; init; }
    }
}
