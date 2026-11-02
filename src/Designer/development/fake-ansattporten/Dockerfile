FROM golang:1.26.9-alpine AS build
WORKDIR /src
COPY go.mod ./
RUN go mod download
COPY main.go ./
RUN CGO_ENABLED=0 go build -o /fake-ansattporten .

FROM alpine:3.24@sha256:28bd5fe8b56d1bd048e5babf5b10710ebe0bae67db86916198a6eec434943f8b
COPY --from=build /fake-ansattporten /fake-ansattporten
EXPOSE 8443
ENTRYPOINT ["/fake-ansattporten"]
