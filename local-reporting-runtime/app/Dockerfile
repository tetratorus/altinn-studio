# Runtime image for the in-repo sample app (ttd/frontend-test). The app is published on the host
# with the monorepo's project references (see scripts/build-app.sh); this image only hosts the output.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
EXPOSE 5005
WORKDIR /app
COPY publish/ .
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet \
 && mkdir -p /tmp/logtelemetry && chown dotnet /tmp/logtelemetry
USER dotnet
ENTRYPOINT ["dotnet", "Altinn.Application.dll"]
