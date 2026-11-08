#!/usr/bin/env python3
"""diff-oci.py <a.tar> <b.tar>: where two builds of an image differ, when the reproducible job finds they do.

Both are OCI layouts (docker buildx build --output type=oci). Prints the configs' differences, then for each layer
that differs, every file whose content, mode, owner, size or time differs, so the cause can be fixed at its source.
"""
import hashlib, io, json, sys, tarfile

def blobs(path):
    t = tarfile.open(path)
    read = lambda name: t.extractfile(name).read()
    def blob(digest):
        return read("blobs/" + digest.replace(":", "/"))
    index = json.loads(read("index.json"))
    manifests = []
    def walk(desc):
        doc = json.loads(blob(desc["digest"]))
        if "manifests" in doc:
            for d in doc["manifests"]:
                walk(d)
        else:
            manifests.append((desc.get("platform", {}), doc))
    for d in index["manifests"]:
        walk(d)
    return blob, manifests

def entries(data):
    out = {}
    with tarfile.open(fileobj=io.BytesIO(data)) as layer:
        for m in layer:
            content = layer.extractfile(m).read() if m.isfile() else b""
            out[m.name] = (m.type, oct(m.mode), m.uid, m.gid, m.size, m.mtime, m.linkname,
                           hashlib.sha256(content).hexdigest()[:16])
    return out

blob_a, ma = blobs(sys.argv[1])
blob_b, mb = blobs(sys.argv[2])
for (pa, a), (pb, b) in zip(ma, mb):
    print(f"== {pa.get('os')}/{pa.get('architecture')}")
    ca, cb = json.loads(blob_a(a["config"]["digest"])), json.loads(blob_b(b["config"]["digest"]))
    for key in sorted(set(ca) | set(cb)):
        if ca.get(key) != cb.get(key) and key != "rootfs":
            print(f"config {key}:\n  a: {json.dumps(ca.get(key))[:400]}\n  b: {json.dumps(cb.get(key))[:400]}")
    for i, (la, lb) in enumerate(zip(a["layers"], b["layers"])):
        if la["digest"] == lb["digest"]:
            continue
        history = [h for h in ca.get("history", []) if not h.get("empty_layer")]
        print(f"layer {i} differs: {history[i].get('created_by', '')[:200] if i < len(history) else ''}")
        ea, eb = entries(blob_a(la["digest"])), entries(blob_b(lb["digest"]))
        shown = 0
        for name in sorted(set(ea) | set(eb)):
            if ea.get(name) != eb.get(name):
                print(f"  {name}\n    a: {ea.get(name)}\n    b: {eb.get(name)}")
                shown += 1
                if shown >= 40:
                    print("  ...")
                    break
        if shown == 0:
            print("  same files: the difference is in how the layer was packed")
