# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/SandersSavingsAndLoan.csproj ./
RUN dotnet restore
COPY src/ ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "SandersSavingsAndLoan.dll"]
