FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# --- BEGIN: Thêm cấu hình NuGet riêng trong Docker ---
ARG BAGET_URL
ARG BAGET_API_KEY
RUN dotnet nuget add source "${BAGET_URL}" --name DevOpsNuGet --username user --password "${BAGET_API_KEY}" --store-password-in-clear-text
# --- END: Thêm cấu hình NuGet riêng trong Docker ---

COPY ["SagaOrchestrator/SagaOrchestrator.csproj", "SagaOrchestrator/"]
RUN dotnet restore "./SagaOrchestrator/SagaOrchestrator.csproj"

COPY . .
WORKDIR "/src/SagaOrchestrator"
# Build: Xóa "-o /app/build". Kết quả sẽ vào thư mục build mặc định (vd: bin/Release/net8.0)
RUN dotnet build "./SagaOrchestrator.csproj" -c $BUILD_CONFIGURATION --no-restore

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Publish: Tìm kết quả build ở thư mục mặc định, xuất kết quả publish ra /app/publish. Xóa comment cuối dòng.
RUN dotnet publish "./SagaOrchestrator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-build

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SagaOrchestrator.dll"]