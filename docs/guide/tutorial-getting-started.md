# Get started: your first schema-aware query

This tutorial walks you, a .NET developer, from an empty project to a running console app that answers a plain-English question about your database. By the end you will have wired AgentQL into dependency injection and watched a language model write and run SQL against your own EF Core model.

## Before you start

You need:

- .NET 10 SDK installed.
- An existing EF Core `DbContext` (this tutorial uses one called `MyDbContext`).
- An API key for a supported AI provider (OpenAI is used here).

## 1. Install the package

Add the full-stack package, which bundles schema introspection, query execution, and the AI chat bridge:

```bash
dotnet add package Equibles.AgentQL.MicrosoftAI
```

You should see the package and its dependencies restore without errors.

## 2. Register AgentQL with dependency injection

In your startup code, register your `DbContext` and then call `AddAgentQLChat`:

```csharp
builder.Services.AddDbContext<MyDbContext>(o => o.UseSqlite("DataSource=app.db"));

builder.Services.AddAgentQLChat<MyDbContext>(configureChat: options =>
{
    options.Provider = AiProvider.OpenAI;
    options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.ModelName = "gpt-4o";
});
```

This single call registers `ISchemaProvider`, `IQueryExecutor`, `AgentQLPlugin`, and `IChatClient` — everything you need is now available from the service provider.

## 3. Ask a question

Resolve the chat client and the plugin, expose the plugin's functions as tools, and send a question:

```csharp
using var scope = serviceProvider.CreateScope();
var chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
var plugin = scope.ServiceProvider.GetRequiredService<AgentQLPlugin>();

var chatOptions = new ChatOptions { Tools = [.. AIFunctionFactory.Create(plugin)] };

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful database assistant."),
    new(ChatRole.User, "How many customers signed up last month?"),
};

var response = await chatClient.GetResponseAsync(messages, chatOptions);
Console.WriteLine(response);
```

## 4. See what happened

Behind the scenes the model called `GetDatabaseSchema` to learn your tables and columns, wrote a `SELECT`, called `ExecuteQuery` to run it, and turned the rows into an answer such as:

```
There were 42 customers who signed up last month.
```

You did not write any SQL — the model did, grounded in your real schema.

## What you achieved and what's next

You installed AgentQL, registered it in one call, and got a language model to answer a question by querying your database. To keep going, return to the [user guide index](README.md) for how-to guides on choosing a provider, controlling which tables the model can see, and keeping execution read-only.
