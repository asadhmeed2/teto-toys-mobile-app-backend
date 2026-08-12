# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

COPY . /source
WORKDIR /source

ARG TARGETARCH

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish TetoToysMobile.sln -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# tzdata is REQUIRED: the alpine runtime ships no IANA timezone database, so
# TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem") throws and /api/store-hours
# falls back to UTC — reporting the shop "open" for hours after it closed.
# icu-libs enables full globalization.
RUN apk add --no-cache tzdata icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app .

# Non-privileged user defined in the base image.
USER $APP_UID

ENTRYPOINT ["dotnet", "TetoToysMobile.Api.dll"]
