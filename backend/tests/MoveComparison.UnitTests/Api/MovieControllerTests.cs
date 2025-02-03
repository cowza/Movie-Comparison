using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MovieComparison.Api.Controllers;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;

namespace MoveComparison.UnitTests.Api
{
    public class MoviesControllerTests
    {
        private readonly Mock<IMovieService> _movieServiceMock;
        private readonly Mock<ILogger<MoviesController>> _loggerMock;
        private readonly MoviesController _sut;
        private readonly List<MovieDto> _starWarsMovies;

        public MoviesControllerTests()
        {
            _movieServiceMock = new Mock<IMovieService>();
            _loggerMock = new Mock<ILogger<MoviesController>>();
            _sut = new MoviesController(_movieServiceMock.Object, _loggerMock.Object);

            // Setup test data
            _starWarsMovies = new List<MovieDto>
        {
            new()
            {
                Title = "Star Wars: Episode IV - A New Hope",
                Year = "1977",
                Poster = "https://m.media-amazon.com/images/M/MV5BOTIyMDY2NGQtOGJjNi00OTk4LWFhMDgtYmE3M2NiYzM0YTVmXkEyXkFqcGdeQXVyNTU1NTcwOTk@._V1_SX300.jpg",
                ID = "0076759",
                Providers = "cinemaworld;filmworld;"
            },
            new()
            {
                Title = "Star Wars: The Force Awakens",
                Year = "2015",
                Poster = "https://m.media-amazon.com/images/M/MV5BOTAzODEzNDAzMl5BMl5BanBnXkFtZTgwMDU1MTgzNzE@._V1_SX300.jpg",
                ID = "2488496",
                Providers = "cinemaworld"
            }
        };
        }

        [Fact]
        public async Task GetMovies_ReturnsAllStarWarsMovies()
        {
            // Arrange
            _movieServiceMock
                .Setup(x => x.GetAllMoviesAsync())
                .ReturnsAsync(_starWarsMovies);

            // Act
            var result = await _sut.GetMovies();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var movies = Assert.IsAssignableFrom<IEnumerable<MovieDto>>(okResult.Value);

            var firstMovie = movies.First();
            Assert.Equal("Star Wars: Episode IV - A New Hope", firstMovie.Title);
            Assert.Equal("1977", firstMovie.Year);
            Assert.Contains("cinemaworld;filmworld;", firstMovie.Providers);

            var lastMovie = movies.Last();
            Assert.Equal("Star Wars: The Force Awakens", lastMovie.Title);
            Assert.Contains("cinemaworld", lastMovie.Providers);
        }

        [Fact]
        public async Task GetMoviePrices_ForNewHope_ReturnsLowestPrice()
        {
            // Arrange
            var expectedPrice = new MoviePriceDto
            {
                Provider = "filmworld",
                Price = "29.5"
            };

            _movieServiceMock
                .Setup(x => x.GetMovieBestPriceAsync(It.Is<string>(p =>
                    p == "0076759" || p == "0076759")))
                .ReturnsAsync(expectedPrice);

            // Act
            var result = await _sut.GetMoviePrices("0076759");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var price = Assert.IsType<MoviePriceDto>(okResult.Value);
            Assert.Equal("filmworld", price.Provider);
            Assert.Equal("29.5", price.Price);
        }

        [Fact]
        public async Task GetMoviePrices_ForSingleProviderMovie_ReturnsCorrectPrice()
        {
            // Arrange
            var expectedPrice = new MoviePriceDto
            {
                Provider = "cinemaworld",
                Price = "25.0"
            };

            _movieServiceMock
                .Setup(x => x.GetMovieBestPriceAsync(It.Is<string>(p =>
                    p == "2488496")))
                .ReturnsAsync(expectedPrice);

            // Act
            var result = await _sut.GetMoviePrices("2488496");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var price = Assert.IsType<MoviePriceDto>(okResult.Value);
            Assert.Equal("cinemaworld", price.Provider);
            Assert.Equal("25.0", price.Price);
        }

        [Fact]
        public async Task GetMoviePrices_WhenOneProviderFails_StillReturnsPrice()
        {
            // Arrange
            _movieServiceMock
                .Setup(x => x.GetMovieBestPriceAsync(It.IsAny<string>()))
                .ReturnsAsync(new MoviePriceDto
                {
                    Provider = "cinemaworld",
                    Price = "30.0"
                });

            // Act
            var result = await _sut.GetMoviePrices("2488496");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetMoviePrices_WithNoProviders_ReturnsBadRequest()
        {
            // Act
            var result = await _sut.GetMoviePrices(null);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("No id specified", badRequestResult.Value);
        }

        [Fact]
        public async Task GetMoviePrices_WhenServiceThrowsException_Returns500()
        {
            // Arrange
            var expectedException = new Exception("Test error");
            _movieServiceMock
                .Setup(x => x.GetMovieBestPriceAsync(It.IsAny<string>()))
                .ThrowsAsync(expectedException);

            // Act
            var result = await _sut.GetMoviePrices("0076759");

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while retrieving movie prices", statusCodeResult.Value);

            // Verify logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetMovies_WhenServiceThrowsException_Returns500()
        {
            // Arrange
            var expectedException = new Exception("Failed to get movies");
            _movieServiceMock
                .Setup(x => x.GetAllMoviesAsync())
                .ThrowsAsync(expectedException);

            // Act
            var result = await _sut.GetMovies();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while retrieving movies", statusCodeResult.Value);

            // Verify logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
