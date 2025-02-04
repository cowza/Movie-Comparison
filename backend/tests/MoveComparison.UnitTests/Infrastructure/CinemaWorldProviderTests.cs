using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using System.Net;
using System.Text.Json;

namespace MoveComparison.UnitTests.Infrastructure
{
    public class CinemaWorldProviderTests
    {
        private readonly Mock<ILogger<CinemaWorldProvider>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly ExternalApiSettings _settings;
        private readonly CinemaWorldProvider _sut;

        public CinemaWorldProviderTests()
        {
            _loggerMock = new Mock<ILogger<CinemaWorldProvider>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            _settings = new ExternalApiSettings
            {
                BaseUrl = "https://api.test.com",
                ApiToken = "test-token"
            };

            var options = Options.Create(_settings);

            _sut = new CinemaWorldProvider(
                _httpClient,
                options,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetMoviesAsync_WhenApiCallSucceeds_ReturnsMovieList()
        {
            // Arrange
            var movieResponse = new ExternalMoviesListResponse
            {
                Movies = new List<ExternalMovieResponse>
                {
                    new ExternalMovieDetailsResponse
                    {
                        Title = "Star Wars: Episode IV - A New Hope",
                        Year = "1977",
                        ID = "cw0076759",
                        Type = "movie",
                        Poster = "https://m.media-amazon.com/images/M/MV5BOTIyMDY2NGQtOGJjNi00OTk4LWFhMDgtYmE3M2NiYzM0YTVmXkEyXkFqcGdeQXVyNTU1NTcwOTk@._V1_SX300.jpg"
                    },
                    new ExternalMovieDetailsResponse
                    {
                        Title = "Star Wars: Episode V - The Empire Strikes Back",
                        Year = "1980",
                        ID = "cw0080684",
                        Type = "movie",
                        Poster = "https://m.media-amazon.com/images/M/MV5BMjE2MzQwMTgxN15BMl5BanBnXkFtZTcwMDQzNjk2OQ@@._V1_SX300.jpg"
                    }
                }
            };

            SetupMockHttpResponse(
                "/api/cinemaworld/movies",
                HttpStatusCode.OK,
                movieResponse
            );

            // Act
            var result = await _sut.GetMoviesAsync();

            // Assert
            var movie = result.First();
            Assert.Equal("cw0076759", movie.ID);
            Assert.Equal("Star Wars: Episode IV - A New Hope", movie.Title);
            Assert.Equal("1977", movie.Year);
            Assert.Equal("cinemaworld", movie.Provider);
        }

        [Fact]
        public async Task GetMovieDetailsAsync_WhenApiCallSucceeds_ReturnsMovieDetails()
        {
            // Arrange
            var movieDetails = new ExternalMovieDetailsResponse
            {
                Title = "Star Wars: Episode VI - Return of the Jedi",
                Year = "1983",
                Rated = "PG",
                Released = "25 May 1983",
                Runtime = "131 min",
                Genre = "Action, Adventure, Fantasy",
                Director = "Richard Marquand",
                Writer = "Lawrence Kasdan (screenplay), George Lucas (screenplay), George Lucas (story by)",
                Actors = "Mark Hamill, Harrison Ford, Carrie Fisher, Billy Dee Williams",
                Plot = "After rescuing Han Solo from the palace of Jabba the Hutt, the rebels attempt to destroy the second Death Star, while Luke struggles to make Vader return from the dark side of the Force.",
                Language = "English",
                Country = "USA",
                Awards = "Nominated for 4 Oscars. Another 18 wins & 16 nominations.",
                Poster = "https://m.media-amazon.com/images/M/MV5BMTQ0MzI1NjYwOF5BMl5BanBnXkFtZTgwODU3NDU2MTE@._V1._CR93,97,1209,1861_SX89_AL_.jpg_V1_SX300.jpg",
                Metascore = "53",
                Rating = "8.4",
                Votes = "686,479",
                ID = "cw0086190",
                Type = "movie",
                Price = "253.5"
            };

            SetupMockHttpResponse(
                "/api/cinemaworld/movie/cw0086190",
                HttpStatusCode.OK,
                movieDetails
            );

            // Act
            var result = await _sut.GetMovieDetailsAsync("0086190");

            // Assert
            Assert.Equal("cw0086190", result.ID);
            Assert.Equal("Star Wars: Episode VI - Return of the Jedi", result.Title);
            Assert.Equal("253.5", result.Price);
            Assert.Equal("cinemaworld", result.Provider);
        }

        [Fact]
        public async Task GetMoviesAsync_WhenApiCallFails_ThrowsProviderException()
        {
            // Arrange
            SetupMockHttpResponse<object>(
                "/api/cinemaworld/movies",
                HttpStatusCode.InternalServerError,
                null
            );

            // Act & Assert
            await Assert.ThrowsAsync<ProviderException>(
                () => _sut.GetMoviesAsync()
            );
        }

        [Fact]
        public async Task GetMovieDetailsAsync_WhenApiCallFails_ThrowsProviderException()
        {
            // Arrange
            SetupMockHttpResponse<object>(
                "/api/cinemaworld/movie/cw1",
                HttpStatusCode.InternalServerError,
                null
            );

            // Act & Assert
            await Assert.ThrowsAsync<ProviderException>(
                () => _sut.GetMovieDetailsAsync("1")
            );
        }

        [Fact]
        public async Task GetMoviesAsync_WhenInvalidJsonReturned_ThrowsProviderException()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("invalid json")
                });

            // Act & Assert
            await Assert.ThrowsAsync<ProviderException>(
                () => _sut.GetMoviesAsync()
            );
        }

        private void SetupMockHttpResponse<T>(string requestUri, HttpStatusCode statusCode, T content)
        {
            var response = new HttpResponseMessage(statusCode);

            if (content != null)
            {
                response.Content = new StringContent(
                    JsonSerializer.Serialize(content)
                );
            }

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri.PathAndQuery.Contains(requestUri)),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);
        }
    }
}