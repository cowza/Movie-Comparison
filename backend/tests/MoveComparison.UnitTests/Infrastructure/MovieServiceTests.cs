using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Services;

namespace MoveComparison.UnitTests.Infrastructure
{
    public class MovieServiceTests
    {
        private readonly Mock<IExternalMovieProvider> _cinemaWorldProviderMock;
        private readonly Mock<IExternalMovieProvider> _filmWorldProviderMock;
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<MovieService>> _loggerMock;
        private readonly MovieService _sut; // System Under Test

        public MovieServiceTests()
        {
            // Setup mocks
            _cinemaWorldProviderMock = new Mock<IExternalMovieProvider>();
            _filmWorldProviderMock = new Mock<IExternalMovieProvider>();
            _loggerMock = new Mock<ILogger<MovieService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            // Configure provider names
            _cinemaWorldProviderMock.Setup(x => x.ProviderName).Returns("cinemaworld");
            _filmWorldProviderMock.Setup(x => x.ProviderName).Returns("filmworld");

            // Create service with mocked dependencies
            _sut = new MovieService(
                new[] { _cinemaWorldProviderMock.Object, _filmWorldProviderMock.Object },
                _cache,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetAllMoviesAsync_WhenBothProvidersReturnMovies_ShouldReturnCombinedList()
        {
            // Arrange
            var cinemaWorldMovies = new List<Movie>
            {
                new Movie { ID = "cw1", Title = "Movie 1", Year = "2021", Provider = "cinemaworld" }
            };

            var filmWorldMovies = new List<Movie>
            {
                new Movie { ID = "fw1", Title = "Movie 1", Year = "2021", Provider = "filmworld" }
            };

            _cinemaWorldProviderMock
                .Setup(x => x.GetMoviesAsync())
                .ReturnsAsync(cinemaWorldMovies);

            _filmWorldProviderMock
                .Setup(x => x.GetMoviesAsync())
                .ReturnsAsync(filmWorldMovies);

            // Act
            var result = await _sut.GetAllMoviesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result); // Should combine duplicate movies
            var movie = result.First();
            Assert.Equal("Movie 1", movie.Title);
            Assert.Contains("cinemaworld", movie.Providers);
            Assert.Contains("filmworld", movie.Providers);
        }

        [Fact]
        public async Task GetMovieBestPriceAsync_WhenBothProvidersRespond_ShouldReturnLowestPrice()
        {
            // Arrange
            var cinemaWorldMovie = new MovieDetails
            {
                ID = "cw1",
                Title = "Movie 1",
                Price = "122",
                Provider = "cinemaworld"
            };

            var filmWorldMovie = new MovieDetails
            {
                ID = "fw1",
                Title = "Movie 1",
                Price = "123",
                Provider = "filmworld"
            };

            _cinemaWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ReturnsAsync(cinemaWorldMovie);

            _filmWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ReturnsAsync(filmWorldMovie);

            // Act
            var result = await _sut.GetMovieBestPriceAsync("1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cinemaworld", result.Provider);
            Assert.Equal("122", result.Price);
        }

        [Fact]
        public async Task GetMovieBestPriceAsync_WhenOneProviderFails_ShouldReturnOtherProviderPrice()
        {
            // Arrange
            var cinemaWorldMovie = new MovieDetails
            {
                ID = "cw1",
                Title = "Movie 1",
                Price = "10",
                Provider = "cinemaworld"
            };

            _cinemaWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ReturnsAsync(cinemaWorldMovie);

            _filmWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ThrowsAsync(new Exception("API Error"));

            // Act
            var result = await _sut.GetMovieBestPriceAsync("1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cinemaworld", result.Provider);
            Assert.Equal("10", result.Price);
        }

        [Fact]
        public async Task GetMovieBestPriceAsync_WhenAllProvidersFail_ShouldThrowException()
        {
            // Arrange
            _cinemaWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ThrowsAsync(new Exception("API Error"));

            _filmWorldProviderMock
                .Setup(x => x.GetMovieDetailsAsync("1"))
                .ThrowsAsync(new Exception("API Error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.GetMovieBestPriceAsync("1")
            );
        }

        [Fact]
        public async Task GetAllMoviesAsync_ShouldUseCacheOnSubsequentCalls()
        {
            // Arrange
            var movies = new List<Movie>
            {
                new Movie { ID = "cw1", Title = "Movie 1", Year = "2021", Provider = "cinemaworld" }
            };

            _cinemaWorldProviderMock
                .Setup(x => x.GetMoviesAsync())
                .ReturnsAsync(movies);

            // Act
            var firstResult = await _sut.GetAllMoviesAsync();

            // Clear the mock to verify cache usage
            _cinemaWorldProviderMock.Reset();

            var secondResult = await _sut.GetAllMoviesAsync();

            // Assert
            Assert.Equal(
                firstResult.First().Title,
                secondResult.First().Title
            );
            _cinemaWorldProviderMock.Verify(
                x => x.GetMoviesAsync(),
                Times.Never,
                "Provider should not be called when using cached data"
            );
        }
    }
}