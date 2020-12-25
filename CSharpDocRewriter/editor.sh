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
# There's a lot going on here...
# Pseudocode:
#   - Import predefined macros etc. from helpers.vimrc.
#   - Disable scrolloff, used in next command to scroll source
#     code buffer to the comment's code element.
#   - Move to the source code buffer, jump to source element,
#     scroll such that code element is first line in buffer,
#     set the current line to the middle of the buffer view,
#     so that enabling scrolloff won't cause an undesired scroll.
#   - Enable scroll off to 999 to make the buffer scroll with
#     any cursor movement (makes it easy for user to navigate
#     the code element quickly).
#   - Move back to the XML comment buffer for editing.
#   - Disable XML auto-indenting.
vim -o "$comment_temp_file" "$source_file" \
  -c 'source ./helpers.vimrc' \
  -c 'set so=0' \
  -c 'exe "norm \<C-w>j'$element_line_number'GztM"' \
  -c 'set so=999' \
  -c 'exe "norm \<C-w>w"' \
  -c 'setl noai nocin nosi inde=' \
  </dev/tty >/dev/tty

# Send edited doc comment to STDOUT.
cat "$comment_temp_file"
rm "$comment_temp_file"
