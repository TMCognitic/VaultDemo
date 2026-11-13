#!/bin/sh
# Configuration initiale de Vault : moteurs Test/Prod, secrets, policies et AppRoles.
# Script idempotent : il peut être relancé sans erreur.
set -e

echo "Attente de Vault sur $VAULT_ADDR..."
until vault status >/dev/null 2>&1; do sleep 1; done

# Active l'authentification AppRole (ignore l'erreur si déjà active)
vault auth enable approle 2>/dev/null || true

setup_env() {
  ENV="$1"      # Test ou Prod
  VALUE="$2"    # valeur de la clé "data"
  LOWER=$(echo "$ENV" | tr '[:upper:]' '[:lower:]')

  echo "--- Configuration de $ENV ---"

  # 1. Moteur KV v2 monté sur /Test ou /Prod
  vault secrets enable -path="$ENV" kv-v2 2>/dev/null || true

  # 2. Secret "config" contenant la clé data
  vault kv put -mount="$ENV" config data="$VALUE"

  # 3. Policy : lecture seule sur ce moteur uniquement
  vault policy write "${ENV}Policy" - <<EOF
path "${ENV}/data/*" {
  capabilities = ["read"]
}
path "${ENV}/metadata/*" {
  capabilities = ["read", "list"]
}
EOF

  # 4. AppRole liée à la policy
  vault write "auth/approle/role/${ENV}Role" \
    token_policies="${ENV}Policy" \
    token_ttl=1h \
    token_max_ttl=4h

  # 5. Identifiants fixes (pratique en démo, à ne pas faire en prod)
  vault write "auth/approle/role/${ENV}Role/role-id" role_id="${LOWER}-role-id"
  vault write "auth/approle/role/${ENV}Role/custom-secret-id" \
    secret_id="${LOWER}-secret-id" >/dev/null 2>&1 || true
}

setup_env Test ValueTest
setup_env Prod ValueProd

echo "Configuration terminée."