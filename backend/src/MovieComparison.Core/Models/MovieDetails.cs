namespace MovieComparison.Core.Models
{
    public class MovieDetails : Movie
    {
        public string Rated { get; set; }
        public string Released { get; set; }
        public string Runtime { get; set; }
        public string Genre { get; set; }
        public string Director { get; set; }
        public string Writer { get; set; }
        public string Actors { get; set; }
        public string Plot { get; set; }
        public string Language { get; set; }
        public string Country { get; set; }
        public string Metascore { get; set; }
        public string Rating { get; set; }
        public string Votes { get; set; }
        public string Price { get; set; }
        public string Provider { get; set; }
    }
}
