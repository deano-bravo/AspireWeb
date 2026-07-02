#!/usr/bin/env bash
# Generate a self-signed TLS cert for local HTTPS and create/refresh the Kubernetes secret
# consumed by k8s/ingress.yaml. The private key stays local (k8s/.certs is git-ignored).
#
# Requires: openssl + kubectl. On Windows, run from Git Bash (which ships openssl).
#   ./k8s/gen-cert.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1  # stop Git-Bash from mangling the openssl "/CN=..." subject

dir_unix="$(cd "$(dirname "$0")" && pwd)/.certs"
mkdir -p "$dir_unix"
# openssl here is a native Windows binary, so hand it Windows-style paths.
dir="$(cygpath -m "$dir_unix" 2>/dev/null || echo "$dir_unix")"

openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
  -keyout "$dir/tls.key" -out "$dir/tls.crt" \
  -subj "/CN=localhost/O=AspireWeb Dev" \
  -addext "subjectAltName=DNS:localhost,DNS:aspireweb.localtest.me,IP:127.0.0.1"

kubectl create namespace aspireweb --dry-run=client -o yaml | kubectl apply -f -
kubectl create secret tls aspireweb-tls -n aspireweb \
  --cert="$dir/tls.crt" --key="$dir/tls.key" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "TLS secret 'aspireweb-tls' created/updated in namespace 'aspireweb'."
echo "Next: kubectl apply -f k8s/ingress.yaml"
