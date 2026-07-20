namespace POTracker.Contracts.Api
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; }
            = Array.Empty<T>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalRecords { get; init; }

        public int TotalPages => Math.Max(
            1,
            (int)Math.Ceiling(
                TotalRecords / (double)PageSize));

        public bool HasPreviousPage => Page > 1;

        public bool HasNextPage => Page < TotalPages;
    }
}
