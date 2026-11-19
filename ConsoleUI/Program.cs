using ConsoleUI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Extensions;

Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var serviceCollection = new ServiceCollection();
serviceCollection.AddGamePersistence(configuration);
serviceCollection.AddTransient<ConsoleApplication>();
var services = serviceCollection.BuildServiceProvider();

services.InitializeGamePersistence();
using var scope = services.CreateScope();
scope.ServiceProvider.GetRequiredService<ConsoleApplication>().Run();
