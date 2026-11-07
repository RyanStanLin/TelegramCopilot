FROM debian:trixie-slim AS tdlib
ARG TDLIB_COMMIT=4c6377aa7386c39217878a9a56d581922ad37ca4
ARG BUILD_JOBS=2
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates git cmake g++ gperf libssl-dev zlib1g-dev make && rm -rf /var/lib/apt/lists/*
RUN git init /td && cd /td && git remote add origin https://github.com/tdlib/td.git && git fetch --depth 1 origin ${TDLIB_COMMIT} && git checkout --detach FETCH_HEAD
RUN cmake -S /td -B /td/build -DCMAKE_BUILD_TYPE=Release -DTD_ENABLE_LTO=OFF && cmake --build /td/build --target tdjson --parallel ${BUILD_JOBS}

FROM debian:trixie-slim AS tdlib-artifact
COPY --from=tdlib /td/build/libtdjson.so* /usr/local/lib/
COPY --from=tdlib /td/LICENSE_1_0.txt /licenses/TDLib.txt

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props TelegramCopilot.slnx ./
COPY src/ src/
RUN dotnet publish src/TelegramCopilot.Api -c Release -o /publish/api /bl:/tmp/api.binlog && dotnet publish src/TelegramCopilot.Cli -c Release -o /publish/cli /bl:/tmp/cli.binlog

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS dotnet-runtime
FROM debian:trixie-slim AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu76 libssl3t64 libstdc++6 libgcc-s1 zlib1g tzdata curl && rm -rf /var/lib/apt/lists/* && useradd --uid 10001 --create-home copilot && mkdir /data && chown copilot:copilot /data && chmod 700 /data
COPY --from=dotnet-runtime /usr/share/dotnet /usr/share/dotnet
COPY --from=tdlib /td/build/libtdjson.so* /usr/local/lib/
COPY --from=tdlib /td/LICENSE_1_0.txt /app/licenses/TDLib.txt
RUN ldconfig
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH LD_LIBRARY_PATH=/usr/local/lib ASPNETCORE_URLS=http://0.0.0.0:5080 DataPath=/data DOTNET_EnableDiagnostics=0
WORKDIR /app
COPY --from=build /publish/api/ ./
COPY --from=build /publish/cli/ ./cli/
USER copilot
VOLUME /data
EXPOSE 5080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s CMD curl -fsS http://127.0.0.1:5080/health/live || exit 1
ENTRYPOINT ["dotnet", "TelegramCopilot.Api.dll"]
