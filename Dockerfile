# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# git: submodule fetch. python3: SS14 build-info tooling. unzip: unpack the package.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git python3 unzip \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Dokploy's checkout (source + .git + .gitmodules).
COPY . .

# Fetch the engine + nested submodules ourselves (all public space-wizards repos,
# no auth). Does NOT depend on Dokploy populating submodules.
RUN git submodule update --init --recursive

# Restore, build the packaging tool, package a linux-x64 server with the client
# embedded (hybrid ACZ) so the launcher self-downloads the client.
RUN dotnet restore \
 && dotnet build Content.Packaging --configuration Release --no-restore \
 && dotnet run --project Content.Packaging server --platform linux-x64 --hybrid-acz

# Unpack the produced server zip.
RUN mkdir -p /app && unzip -o release/SS14.Server_linux-x64.zip -d /app

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

# Server is framework-dependent (--no-self-contained), so the runtime image is required.
# ICU ships with the runtime image; add freetype/fontconfig defensively for the engine.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfreetype6 fontconfig \
 && rm -rf /var/lib/apt/lists/*

RUN useradd --system --create-home --uid 10001 ss14
WORKDIR /app

COPY --from=build /app /app
COPY Docker/server_config.prod.toml /app/server_config.toml
COPY entrypoint.sh /app/entrypoint.sh

# Strip any CR (in case the script was checked out with CRLF) so the shebang works.
RUN sed -i 's/\r$//' /app/entrypoint.sh \
 && chmod +x /app/entrypoint.sh /app/Robust.Server \
 && mkdir -p /data \
 && chown -R ss14:ss14 /app /data

USER ss14

# UDP = gameplay (direct host port); TCP = status/launcher (frontable via Traefik).
EXPOSE 1212/udp 1212/tcp

ENTRYPOINT ["/app/entrypoint.sh"]
