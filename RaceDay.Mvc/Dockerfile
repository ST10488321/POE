# ---- build stage ----
# Change 8.0 to 9.0 or 10.0 if your projects target a newer .NET (see <TargetFramework> in RaceDay.Mvc.csproj)
ARG DOTNET_VERSION=8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first so Docker can cache the packages layer
COPY RaceDay.Mvc.csproj ./
RUN dotnet restore

# Then copy the rest and publish
COPY . ./
RUN dotnet publish RaceDay.Mvc.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage (small image, non-root user) ----
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "RaceDay.Mvc.dll"]
