#!/bin/bash

# Opens vim with both the XML doc comment intended for editing and the
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

# Open vim with source file and doc comment temp file.
vim -o "$comment_temp_file" "$source_file" \
  -c 'source ./helpers.vimrc' \
  -c 'exe "norm \<C-w>j'$element_line_number'Gzt\<C-w>w"' \
  -c 'setl noai nocin nosi inde=' \
  </dev/tty >/dev/tty

# Send edited doc comment to STDOUT.
cat "$comment_temp_file"
rm "$comment_temp_file"
