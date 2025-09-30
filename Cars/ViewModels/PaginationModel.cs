namespace Cars.ViewModels
{
    public class PaginationModel
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
        public string? Action { get; set; }
        public string? Controller { get; set; }
        public RouteValueDictionary RouteValues { get; set; } = new();
    }
}
