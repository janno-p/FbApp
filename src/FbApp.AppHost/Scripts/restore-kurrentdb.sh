#!/bin/sh
set -eu

DATA_DIR="${DATA_DIR:-/var/lib/kurrentdb}"
BACKUP_DIR="${BACKUP_DIR:-/backup}"

# KurrentDB container expects to be able to write database/checkpoint files.
KURRENT_UID="${KURRENT_UID:-1000}"
KURRENT_GID="${KURRENT_GID:-1000}"

echo "Restoring KurrentDB backup from $BACKUP_DIR into $DATA_DIR"

rm -rf "$DATA_DIR"/* "$DATA_DIR"/.[!.]* "$DATA_DIR"/..?* 2>/dev/null || true
mkdir -p "$DATA_DIR"

# Layout A: backup directory is the KurrentDB data directory itself.
if [ -f "$BACKUP_DIR/chaser.chk" ]; then
  cp -a "$BACKUP_DIR"/. "$DATA_DIR"/

# Layout B: backup has db/ and index/ folders.
elif [ -f "$BACKUP_DIR/db/chaser.chk" ]; then
  cp -a "$BACKUP_DIR/db"/. "$DATA_DIR"/

  if [ -d "$BACKUP_DIR/index" ]; then
    mkdir -p "$DATA_DIR/index"
    cp -a "$BACKUP_DIR/index"/. "$DATA_DIR/index"/
  fi

else
  echo "Backup does not look like a KurrentDB backup. chaser.chk not found."
  exit 1
fi

cp -f "$DATA_DIR/chaser.chk" "$DATA_DIR/truncate.chk"

# Important: cp -a may preserve production/root ownership.
# Alpine runs this restore step as root, so fix ownership for the KurrentDB container.
chown -R "$KURRENT_UID:$KURRENT_GID" "$DATA_DIR"
chmod -R u+rwX "$DATA_DIR"

echo "KurrentDB restore completed."
