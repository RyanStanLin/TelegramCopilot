ARG TDLIB_IMAGE
FROM ${TDLIB_IMAGE} AS tdlib
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY global.json Directory.Build.props TelegramCopilot.slnx ./
COPY src/ src/
RUN dotnet publish src/TelegramCopilot.Api -c Release -o /publish/api /bl:/tmp/api.binlog && dotnet publish src/TelegramCopilot.Cli -c Release -o /publish/cli /bl:/tmp/cli.binlog
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS dotnet-runtime
FROM debian:trixie-slim@sha256:a72b4433de1cf7d6bae0c4cee74d0547f58601a643b3375f070c51600d63b6b2 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu76 libssl3t64 libstdc++6 libgcc-s1 zlib1g tzdata curl && rm -rf /var/lib/apt/lists/* && useradd --uid 10001 --create-home copilot && mkdir /data && chown copilot:copilot /data && chmod 700 /data
COPY --from=dotnet-runtime /usr/share/dotnet /usr/share/dotnet
COPY --from=tdlib /usr/local/lib/libtdjson.so* /usr/local/lib/
COPY --from=tdlib /licenses/TDLib.txt /app/licenses/TDLib.txt
RUN ldconfig
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH LD_LIBRARY_PATH=/usr/local/lib ASPNETCORE_URLS=http://0.0.0.0:5080 DATA_PATH=/data DOTNET_EnableDiagnostics=0 DOTNET_GCHeapHardLimitPercent=0x32 DOTNET_GCConserveMemory=9
WORKDIR /app
COPY --from=build /publish/api/ ./
COPY --from=build /publish/cli/ ./cli/
USER 10001:10001
VOLUME /data
EXPOSE 5080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s CMD curl -fsS http://127.0.0.1:5080/health/ready || exit 1
ENTRYPOINT ["dotnet", "TelegramCopilot.Api.dll"]
