FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24@sha256:0294f285d062afacc8b5f6d7febb0cd4ee879320e5585fa15f2f4ebf1feb9ba7 AS build
WORKDIR /src

COPY Runtime/localtest/src/LocalTest.csproj Runtime/localtest/src/
WORKDIR /src/Runtime/localtest/src
RUN dotnet restore LocalTest.csproj

COPY Runtime/localtest/src/ .
COPY Runtime/common/dotnet/Altinn.Studio.Runtime.Common/HostBridge/ /src/Runtime/common/dotnet/Altinn.Studio.Runtime.Common/HostBridge/
COPY Runtime/common/dotnet/Altinn.Studio.Runtime.Common/EnvTopology/ /src/Runtime/common/dotnet/Altinn.Studio.Runtime.Common/EnvTopology/
RUN dotnet publish LocalTest.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930 AS final
ENV ASPNETCORE_URLS=http://*:5101/
EXPOSE 5101
# Create the storage folder if it isn't mapped to a volume runtime
RUN mkdir /AltinnPlatformLocal
WORKDIR /app
COPY --from=build /app_output .

# Copy various data
COPY Runtime/localtest/testdata /testdata
HEALTHCHECK --interval=1s --timeout=1s --retries=20 \
    CMD wget -nv -t1 --spider 'http://localhost:5101/health' || exit 1

# setup the user and group (not important for LocalTest and this removes write access to /AltinnPlatformLocal)
# RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# USER dotnet

ENTRYPOINT ["dotnet", "LocalTest.dll"]
