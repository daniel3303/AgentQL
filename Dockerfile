# Overridable environment variables:
#   ConnectionStrings__DefaultConnection  — SQLite connection string (default: DataSource=travel.db)
#   AgentQL__Provider                     — AI provider: OpenAI / Anthropic / Ollama
#   AgentQL__ApiKey                       — API key for the chosen provider
#   AgentQL__Endpoint                     — API endpoint URL
#   AgentQL__ModelName                    — Model name (e.g. gpt-4o, claude-sonnet-4-20250514)
#
# Usage:
#   docker build -t agentql-demo .
#   docker run -p 8080:8080 -e AgentQL__ApiKey=sk-xxx -e AgentQL__ModelName=gpt-4o agentql-demo

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.sln global.json Directory.Build.props Directory.Packages.props ./
COPY src/Directory.Build.props src/
COPY src/Equibles.AgentQL/*.csproj src/Equibles.AgentQL/
COPY src/Equibles.AgentQL.EntityFrameworkCore/*.csproj src/Equibles.AgentQL.EntityFrameworkCore/
COPY src/Equibles.AgentQL.MicrosoftAI/*.csproj src/Equibles.AgentQL.MicrosoftAI/
COPY src/Equibles.AgentQL.Demo/*.csproj src/Equibles.AgentQL.Demo/
RUN dotnet restore src/Equibles.AgentQL.Demo/Equibles.AgentQL.Demo.csproj

COPY . .
RUN dotnet publish src/Equibles.AgentQL.Demo -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Equibles.AgentQL.Demo.dll"]
