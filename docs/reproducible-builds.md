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
- **Alpine packages** (backend): by version and sha256 in `GrifballWebApp.Server/apk.lock`. Each lock's packages
  are published once as their own release, `apk-<first 12 of the lock's sha256>`, and the Dockerfile works out the
  same name from the lock and downloads by sha256. With release immutability on, a published release can't be
  changed or deleted, so the packages any commit needs stay there for good. Alpine's mirrors keep only the newest
  version of each package; `apk-lock.yml` refreshes the lock weekly with a PR. Alpine's own package signatures are
  still checked. (Builds before these releases used the single `apk-archive` release, which is kept.)
- **sqlcmd**: by version and sha256.
- **Time**: every file and the image itself get the commit's time (`SOURCE_DATE_EPOCH`), not the build's. CI
  doesn't reuse layers from other commits' builds, which would carry those commits' times.
- **Labels**: from the commit only. There's no "created" time and no BuildKit provenance; the attestation in
  section 1 replaces it.
- **Git info built into the backend** (the footer's hash and date): written by the server project's own
  `WriteGitInfo` target from a clone holding only the commit, so it's the same however the source was cloned. (It
  replaced a package that stamped the time of the build into its generated code.)
- **BuildKit itself**: pinned in `docker-publish.yml` (`BUILDKIT`), because it does the layer compression.

CI checks all of this weekly, and on any branch by hand: `reproducible.yml` rebuilds master's newest images from
nothing (new builder, no cache) and fails unless the digest matches the image published for the commit.

To do it yourself, use a full clone (not shallow) and Docker with Buildx. `rebuild-image.sh` is the same script the
`reproducible.yml` runs:

```sh
git clone https://github.com/PlayGrifball/GrifballWebApp && cd GrifballWebApp
git checkout <commit>
.github/scripts/rebuild-image.sh client
.github/scripts/rebuild-image.sh server
```

Each run builds both platforms from nothing, prints the digest, and compares it with the published image for the
commit (`sha-<7 chars>`). If your machine can't build one of the platforms (amd64 on an arm64 Mac, or arm64 on a PC),
the script prints the one command that installs emulation for it. A preview image is amd64 only and was built against
the master commit of its time: use `PLATFORMS=linux/amd64 .github/scripts/rebuild-image.sh server <that master commit>`.

## 3. Check the site you're using

The frontend runs in your browser, so you can check what you're actually sent. The client build writes
`build-manifest.json`, with the sha256 of every file the site serves and of every inline script the build can put
in a page. CI attests that manifest. Then:

```sh
cd grifballwebapp.client
npm ci   # verify-site.mjs reads pages with parse5, the HTML standard's parser
node verify-site.mjs https://grifball.xyz --path /seasons --path /leaderboards
```

It:

1. fetches `/build-manifest.json` and checks its attestation with `gh` (built by `docker-publish.yml` on master),
   then prints the commit;
2. downloads every file in the manifest from the site and compares sha256s;
3. loads each page and checks that every script on it is the build's. Every `<script src>`, stylesheet and preload
   must be a file in the manifest. Inline scripts must be the build's own, the loader from
   `src/app/afterFirstPaint.ts` (checked exactly), or Angular's event-replay bootstrap (data only). Build files must
   be loaded with no query string. There must be no other inline handlers, `javascript:` URLs, `<base>`, iframes,
   objects or `srcdoc`. Pages are parsed the way a browser parses them, so differences in quoting or spacing can't
   hide anything.

It exits non-zero and lists anything that isn't the build. It proves what your machine was sent. A dishonest host
could send something else to someone else, but anyone can run it at any time.

## 4. See what an environment runs

Every promotion (`Promote to staging`, `Promote to prod`) publishes a GitHub release: `prod-<date>` (the latest release)
or `staging-<date>` (a pre-release), tagged at the commit the images were built from, and tags both images with the
same name (`docker pull ghcr.io/playgrifball/grifballwebappserver:prod-2026-11-08`). Its notes list both images by
digest, the commands to verify them, and what changed since that environment's previous release; `release.json` has
the same for scripts. With release immutability on, a published release and its tag can't be changed, and GitHub
attests it:

```sh
gh release verify prod-2026-11-08 -R PlayGrifball/GrifballWebApp
gh release download prod-2026-11-08 -R PlayGrifball/GrifballWebApp -p release.json
gh release verify-asset prod-2026-11-08 release.json -R PlayGrifball/GrifballWebApp
```

A release records what was promoted. Like the footer, it can't prove what the server is running right now (see the
top of this page), but every image it names can be verified and rebuilt from its commit (sections 1 and 2).
