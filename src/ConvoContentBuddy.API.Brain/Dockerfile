# ConvoContentBuddy API Brain Service
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/ConvoContentBuddy.API.Brain/ConvoContentBuddy.API.Brain.csproj", "src/ConvoContentBuddy.API.Brain/"]
COPY ["src/ConvoContentBuddy.ServiceDefaults/ConvoContentBuddy.ServiceDefaults.csproj", "src/ConvoContentBuddy.ServiceDefaults/"]

RUN dotnet restore "src/ConvoContentBuddy.API.Brain/ConvoContentBuddy.API.Brain.csproj"

# Copy source code
COPY src/ .

WORKDIR "/src/src/ConvoContentBuddy.API.Brain"
RUN dotnet build "ConvoContentBuddy.API.Brain.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConvoContentBuddy.API.Brain.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConvoContentBuddy.API.Brain.dll"]
