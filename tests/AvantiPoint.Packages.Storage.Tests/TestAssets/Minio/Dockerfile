# Test-only, unmodified official MinIO source. Never publish this image.
FROM golang:1.24.13-alpine3.23@sha256:8bee1901f1e530bfb4a7850aa7a479d17ae3a18beb6e09064ed54cfd245b7191 AS build
ARG RESOURCE_REAPER_SESSION_ID
LABEL org.testcontainers.resource-reaper-session=$RESOURCE_REAPER_SESSION_ID
ENV CGO_ENABLED=0 GOTOOLCHAIN=local GOMAXPROCS=2 GOPROXY=https://proxy.golang.org GOSUMDB=sum.golang.org
WORKDIR /src
COPY build-minio.sh Dockerfile README.md /fixture/
# Bound the whole source download, dependency verification, and compilation.
RUN timeout -s KILL 900 sh /fixture/build-minio.sh

FROM scratch
ARG RESOURCE_REAPER_SESSION_ID
LABEL org.testcontainers.resource-reaper-session=$RESOURCE_REAPER_SESSION_ID \
      org.opencontainers.image.source="https://github.com/minio/minio" \
      org.opencontainers.image.revision="9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a" \
      org.opencontainers.image.licenses="AGPL-3.0-only" \
      org.opencontainers.image.description="Local S3 integration-test fixture; do not publish or deploy"
COPY --from=build /out/ /
ENV HOME=/tmp MINIO_BROWSER=off MINIO_UPDATE=off
USER 10001:10001
EXPOSE 9000
ENTRYPOINT ["/minio"]
