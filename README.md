# VaultDemo — POC HashiCorp Vault + ASP.NET Core

> ⚠️ **Ce dépôt est un POC (Proof of Concept).**
> Il sert uniquement à montrer comment une API .NET peut lire des secrets dans HashiCorp Vault avec l'authentification AppRole.
> Il **n'est pas prévu pour la production** : Vault tourne en mode `dev` (en mémoire, sans TLS, token root fixe), les identifiants AppRole sont fixes et écrits en clair, et l'API journalise des valeurs sensibles.

## Objectif

Montrer qu'**un même code applicatif** lit des secrets différents selon l'environnement (**Test** ou **Prod**), en changeant **uniquement la configuration** :

- chaque environnement a son propre moteur de secrets KV v2 (`Test` / `Prod`) ;
- chaque environnement a sa propre policy en lecture seule (`TestPolicy` / `ProdPolicy`) ;
- chaque environnement a son propre AppRole (`TestRole` / `ProdRole`).

Le rôle `TestRole` ne peut lire que `Test/`, et `ProdRole` ne peut lire que `Prod/`.

## Architecture

```
┌──────────────────────┐   AppRole login (role_id / secret_id)   ┌──────────────────────┐
│   VaultDemo.Api      │ ──────────────────────────────────────▶ │  HashiCorp Vault     │
│   (ASP.NET Core)     │                                         │  (docker, mode dev)  │
│                      │ ◀────── lecture KV v2 <Mount>/config ── │                      │
│   GET /status        │                                         │  Test/config → ValueTest
└──────────────────────┘                                         │  Prod/config → ValueProd
                                                                 └──────────────────────┘
```

| Élément | Rôle |
|---|---|
| [docker-compose.yml](docker-compose.yml) | Lance Vault (`hashicorp/vault:2.2`, mode dev) et un conteneur d'initialisation à usage unique |
| [HashiCorp/init/init.sh](HashiCorp/init/init.sh) | Script idempotent : active AppRole, crée les moteurs `Test`/`Prod`, les secrets, les policies et les rôles |
| [VaultDemo.Api/Program.cs](VaultDemo.Api/Program.cs) | Minimal API qui s'authentifie via [VaultSharp](https://github.com/rajanadar/VaultSharp) et expose `GET /status` |
| [VaultDemo.Api/infrastructure/VaultSettings.cs](VaultDemo.Api/infrastructure/VaultSettings.cs) | Section de configuration `Vault` |

## Prérequis

- [Docker](https://www.docker.com/) avec Docker Compose
- [.NET SDK 11](https://dotnet.microsoft.com/)

## Démarrage

### 1. Lancer Vault et l'initialiser

```bash
docker compose up -d
```

Le conteneur `vault-init` attend que Vault soit prêt, applique la configuration puis s'arrête. Pour vérifier :

```bash
docker logs vault-init
```

Interface web : <http://localhost:8200> (token : `root-demo`).

### 2. Configurer l'API

La section `Vault` de [appsettings.json](VaultDemo.Api/appsettings.json) pointe par défaut sur **Prod** :

```json
"Vault": {
  "Address": "http://localhost:8200",
  "RoleId": "prod-role-id",
  "SecretId": "prod-secret-id",
  "MountPoint": "Prod",
  "SecretPath": "config"
}
```

Pour basculer sur **Test** en local sans toucher au fichier, utilise les *user secrets* :

```bash
cd VaultDemo.Api
dotnet user-secrets set "Vault:RoleId" "test-role-id"
dotnet user-secrets set "Vault:SecretId" "test-secret-id"
dotnet user-secrets set "Vault:MountPoint" "Test"
```

### 3. Lancer l'API

```bash
dotnet run --project VaultDemo.Api --launch-profile https
```

Puis appelle l'endpoint :

```bash
curl -k https://localhost:7103/status
```

Réponse attendue :

```json
{ "mountPoint": "Test", "data": "ValueTest" }
```

ou, avec la configuration Prod :

```json
{ "mountPoint": "Prod", "data": "ValueProd" }
```

## Données créées par le script d'initialisation

| Environnement | Moteur KV v2 | Secret | Policy | AppRole | role_id | secret_id |
|---|---|---|---|---|---|---|
| Test | `Test/` | `config` → `data=ValueTest` | `TestPolicy` | `TestRole` | `test-role-id` | `test-secret-id` |
| Prod | `Prod/` | `config` → `data=ValueProd` | `ProdPolicy` | `ProdRole` | `prod-role-id` | `prod-secret-id` |

Les policies donnent uniquement `read` sur `<Env>/data/*` et `read`/`list` sur `<Env>/metadata/*`.
Les tokens obtenus ont un TTL d'1 h (4 h max).

## Limites du POC (à ne pas reproduire en production)

- **Vault en mode `dev`** : stockage en mémoire (tout est perdu au redémarrage), pas de TLS, unseal automatique, token root fixe (`root-demo`).
- **Identifiants AppRole fixes et en clair** dans le script et dans `appsettings.json`. En production, le `secret_id` doit être généré, avoir une durée de vie courte et être injecté de façon sécurisée (response wrapping, orchestrateur, etc.).
- **Secrets journalisés** : l'endpoint `/status` écrit `RoleId` et `SecretId` dans les logs, pour faciliter le débogage de la démo.
- **Pas de renouvellement de token** ni de cache des secrets côté API.
- **Pas de tests** ni de gestion d'erreur poussée (Vault indisponible, accès refusé, etc.).
- Le fichier [VaultDemo.Api.http](VaultDemo.Api/VaultDemo.Api.http) vient encore du template et ne cible pas `/status`.

## Arrêt

```bash
docker compose down
```
