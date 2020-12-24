#!/bin/bash

# Opens $VISUAL with both the XML doc comment intended for editing and the
# original source code (for context).
#
# STDIN is the doc comment for editing.
# $1 is source file containing the doc comment.
# $2 is the line number of the start of the code element associated with the doc comment.
set -u

source_file="$1"
element_line_number="$2"

# Write STDIN (the doc comment) to a temp file.
comment_temp_file="$(mktemp --suffix=.xml)"
cat > "$comment_temp_file"

# Open visual editor with source file and doc comment temp file.
VISUAL=vim
$VISUAL -o "$comment_temp_file" "$source_file" </dev/tty >/dev/tty

# Send edited doc comment to STDOUT.
cat "$comment_temp_file"
rm "$comment_temp_file"
