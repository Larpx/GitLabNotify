# ============================================================================
# GitLabNotify 多阶段 Dockerfile
# ============================================================================
# 阶段 1：使用 SDK 镜像构建
# 阶段 2：使用 Alpine 运行时镜像（更小）
# ============================================================================

# --- 阶段 1：构建 ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先复制 csproj 并还原依赖（利用 Docker 层缓存）
COPY src/GitLabNotify/GitLabNotify.csproj ./GitLabNotify/
RUN dotnet restore ./GitLabNotify/GitLabNotify.csproj

# 复制源代码并构建
COPY src/GitLabNotify/ ./GitLabNotify/
WORKDIR /src/GitLabNotify
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- 阶段 2：运行时 ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# 安装运行时依赖：
# - icu-libs: ICU 全球化支持（SQLite 中文排序 + 中文消息编码需要）
# - tzdata: 时区数据（日志时间戳正确显示）
# - sqlite-libs: SQLite 原生库（Microsoft.Data.Sqlite 在 Alpine musl 上运行需要）
RUN apk add --no-cache icu-libs tzdata sqlite-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# 复制构建产物
COPY --from=build /app/publish ./

# 创建数据和日志目录
RUN mkdir -p /app/data /app/logs

# 暴露端口
EXPOSE 8080

# 环境变量
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# 数据卷
VOLUME ["/app/data", "/app/logs"]

# 健康检查（使用 busybox wget 兼容写法）
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://localhost:8080/health || exit 1

# 入口
ENTRYPOINT ["dotnet", "GitLabNotify.dll"]
