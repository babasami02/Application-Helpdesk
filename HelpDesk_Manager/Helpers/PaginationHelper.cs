namespace HelpDesk_Manager.Helpers
{
    public class PagedList<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageActuelle { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }

        public bool APagePrecedente => PageActuelle > 1;
        public bool APageSuivante => PageActuelle < TotalPages;

        public static PagedList<T> Creer(IQueryable<T> source, int page, int pageSize = 10)
        {
            var total = source.Count();
            var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedList<T>
            {
                Items = items,
                PageActuelle = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalItems = total,
                PageSize = pageSize
            };
        }
    }
}