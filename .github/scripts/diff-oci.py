#!/usr/bin/env python3
"""diff-oci.py <a.tar> <b.tar>: where two builds of an image differ, when the reproducible job finds they do.

Both are OCI layouts (docker buildx build --output type=oci). Prints the configs' differences, then for each layer
that differs, every file whose content, mode, owner, size or time differs, so the cause can be fixed at its source.
"""
import hashlib, io, json, os, re, sys, tarfile

def tarball(arg):
    """A tarball named on the command line, only inside the directory the builds are written to: the runner's
    temporary directory in CI (docker-publish.yml), else the current one."""
    base = os.path.realpath(os.environ.get("RUNNER_TEMP") or os.getcwd())
    path = os.path.realpath(arg)
    if not path.startswith(base + os.sep):
        sys.exit(f"diff-oci.py: {arg} is outside {base}")
    return path

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

def strings(data):
    """Text runs in a binary: ASCII, and UTF-16 (.NET keeps its string literals so)."""
    ascii = {m.decode() for m in re.findall(rb"[\x20-\x7e]{6,}", data)}
    utf16 = {m.decode("utf-16-le") for m in re.findall(rb"(?:[\x20-\x7e]\x00){6,}", data)}
    return ascii | utf16

def explain(a, b):
    """For two versions of one file: the first byte that differs, and the text in only one of them."""
    first = next((i for i, (x, y) in enumerate(zip(a, b)) if x != y), min(len(a), len(b)))
    print(f"    first difference at byte {first} of {len(a)}/{len(b)}; a {a[first:first + 24].hex()} b {b[first:first + 24].hex()}")
    sa, sb = strings(a), strings(b)
    for label, only in (("only in a", sa - sb), ("only in b", sb - sa)):
        for s in sorted(only)[:15]:
            print(f"    {label}: {s[:160]}")

def contents(data):
    with tarfile.open(fileobj=io.BytesIO(data)) as layer:
        return {m.name: layer.extractfile(m).read() for m in layer if m.isfile() and m.size < 200_000_000}

def entries(data):
    out = {}
    with tarfile.open(fileobj=io.BytesIO(data)) as layer:
        for m in layer:
            content = layer.extractfile(m).read() if m.isfile() else b""
            out[m.name] = (m.type, oct(m.mode), m.uid, m.gid, m.size, m.mtime, m.linkname,
                           hashlib.sha256(content).hexdigest()[:16])
    return out

blob_a, ma = blobs(tarball(sys.argv[1]))
blob_b, mb = blobs(tarball(sys.argv[2]))
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
        da, db = blob_a(la["digest"]), blob_b(lb["digest"])
        ea, eb = entries(da), entries(db)
        ca_files, cb_files = contents(da), contents(db)
        shown = 0
        for name in sorted(set(ea) | set(eb)):
            if ea.get(name) != eb.get(name):
                print(f"  {name}\n    a: {ea.get(name)}\n    b: {eb.get(name)}")
                if name in ca_files and name in cb_files and ca_files[name] != cb_files[name]:
                    explain(ca_files[name], cb_files[name])
                    # A PDB that differs: saved for pdb-inputs.cs, which lists what each was compiled from
                    if name.endswith(".pdb") and os.environ.get("PDB_OUT"):
                        base = os.path.join(os.environ["PDB_OUT"], os.path.basename(name))
                        open(base + ".a", "wb").write(ca_files[name])
                        open(base + ".b", "wb").write(cb_files[name])
                shown += 1
                if shown >= 40:
                    print("  ...")
                    break
        if shown == 0:
            print("  same files: the difference is in how the layer was packed")
