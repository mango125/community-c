#!/usr/bin/env bash
set -Eeuo pipefail

readonly APP_DIR="/opt/community-c"
readonly COMPOSE_FILE="${APP_DIR}/compose.production.yaml"
readonly CONTAINER_NAME="community-c"
readonly IMAGE_REPOSITORY="ghcr.io/mango125/community-c"
readonly HEALTH_URL="http://127.0.0.1:8080/"

if [[ $# -ne 1 || ! "$1" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Usage: $0 v<major>.<minor>.<patch>" >&2
  exit 2
fi

readonly VERSION="$1"
readonly NEW_IMAGE="${IMAGE_REPOSITORY}:${VERSION}"

if [[ ! -f "${APP_DIR}/community-c.env" ]]; then
  echo "Missing environment file: ${APP_DIR}/community-c.env" >&2
  exit 1
fi

if [[ ! -f "${COMPOSE_FILE}" ]]; then
  echo "Missing compose file: ${COMPOSE_FILE}" >&2
  exit 1
fi

current_image="$(docker inspect --format '{{.Config.Image}}' "${CONTAINER_NAME}" 2>/dev/null || true)"
next_env="$(mktemp "${APP_DIR}/.image-next.XXXXXX")"
rollback_env="$(mktemp "${APP_DIR}/.image-rollback.XXXXXX")"

cleanup() {
  rm -f "${next_env}" "${rollback_env}"
}
trap cleanup EXIT

write_image_env() {
  local target_file="$1"
  local image="$2"
  printf 'COMMUNITY_C_IMAGE=%s\n' "${image}" > "${target_file}"
  chmod 600 "${target_file}"
}

start_image() {
  local image_env_file="$1"
  docker compose \
    --env-file "${image_env_file}" \
    --file "${COMPOSE_FILE}" \
    up --detach --no-deps web
}

rollback() {
  if [[ -z "${current_image}" ]]; then
    echo "Rollback skipped: no previous image was found." >&2
    return
  fi

  echo "Rolling back to ${current_image}." >&2
  docker rm --force "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  write_image_env "${rollback_env}" "${current_image}"
  start_image "${rollback_env}"
}

write_image_env "${next_env}" "${NEW_IMAGE}"

echo "Pulling ${NEW_IMAGE}."
docker pull "${NEW_IMAGE}"

if docker container inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
  docker stop --time 30 "${CONTAINER_NAME}"
  docker rm "${CONTAINER_NAME}"
fi

if ! start_image "${next_env}"; then
  rollback
  exit 1
fi

for attempt in $(seq 1 20); do
  container_status="$(docker inspect --format '{{.State.Status}}' "${CONTAINER_NAME}" 2>/dev/null || true)"

  if [[ "${container_status}" == "running" ]] && curl --fail --silent --show-error --max-time 5 "${HEALTH_URL}" >/dev/null; then
    install -m 600 "${next_env}" "${APP_DIR}/image.env"
    echo "Deployment completed: ${NEW_IMAGE}"
    exit 0
  fi

  sleep 3
done

echo "Health check failed for ${NEW_IMAGE}." >&2
docker logs --tail 100 "${CONTAINER_NAME}" >&2 || true
rollback
exit 1
