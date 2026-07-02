#!/usr/bin/env bash
# Generate a locally-trusted TLS cert for https://localhost and create/refresh the k8s secret
# consumed by k8s/ingress.yaml.
#
# A local root CA is created once (k8s/.certs/ca.crt); trust it with k8s/trust-ca.ps1 so browsers
# stop warning. The leaf cert is (re)issued from that CA on every run. Everything under
# k8s/.certs is git-ignored — private keys never leave your machine.
#
# Requires: openssl + kubectl. On Windows, run from Git Bash (which ships openssl).
#   ./k8s/gen-cert.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1  # stop Git-Bash from mangling openssl "/CN=..." subjects

dir_unix="$(cd "$(dirname "$0")" && pwd)/.certs"
mkdir -p "$dir_unix"
# openssl here is a native Windows binary, so hand it Windows-style paths.
d="$(cygpath -m "$dir_unix" 2>/dev/null || echo "$dir_unix")"

# 1) Local root CA — created once so it stays trusted across re-runs.
if [ ! -f "$dir_unix/ca.crt" ]; then
  openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
    -keyout "$d/ca.key" -out "$d/ca.crt" \
    -subj "/CN=AspireWeb Local Dev CA/O=AspireWeb" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
    -addext "keyUsage=critical,keyCertSign,cRLSign"
fi

# 2) Leaf certificate for localhost, issued by the CA.
printf 'subjectAltName=DNS:localhost,DNS:aspireweb.localtest.me,IP:127.0.0.1\nbasicConstraints=CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n' > "$d/leaf.ext"
openssl req -nodes -newkey rsa:2048 -keyout "$d/tls.key" -out "$d/tls.csr" \
  -subj "/CN=localhost/O=AspireWeb Dev"
openssl x509 -req -in "$d/tls.csr" -CA "$d/ca.crt" -CAkey "$d/ca.key" -CAcreateserial \
  -out "$d/tls.crt" -days 365 -extfile "$d/leaf.ext"
cat "$d/tls.crt" "$d/ca.crt" > "$d/fullchain.crt"

# 3) Create/refresh the k8s TLS secret (leaf + CA chain).
kubectl create namespace aspireweb --dry-run=client -o yaml | kubectl apply -f -
kubectl create secret tls aspireweb-tls -n aspireweb \
  --cert="$d/fullchain.crt" --key="$d/tls.key" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "TLS secret 'aspireweb-tls' created/updated in namespace 'aspireweb'."
echo "Trust the CA once (browsers stop warning):  powershell -File k8s/trust-ca.ps1"
echo "Then apply the ingress:                     kubectl apply -f k8s/ingress.yaml"
