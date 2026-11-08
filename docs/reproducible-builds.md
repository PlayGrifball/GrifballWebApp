# Reproducible builds and verifying the site

The site is self-hosted, so "the footer says commit X" is only the host's word. This page shows how to check it
without trusting the host.

| What | How it's proven | You need |
| --- | --- | --- |
| An image on GHCR was built from commit X by this repo's CI | GitHub build provenance attestation, signed through Sigstore and logged publicly in Rekor | `gh` |
| The image contains exactly what commit X builds | Reproducible builds: rebuild it yourself and get the same digest | Docker with Buildx |
| The site sends your browser exactly commit X's frontend | `grifballwebapp.client/verify-site.mjs`, checked against the attested build manifest | Node 22+, `gh` |

What can't be proven from outside: which backend image the server is actually running, and what's in its database.
The backend runs on the host's hardware, and nothing it reports about itself is proof. What you can check is that
the image published for each commit is exactly that commit, and that the frontend you were sent is.

## 1. Check an image's provenance

Every image pushed from master has a GitHub attestation. It ties the image digest to the repo, the commit and the
workflow run:

```sh
gh attestation verify oci://ghcr.io/playgrifball/grifballwebappserver:sha-<7 chars> \
  -R PlayGrifball/GrifballWebApp \
  --signer-workflow PlayGrifball/GrifballWebApp/.github/workflows/docker-publish.yml \
  --source-ref refs/heads/master
```

(The client image is `grifballwebappclient`.) The images are also cosign-signed by the same workflow, which the
homelab cluster checks before running them.

## 2. Rebuild an image and compare

The same commit builds the same image, byte for byte, anywhere. Every input is pinned:

- **Base images**: by digest in the Dockerfiles. Renovate keeps the digests current.
- **npm and NuGet packages**: `npm ci` installs exactly `package-lock.json`. NuGet packages and the `dotnet-ef`
  tool are pinned by version, and published NuGet versions never change.
- **Alpine packages** (backend): by version and sha256 in `GrifballWebApp.Server/apk.lock`, downloaded from this
  repo's [`apk-archive`](https://github.com/PlayGrifball/GrifballWebApp/releases/tag/apk-archive) release.
  Alpine's mirrors keep only the newest version of each package, so the archive keeps every version a build has
  used. `apk-lock.yml` refreshes the lock weekly with a PR. Alpine's own package signatures are still checked.
- **sqlcmd**: by version and sha256.
- **Time**: `SOURCE_DATE_EPOCH` is the commit's time, and BuildKit rewrites every file's timestamp to it.
- **Labels**: from the commit only. There's no "created" time and no BuildKit provenance; the attestation in
  section 1 replaces it.
- **Git info built into the backend** (the footer's hash): the build detaches HEAD and uses 7-character hashes,
  so it's the same in every clone.
- **BuildKit itself**: pinned in `docker-publish.yml` (`BUILDKIT`), because it does the layer compression.

CI checks all of this on every PR and every master push. The `reproducible` job in `docker-publish.yml` rebuilds
each image from nothing (new builder, no cache) and fails unless the digest matches the image pushed.

To do it yourself, use a full clone (not shallow) and Linux or WSL:

```sh
git clone https://github.com/PlayGrifball/GrifballWebApp && cd GrifballWebApp
git checkout <commit>
# The BuildKit image this commit's CI used:
BUILDKIT=$(sed -n -E 's/^  BUILDKIT: image=(.*)$/\1/p' .github/workflows/docker-publish.yml)
docker buildx create --name repro --driver docker-container --driver-opt "image=$BUILDKIT" --use
docker run --privileged --rm tonistiigi/binfmt --install arm64   # if your machine isn't arm64
ARGS="--platform linux/amd64,linux/arm64 --provenance=false --output type=image,push=false,rewrite-timestamp=true
      --build-arg SOURCE_DATE_EPOCH=$(git log -1 --format=%ct) --build-arg SOURCE_REVISION=$(git rev-parse HEAD)"

# Frontend
docker buildx build $ARGS --metadata-file client.json grifballwebapp.client
jq -r '."containerimage.digest"' client.json

# Backend (master's migration list goes into the image: for a master commit, its own)
.github/scripts/master-migrations.sh .master-migrations HEAD
docker buildx build $ARGS --metadata-file server.json -f GrifballWebApp.Server/Dockerfile .
jq -r '."containerimage.digest"' server.json

# The published images, to compare
docker buildx imagetools inspect ghcr.io/playgrifball/grifballwebappclient:sha-$(git rev-parse --short=7 HEAD) --format '{{json .Manifest.Digest}}'
docker buildx imagetools inspect ghcr.io/playgrifball/grifballwebappserver:sha-$(git rev-parse --short=7 HEAD) --format '{{json .Manifest.Digest}}'
```

## 3. Check the site you're using

The frontend runs in your browser, so you can check what you're actually sent. The client build writes
`build-manifest.json`, with the sha256 of every file the site serves and of every inline script the build can put
in a page. CI attests that manifest. Then:

```sh
cd grifballwebapp.client
node verify-site.mjs https://grifball.xyz --path /seasons --path /leaderboards
```

It:

1. fetches `/build-manifest.json` and checks its attestation with `gh` (built by `docker-publish.yml` on master),
   then prints the commit;
2. downloads every file in the manifest from the site and compares sha256s;
3. loads each page and checks that every script on it is the build's. Every `<script src>`, stylesheet and preload
   must be a file in the manifest. Inline scripts must be the build's own, the loader from
   `src/app/afterFirstPaint.ts` (checked exactly), or Angular's event-replay bootstrap (data only). No other inline
   handlers or `javascript:` URLs are allowed.

It exits non-zero and lists anything that isn't the build. It proves what your machine was sent. A dishonest host
could send something else to someone else, but anyone can run it at any time.
