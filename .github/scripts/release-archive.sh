#!/usr/bin/env bash
# release-archive.sh <tag> <title> <notes> <list>
#
# Publishes files the image build downloads as one immutable release of this repo, <tag>, if it isn't already: the
# build then needs only this repo's releases, which nobody can change or delete once published (with release
# immutability on), not the original publishers' downloads (apk-archive.sh, sqlcmd-archive.sh). <list> has a line per
# file: "<sha256> <asset name> <source>...", the sources tried in order:
#   https://...                         a URL
#   gh:<owner>/<repo>@<tag>/<asset>     a GitHub release asset, through the API (a working path when github.com's
#                                       download link isn't)
# Each must match its sha256. The release is published only once every file is in it; published, it's sealed.
#
# Runs can do this at the same time (master's runs go side by side, and the Helm chart's workflow does it too), so
# nothing here assumes it's alone: a draft another run made is used, an asset another run uploaded is accepted by its
# digest, and only a complete release is published. What counts at the end is that the published release has every
# file.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, TARGET (the commit a new release's tag points at: a master commit,
# which GitHub signed, so the tag shows as Verified).
set -euo pipefail

tag=${1:?usage: $0 <tag> <title> <notes> <list>} title=${2:?} notes=${3:?} list=${4:?}
repo=${GITHUB_REPOSITORY:?}

releases() { gh api "repos/$repo/releases" --paginate --jq ".[] | select(.tag_name == \"$tag\") | \"\(.id) \(.draft)\""; }
assets() { gh api "repos/$repo/releases/$1" --jq '.assets[] | select(.state == "uploaded") | "\(.name) \(.digest)"'; }
published() { releases | awk '$2 == "false" { print $1; exit }'; }
# Every file of the list in release $1, by GitHub's digest of each; names any that aren't
complete() {
  local have missing=0
  have=$(assets "$1")
  while read -r sha asset _; do
    if [ "$(awk -v a="$asset" '$1 == a { print $2 }' <<< "$have")" != "sha256:$sha" ]; then
      echo "$tag lacks $asset" >&2
      missing=1
    fi
  done < "$list"
  return $missing
}
# Fetches one source to $2
fetch() {
  case "$1" in
    gh:*)
      local spec=${1#gh:}
      local source_repo=${spec%%@*} rest=${spec#*@}
      gh release download "${rest%%/*}" -R "$source_repo" -p "${rest#*/}" -O "$2" --clobber 2> /dev/null ;;
    *) curl -fsSL --retry 5 --retry-all-errors -o "$2" "$1" ;;
  esac
}

id=$(published)
if [ -n "$id" ]; then
  complete "$id" || { echo "::error::$tag is published without every file (above)" >&2; exit 1; }
  echo "$tag: published, all $(wc -l < "$list") files there"
  exit 0
fi

# The draft: another run's if there is one (the oldest, if two runs made one each), else a new one
if [ -z "$(releases)" ]; then
  gh api "repos/$repo/releases" -X POST -f tag_name="$tag" -f target_commitish="${TARGET:?}" -f name="$title" \
    -f body="$notes" -F draft=true -F prerelease=false -f make_latest=false > /dev/null
fi
id=$(releases | awk '$2 == "true" { print $1 }' | sort -n | head -n 1)
[ -n "$id" ] || id=$(published)
[ -n "$id" ] || { echo "::error::No release for $tag after making one" >&2; exit 1; }

work=$(mktemp -d)
have=$(assets "$id")
while read -r sha asset sources; do
  [ "$(awk -v a="$asset" '$1 == a { print $2 }' <<< "$have")" = "sha256:$sha" ] && continue
  got=
  for source in $sources; do
    if fetch "$source" "$work/$asset" && echo "$sha  $work/$asset" | sha256sum -c - > /dev/null 2>&1; then
      got=1
      break
    fi
  done
  [ -n "$got" ] || { echo "::error::$asset: no source had it with sha256 $sha ($sources)" >&2; exit 1; }
  # By the release's id: a draft has no tag yet to find it by. Fails if another run uploaded it meanwhile (or
  # published the release): checked below.
  gh api -X POST "https://uploads.github.com/repos/$repo/releases/$id/assets?name=$asset" \
    -H "Content-Type: application/octet-stream" --input "$work/$asset" > /dev/null \
    || echo "Uploading $asset failed: another run may have; checking below"
done < "$list"

# Published only when complete. Another run may have published it already, or still be uploading: wait for either.
for attempt in 1 2 3 4 5 6 7 8 9 10 11 12; do
  done_id=$(published)
  if [ -n "$done_id" ]; then
    complete "$done_id" || { echo "::error::$tag is published without every file (above)" >&2; exit 1; }
    # A second run's draft, if two made one: not needed now
    releases | awk '$2 == "true" { print $1 }' | while read -r extra; do
      gh api -X DELETE "repos/$repo/releases/$extra" > /dev/null || true
    done
    echo "$tag: published with all $(wc -l < "$list") files"
    exit 0
  fi
  if complete "$id" 2> /dev/null; then
    gh api "repos/$repo/releases/$id" -X PATCH -F draft=false -f make_latest=false > /dev/null \
      || echo "Publishing $tag failed: another run may have; checking"
  fi
  sleep 10
done
complete "$id" || true
echo "::error::$tag wasn't published with every file (above)" >&2
exit 1
