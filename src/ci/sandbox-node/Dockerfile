FROM --platform=$BUILDPLATFORM docker.io/library/golang:1.26.9-trixie AS builder
ARG TARGETOS
ARG TARGETARCH

WORKDIR /workspace

COPY go.mod go.sum ./
RUN go mod download

COPY . ./
RUN CGO_ENABLED=0 GOOS=${TARGETOS:-linux} GOARCH=${TARGETARCH} \
    go build -trimpath -ldflags="-s -w" -o /sandbox-node ./cmd/sandbox-node

FROM scratch

COPY --from=builder /sandbox-node /sandbox-node

USER 0:0
ENTRYPOINT ["/sandbox-node"]
