# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so the (slow) package layer is cached until a .csproj changes.
COPY src/TodoApp.Api/TodoApp.Api.csproj src/TodoApp.Api/
RUN dotnet restore src/TodoApp.Api/TodoApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/TodoApp.Api/TodoApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# SQLite lives on a volume mounted at /data. Create it owned by the unprivileged runtime user so a fresh
# named volume inherits writable permissions.
RUN mkdir /data && chown app:app /data
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/todo.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TodoApp.Api.dll"]
