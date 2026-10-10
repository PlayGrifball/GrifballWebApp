# PostgreSQL with the periods extension (https://github.com/xocolatl/periods): SQL-standard periods and
# SYSTEM VERSIONING, which keeps every row's history on PostgreSQL as SQL Server's temporal tables do
# (readme.md, Database). The official image doesn't carry it and Alpine has no package, so it's built
# from source here. Published as ghcr.io/playgrifball/postgres-periods for linux/amd64 and linux/arm64
# (.github/workflows/postgres-periods.yml); the Helm chart runs it with database.history.enabled, and
# the tests' PostgreSQL is built from this file (GrifballWebApp.Test/TestDatabase.cs).

# The chart's PostgreSQL (postgres.image in charts/grifballwebapp/values.yaml), by the same digest: the
# extension is built against this exact server, and either image opens the other's data. Renovate
# updates both.
ARG POSTGRES_IMAGE=docker.io/library/postgres:18-alpine@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873

FROM ${POSTGRES_IMAGE} AS build
# A tag, and the commit it must point at: a moved tag fails the build.
ARG PERIODS_VERSION=v1.2.3
ARG PERIODS_COMMIT=f27cd729e5b6fd562464da2cc4778eb630dff6ba
COPY *.patch /patches/
# Two fixes, neither upstream as of v1.2.3 (master the same). Its own regression tests pass with both
# (but for the line numbers in two expected error contexts, the functions being longer):
# - insert-history-plan.patch: periods caches the plan that writes a history row, per history table,
#   but its check is inverted: it prepares and keeps a new plan for every row written, never freeing
#   the old one (about 8 kB per history row, for as long as the connection lives), and keeps using the
#   old plan once the history table is renamed. Fixed: re-planned only when the table's name or row
#   type changed, the replaced plan freed.
# - event-trigger-queries.patch: its event triggers run after every DDL statement, and several of
#   their catalog queries grew with every versioned table (every function's signature as text, every
#   pair of a table's columns, every function per table) and were costed high enough for JIT: with
#   this app's 41 tables, versioning them all took 205 s, and any DDL statement a fraction of one.
#   Fixed: the same checks, only on what may have changed, and no JIT in them (7 s).
# with_llvm=no: no JIT bitcode, so no clang and LLVM of the exact version the server was built with;
# PostgreSQL's JIT just doesn't inline the extension's few C functions.
RUN apk add --no-cache build-base git \
 && git clone --quiet --depth 1 --branch "$PERIODS_VERSION" https://github.com/xocolatl/periods.git /src \
 && test "$(git -C /src rev-parse HEAD)" = "$PERIODS_COMMIT" \
 && git -C /src apply /patches/insert-history-plan.patch /patches/event-trigger-queries.patch \
 && make -C /src with_llvm=no \
 && make -C /src with_llvm=no install DESTDIR=/out

FROM ${POSTGRES_IMAGE}
# periods.so, its control file and SQL scripts.
COPY --from=build /out/usr/local/ /usr/local/
