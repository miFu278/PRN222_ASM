# Sử dụng base image .NET 9
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy toàn bộ solution và các project files để restore (Tận dụng cache của Docker)
COPY ["RAGChatBot.Presentation/RAGChatBot.Presentation.csproj", "RAGChatBot.Presentation/"]
COPY ["RAGChatBot.Application/RAGChatBot.Application.csproj", "RAGChatBot.Application/"]
COPY ["RAGChatBot.Domain/RAGChatBot.Domain.csproj", "RAGChatBot.Domain/"]
COPY ["RAGChatBot.Infrastructure/RAGChatBot.Infrastructure.csproj", "RAGChatBot.Infrastructure/"]

RUN dotnet restore "RAGChatBot.Presentation/RAGChatBot.Presentation.csproj"

# Copy toàn bộ source code
COPY . .
WORKDIR "/src/RAGChatBot.Presentation"

# Build project
RUN dotnet build "RAGChatBot.Presentation.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "RAGChatBot.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Chạy ứng dụng
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RAGChatBot.Presentation.dll"]
