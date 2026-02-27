# ConvoContentBuddy UI Web Service
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ["src/ConvoContentBuddy.UI.Web/ConvoContentBuddy.UI.Web.csproj", "src/ConvoContentBuddy.UI.Web/"]
COPY ["src/ConvoContentBuddy.ServiceDefaults/ConvoContentBuddy.ServiceDefaults.csproj", "src/ConvoContentBuddy.ServiceDefaults/"]

RUN dotnet restore "src/ConvoContentBuddy.UI.Web/ConvoContentBuddy.UI.Web.csproj"

# Copy source code
COPY src/ .

WORKDIR "/src/src/ConvoContentBuddy.UI.Web"
RUN dotnet build "ConvoContentBuddy.UI.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConvoContentBuddy.UI.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - use nginx to serve static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY src/ConvoContentBuddy.UI.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
