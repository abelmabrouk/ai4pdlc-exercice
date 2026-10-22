#!/bin/sh
# Boucle de développement de l'API (utilisée par docker-compose) : recompile et redémarre l'API
# dès qu'un fichier source est ajouté, modifié ou supprimé. Plus fiable que `dotnet watch`
# sur un volume monté depuis l'hôte.
cd /src

fingerprint() {
  find src -type f \( -name '*.cs' -o -name '*.csproj' -o -name 'appsettings*.json' \) \
    -not -path '*/bin/*' -not -path '*/obj/*' -exec stat -c '%n %s %Y' {} + 2>/dev/null | sort | md5sum
}

while true; do
  current="$(fingerprint)"
  echo "dev-watch: compilation…"
  if dotnet build src/Conges.Api --nologo -v q -clp:ErrorsOnly; then
    dotnet run --project src/Conges.Api --no-build --no-launch-profile &
    pid=$!
  else
    echo "dev-watch: échec de compilation, en attente d'une modification…"
    pid=""
  fi
  while [ "$(fingerprint)" = "$current" ]; do sleep 2; done
  echo "dev-watch: modification détectée, redémarrage"
  if [ -n "$pid" ]; then kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; fi
done
