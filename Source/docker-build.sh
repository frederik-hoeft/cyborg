#!/usr/bin/env bash

set -euo pipefail

build_home="$(/usr/bin/dirname "$(/usr/bin/realpath "${BASH_SOURCE[0]}")")"

docker build --target artifact --output type=local,dest="${build_home}/artifacts" "${build_home}"