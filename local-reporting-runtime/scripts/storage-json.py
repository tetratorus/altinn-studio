#!/usr/bin/env python3
"""Re-key app metadata / text resources the way Storage persists them.

Storage writes these jsonb columns through Npgsql dynamic JSON (System.Text.Json defaults), so the
stored keys are the C# property names (PascalCase), not the camelCase of the source files.
Language-keyed dictionaries (title, description, rightDescription) keep their keys.
Usage: storage-json.py app <applicationmetadata.json> | storage-json.py text <resource.xx.json> <org> <app>
"""
import json, sys

DICT_KEYS = {"title", "description", "rightDescription"}

def pascal(k): return k[:1].upper() + k[1:]

def rekey(o, parent=None):
    if isinstance(o, dict):
        if parent in DICT_KEYS:
            return o
        return {pascal(k): rekey(v, k) for k, v in o.items() if not k.startswith("$")}
    if isinstance(o, list):
        return [rekey(v, parent) for v in o]
    return o

mode, path = sys.argv[1], sys.argv[2]
src = json.load(open(path, encoding="utf-8-sig"))
if mode == "text":
    org, app = sys.argv[3], sys.argv[4]
    src = {"id": f"{org}-{app}-{src['language']}", "org": org, **src}
print(json.dumps(rekey(src)))
