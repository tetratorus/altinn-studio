#!/usr/bin/env bash
# Publish the sample reporting app (src/test/apps/frontend-test) with its in-repo Altinn.App references.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"; export PATH="$DOTNET_ROOT:$PATH"
rm -rf app/publish
dotnet publish "$LRR_DIR/../src/test/apps/frontend-test/App/App.csproj" -c Release -o app/publish --nologo -v q

# Real apps get the frontend bundle as static web assets from the Altinn.App.Api NuGet package
# (build/Altinn.App.Api.targets -> /{org}/{app}/altinn-app-frontend/). The sample app uses
# project references instead, so copy the built bundle into wwwroot ourselves.
FE_DIST="$LRR_DIR/../src/App/frontend/dist"
if [[ ! -f "$FE_DIST/altinn-app-frontend.js" ]]; then
  (cd "$LRR_DIR/../src/App/frontend" && yarn build)
fi
mkdir -p app/publish/wwwroot/altinn-app-frontend
cp -r "$FE_DIST"/. app/publish/wwwroot/altinn-app-frontend/
