# syntax=docker/dockerfile:1.7
#
# ILGPUC.CompilerService — OpenCL image (Intel oneAPI offline compiler)
#
# Compiler: Intel `ocloc` (Offline Compiler) installed at /usr/bin/ocloc.
#       This is the path that ILGPUC.Compilers/OclocCompiler.cs:20
#       (`DefaultPath`) expects, so the in-tree compiler wrapper picks it
#       up with no override.
#
# Base: ubuntu:24.04 + Intel's graphics compute-runtime apt repository
#       (https://repositories.intel.com/gpu/ubuntu noble unified). The
#       package `intel-ocloc` ships from intel/compute-runtime and is the
#       supported standalone way to get `/usr/bin/ocloc` without dragging
#       in the rest of oneAPI. We deliberately do NOT use the heavier
#       intel/oneapi-basekit image (~5GB) — the offline compiler is the
#       only Intel piece we need at compile time, and ocloc is the
#       compiler-only path.
#
# Targets: Intel CPU OpenCL devices and Intel GPU OpenCL devices. The
#       compiler-only image is useful as a remote builder even on machines
#       without an Intel GPU — ocloc compiles to SPIR-V / device IR without
#       touching the device.
#
# Architectures supported by the base: linux/amd64 only. Intel does not
#       publish ARM64 builds of ocloc.
#
# Build context: REPO ROOT (the parent of Src/), so the build can pick up
#   - global.json
#   - Src/Directory.Build.props
#   - Tools/CheckStyles/ILGPU.CheckStyles.targets   (imported by props above)
#   - Src/ILGPU/Properties/ILGPU.nuspec.targets     (imported by ILGPU.csproj)
#
# Build:
#   docker build --platform linux/amd64 \
#       -f Src/docker/Dockerfile.opencl \
#       -t ilgpuc-compiler-service-opencl:latest .
#
# Run (no GPU access required for compilation only):
#   docker run --rm -d -p 5003:5000 \
#       --name ilgpuc-opencl \
#       ilgpuc-compiler-service-opencl:latest
#

############################
# Stage 1: build
############################
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# --- Restore-cache layer ---------------------------------------------------
COPY global.json ./
COPY Src/Directory.Build.props                                Src/
COPY Tools/CheckStyles/ILGPU.CheckStyles.targets              Tools/CheckStyles/

COPY Src/ILGPU/ILGPU.csproj                                   Src/ILGPU/
COPY Src/ILGPU/Properties/ILGPU.nuspec.targets                Src/ILGPU/Properties/
COPY Src/ILGPUC.Compilers/ILGPUC.Compilers.csproj             Src/ILGPUC.Compilers/
COPY Src/ILGPUC.CompilerService/ILGPUC.CompilerService.csproj Src/ILGPUC.CompilerService/

RUN dotnet restore Src/ILGPUC.CompilerService/ILGPUC.CompilerService.csproj

# --- Source + publish ------------------------------------------------------
# ILGPU's T4 templates pull `.ttinclude` files from Src/ILGPUC/Static/ via
# `<#@ include file="../../ILGPUC/Static/..." #>`, so the static directory
# from the (otherwise unrelated) ILGPUC project must be present at codegen
# time. We do NOT need to build ILGPUC itself.
COPY Src/ILGPU/                  Src/ILGPU/
COPY Src/ILGPUC/Static/          Src/ILGPUC/Static/
COPY Src/ILGPUC.Compilers/       Src/ILGPUC.Compilers/
COPY Src/ILGPUC.CompilerService/ Src/ILGPUC.CompilerService/

RUN dotnet publish Src/ILGPUC.CompilerService/ILGPUC.CompilerService.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

############################
# Stage 2: runtime (Intel oneAPI ocloc)
############################
FROM ubuntu:24.04 AS runtime

# OCI labels — auto-link the published GHCR package back to the source repo
# (so it appears under the repo's Packages tab) and tag it with the compound
# license set that actually applies: ILGPU code is NCSA; Intel ocloc and
# Intel Graphics Compiler (libigc2, libigdfcl2) are both MIT-licensed.
LABEL org.opencontainers.image.source="https://github.com/m4rs-mt/ILGPU"
LABEL org.opencontainers.image.licenses="NCSA AND MIT"
LABEL org.opencontainers.image.description="ILGPUC CompilerService bundled with the Intel ocloc OpenCL offline compiler. Used as a remote compile-only service AND as a CI test runner: `docker run image dotnet test ...` overrides the default CMD to run tests inside the container with the .NET 10 SDK preinstalled."

# Bundle ILGPU's own license texts. Third-party licenses are already present
# in the image: intel-ocloc, libigc2, libigdfcl2, and every Ubuntu base
# package ship their copyright / license text at /usr/share/doc/<package>/
# copyright per Debian convention — no further bundling needed for those.
COPY LICENSE.txt LICENSE-3RD-PARTY.txt /usr/share/doc/ilgpuc/

# Index pointing at every license text already present in the image
# filesystem, so a consumer inspecting the image has a single starting point.
RUN printf '%s\n' \
    'This image bundles the Intel Graphics Compute Runtime offline compiler' \
    '(intel-ocloc) and the Intel Graphics Compiler (libigc2, libigdfcl2),' \
    'all licensed under the MIT License by Intel Corporation. Per-package' \
    'license texts are in the image at:' \
    '  /usr/share/doc/intel-ocloc/copyright' \
    '  /usr/share/doc/libigc2/copyright' \
    '  /usr/share/doc/libigdfcl2/copyright' \
    'Upstream copies:' \
    '  https://github.com/intel/compute-runtime/blob/master/LICENSE.md' \
    '  https://github.com/intel/intel-graphics-compiler/blob/master/LICENSE.md' \
    '' \
    'ILGPU source code is licensed under the University of Illinois/NCSA' \
    'Open Source License. The full text is present in the image at:' \
    '  /usr/share/doc/ilgpuc/LICENSE.txt' \
    'Attributions for bundled third-party ILGPU dependencies are at:' \
    '  /usr/share/doc/ilgpuc/LICENSE-3RD-PARTY.txt' \
    > /usr/share/doc/ilgpuc/NOTICES

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false \
    ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    PATH=/usr/share/dotnet:$PATH

# Install Intel ocloc (Intel Graphics Compute Runtime offline compiler) +
# ASP.NET Core 10 runtime in a single layer.
#
# `intel-ocloc` is published by Intel via the compute-runtime apt repo at
# https://repositories.intel.com/gpu/ubuntu (the same repo that ships
# intel-opencl-icd, level-zero, etc.). The package installs the binary
# directly at /usr/bin/ocloc — exactly what
# ILGPUC.Compilers/OclocCompiler.cs:20 (DefaultPath = "/usr/bin/ocloc")
# expects, so no symlink or env-var override is needed.
#
# `libigc2` and `libigdfcl2` are NOT pulled by intel-ocloc as hard
# dependencies even though ocloc dlopen()s them at runtime. Without
# `libigdfcl2`, ocloc fails every compile with:
#   Error! Loading of FCL library has failed! Filename: libigdfcl.so.2
#   Error! FCL initialization failure. Error code = -6
# Install both libraries explicitly.
#
# Note: ocloc is offline-compile-only. It does NOT need a GPU device or
# the Intel ICD at compile time, which is what makes this image usable
# as a remote compiler service even on hosts without Intel hardware.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg2 libicu74 libssl3 libstdc++6 zlib1g tzdata \
 && curl -fsSL https://repositories.intel.com/gpu/intel-graphics.key \
      | gpg --dearmor -o /usr/share/keyrings/intel-graphics.gpg \
 && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/intel-graphics.gpg] " \
         "https://repositories.intel.com/gpu/ubuntu noble unified" \
      > /etc/apt/sources.list.d/intel-gpu.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
        intel-ocloc \
        libigc2 \
        libigdfcl2 \
 && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --channel 10.0 \
        --install-dir /usr/share/dotnet \
 && rm /tmp/dotnet-install.sh \
 && rm -rf /var/lib/apt/lists/*

# NOTE: the dotnet-install.sh invocation above installs the FULL .NET 10 SDK
# (not just --runtime aspnetcore) so the image is usable both as the
# CompilerService runtime and as a CI test runner via `docker run image
# dotnet test ...`. See Dockerfile.cuda for the rationale.

# Non-root user. Ubuntu 24.04 ships with a default `ubuntu` user at uid/gid
# 1000; same dance as Dockerfile.cuda / Dockerfile.rocm.
RUN (userdel -r ubuntu 2>/dev/null || true) \
 && (groupdel ubuntu 2>/dev/null || true) \
 && groupadd --system --gid 1000 ilgpuc \
 && useradd  --system --uid 1000 --gid 1000 \
             --home /home/ilgpuc --shell /usr/sbin/nologin ilgpuc \
 && mkdir -p /home/ilgpuc /app \
 && chown -R ilgpuc:ilgpuc /home/ilgpuc /app

WORKDIR /app
COPY --from=build --chown=ilgpuc:ilgpuc /app/publish ./

USER ilgpuc

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:5000/api/v1/status || exit 1

# CMD (not ENTRYPOINT) so `docker run image dotnet test ...` cleanly
# overrides the default. The default behavior is unchanged: `docker run
# image` (with no args) starts the CompilerService exactly like before.
CMD ["dotnet", "ILGPUC.CompilerService.dll"]
