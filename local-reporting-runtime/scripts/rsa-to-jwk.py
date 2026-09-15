#!/usr/bin/env python3
"""Convert a PEM RSA private key into private and public RSA JWKs (RFC 7517/7518).

Usage: rsa-to-jwk.py <key.pem> <kid> <private.jwk.json> <public.jwk.json>
Only depends on openssl (for the key parameters) and the standard library.
"""
import base64
import json
import re
import subprocess
import sys


def b64url(n: int) -> str:
    raw = n.to_bytes((n.bit_length() + 7) // 8, "big")
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


key_path, kid, priv_out, pub_out = sys.argv[1:5]
text = subprocess.check_output(["openssl", "rsa", "-in", key_path, "-noout", "-text"], text=True)

fields = {}
current = None
for line in text.splitlines():
    m = re.match(r"^(modulus|publicExponent|privateExponent|prime1|prime2|exponent1|exponent2|coefficient):\s*(.*)$", line)
    if m:
        current = m.group(1)
        fields[current] = m.group(2).strip()
    elif current and line.startswith("    "):
        fields[current] += line.strip()
    else:
        current = None


def to_int(name: str) -> int:
    v = fields[name]
    if name == "publicExponent":
        return int(v.split()[0])
    return int(v.replace(":", ""), 16)


pub = {"kty": "RSA", "use": "sig", "alg": "RS256", "kid": kid,
       "n": b64url(to_int("modulus")), "e": b64url(to_int("publicExponent"))}
priv = dict(pub, d=b64url(to_int("privateExponent")), p=b64url(to_int("prime1")), q=b64url(to_int("prime2")),
            dp=b64url(to_int("exponent1")), dq=b64url(to_int("exponent2")), qi=b64url(to_int("coefficient")))

with open(priv_out, "w") as f:
    json.dump(priv, f)
with open(pub_out, "w") as f:
    json.dump(pub, f)
