FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/SchoolManagement.Core/SchoolManagement.Core.csproj", "src/SchoolManagement.Core/"]
COPY ["src/SchoolManagement.Application/SchoolManagement.Application.csproj", "src/SchoolManagement.Application/"]
COPY ["src/SchoolManagement.Infrastructure/SchoolManagement.Infrastructure.csproj", "src/SchoolManagement.Infrastructure/"]
COPY ["src/SchoolManagement.Web/SchoolManagement.Web.csproj", "src/SchoolManagement.Web/"]
RUN dotnet restore "src/SchoolManagement.Web/SchoolManagement.Web.csproj"

COPY . .
RUN dotnet publish "src/SchoolManagement.Web/SchoolManagement.Web.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
ARG APP_UID=1654
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

RUN mkdir -p /var/lib/schoolmanagement/private-documents /var/lib/schoolmanagement/data-protection \
    && chown -R "$APP_UID:$APP_UID" /var/lib/schoolmanagement
COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "SchoolManagement.Web.dll"]
