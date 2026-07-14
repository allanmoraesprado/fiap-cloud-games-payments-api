# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for better layer caching
COPY src/PaymentsApi/PaymentsApi.csproj src/PaymentsApi/
RUN dotnet restore src/PaymentsApi/PaymentsApi.csproj

# Copy sources and publish
COPY src/ src/
RUN dotnet publish src/PaymentsApi/PaymentsApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
EXPOSE 8080
# Run as the image's built-in non-root user
USER app
ENTRYPOINT ["dotnet", "PaymentsApi.dll"]
