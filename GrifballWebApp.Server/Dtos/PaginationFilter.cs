namespace GrifballWebApp.Server.Dtos;

public class PaginationFilter
{
    private int _pageNumber;
    private int _pageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 250);
    }
    public SortDirection? SortDirection { get; set; }
    public string? SortColumn { get; set; }
    public PaginationFilter()
    {
        this.PageNumber = 1;
        this.PageSize = 10;
    }
    public PaginationFilter(int pageNumber, int pageSize)
    {
        this.PageNumber = pageNumber;
        this.PageSize = pageSize;
    }
}

public enum SortDirection
{
    Asc,
    Desc,
    None,
}

