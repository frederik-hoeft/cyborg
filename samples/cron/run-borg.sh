#!/usr/bin/env bash

set -euo pipefail

readonly MAIN_CONFIG="/etc/cyborg/cyborg.jconf"
readonly OPTIONS_CONFIG="/etc/cyborg/cyborg.options.jconf"
readonly METRICS_DIR="/var/log/cyborg/metrics"

: "${CYBORG_HOME:?CYBORG_HOME is not set}"
: "${CYBORG_FREQUENCY:?CYBORG_FREQUENCY is not set}"

case "${CYBORG_FREQUENCY}" in
    daily|weekly|monthly)
        ;;
    *)
        echo "Unsupported CYBORG_FREQUENCY: ${CYBORG_FREQUENCY}" >&2
        exit 1
        ;;
esac

readonly CYBORG_BIN="${CYBORG_HOME}/cyborg"
readonly METRICS_FILE="${METRICS_DIR}/cyborg-${CYBORG_FREQUENCY}.prom"
readonly LOCK_FILE="/var/lock/cyborg.lock"

if [[ ! -x "${CYBORG_BIN}" ]]; then
    echo "Cyborg binary is not executable: ${CYBORG_BIN}" >&2
    exit 1
fi

mkdir -p "${METRICS_DIR}"
exec 9>"${LOCK_FILE}"

if ! flock -n 9; then
    echo "Another backup run is already in progress." >&2
    exit 1
fi

"${CYBORG_BIN}" run \
    --main "${MAIN_CONFIG}" \
    --options "${OPTIONS_CONFIG}" \
    --metrics "${METRICS_FILE}" \
    -e "target=${CYBORG_FREQUENCY},borg.frequency=${CYBORG_FREQUENCY}"