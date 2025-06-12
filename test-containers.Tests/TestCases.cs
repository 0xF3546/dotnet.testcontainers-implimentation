using DotNet.Testcontainers.Builders;
using Npgsql;
using StackExchange.Redis;
using System.Diagnostics;

namespace test_containers.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }
        /// <summary>
        /// This is a copy of https://dotnet.testcontainers.org/
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task Test2()
        {
            // Create a new instance of a container.
            var container = new ContainerBuilder()
              // Set the image for the container to "testcontainers/helloworld:1.2.0".
              .WithImage("testcontainers/helloworld:1.2.0")
              // Bind port 8080 of the container to a random port on the host.
              .WithPortBinding(8080, true)
              // Wait until the HTTP endpoint of the container is available.
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080)))
              // Build the container configuration.
              .Build();

            // Start the container.
            await container.StartAsync()
              .ConfigureAwait(false);

            // Create a new instance of HttpClient to send HTTP requests.
            using var httpClient = new HttpClient();

            // Construct the request URI by specifying the scheme, hostname, assigned random host port, and the endpoint "uuid".
            var requestUri = new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(8080), "uuid").Uri;

            // Send an HTTP GET request to the specified URI and retrieve the response as a string.
            var guid = await httpClient.GetStringAsync(requestUri)
              .ConfigureAwait(false);

            // Ensure that the retrieved UUID is a valid GUID.
            Debug.Assert(Guid.TryParse(guid, out _));
        }

        [Test]
        public async Task TestPostgreSqlContainer()
        {
            const string postgresPassword = "password";
            const string postgresDatabase = "testdb";

            // Create a new instance of a PostgreSQL container.
            var postgresContainer = new ContainerBuilder()
              .WithImage("postgres:latest")
              .WithPortBinding(5432, true)
              .WithEnvironment("POSTGRES_PASSWORD", postgresPassword)
              .WithEnvironment("POSTGRES_DB", postgresDatabase)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
              .Build();

            // Start the container.
            await postgresContainer.StartAsync()
              .ConfigureAwait(false);

            var connectionString = $"Host={postgresContainer.Hostname};Port={postgresContainer.GetMappedPublicPort(5432)};" +
                                   $"Username=postgres;Password={postgresPassword};Database={postgresDatabase}";

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync();

            Assert.AreEqual(1, result);
        }

        [Test]
        public async Task TestRedisContainer()
        {
            // Create a new instance of a Redis container.
            var redisContainer = new ContainerBuilder()
              .WithImage("redis:latest")
              .WithPortBinding(6379, true)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
              .Build();

            // Start the container.
            await redisContainer.StartAsync()
              .ConfigureAwait(false);

            var configurationOptions = new ConfigurationOptions
            {
                EndPoints = { $"{redisContainer.Hostname}:{redisContainer.GetMappedPublicPort(6379)}" }
            };

            using (var redis = await ConnectionMultiplexer.ConnectAsync(configurationOptions))
            {
                var db = redis.GetDatabase();
                var setResult = await db.StringSetAsync("key", "value");
                var getResult = await db.StringGetAsync("key");

                Assert.True(setResult);
                Assert.True(getResult.HasValue);
                Assert.AreEqual("value", getResult.ToString());
            }
        }
    }
}