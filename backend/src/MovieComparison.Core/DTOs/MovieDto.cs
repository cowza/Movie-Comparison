namespace MovieComparison.Core.DTOs
{
    public class MovieDto
    {
        public string Title { get; set; }
        public string Year { get; set; }
        public string Poster { get; set; }
        public List<ProviderDto> Providers { get; set; }
    }

    public class ProviderDto
    {
        public string Name { get; set; }
        public string ID { get; set; }
    }
}
