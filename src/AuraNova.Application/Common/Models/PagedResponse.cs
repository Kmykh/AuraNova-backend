using System.Collections.Generic;

namespace AuraNova.Application.Common.Models
{
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }

        public PagedResponse() { }

        public PagedResponse(List<T> items, int count, int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
            TotalItems = count;
            TotalPages = pageSize > 0 ? (int)System.Math.Ceiling(count / (double)pageSize) : 0;
            Items = items;
            HasNextPage = Page < TotalPages;
            HasPreviousPage = Page > 1;
        }
    }
}
