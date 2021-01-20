# CSharpDocRewriter
A Roslyn-powered tool for rewriting C# XML documentation comments with a Vim front-end.

<p align="center">
  <img src="editing.gif" alt="Editing with CSharpDocRewriter">
</p>

## Details
This tools edits all XML doc comments in the provided source file(s) one-by-one,
optionally opening each for manual editing in Vim, alongside the corresponding source-code element.

Predefined macros make it easy to quickly edit C# XML doc tags.

Save your progress and exit. Pick up where you left off.

### Filter by Git author
Set env `REWRITER_GIT_AUTHOR_NAME` to visit only XML doc comments that have been touched
by a specific author.

This feature works by running Git blame standing in the current C# file's parent
directory for the span of the current comment.

## Usage

```
CSharpDocRewriter.exe [options] [<files>...]

Options:
  --automatic       Don't open Vim. Instead, apply automatic changes, only. [default: False]
  --reorder-tags    Reorder comment XML tags after edits. [default: False]
  -?, -h, --help    Show help and usage information
```

While editing, the following Vim macros are available:

```
    @t  Make the current word into a <paramref> tag.
    @y  Make the current word into a <see> tag.
    @n  Insert a blank <returns> tag at the end of the comment.
    @m  Insert a blank <remarks> tag at the end of the comment.
    @p  Insert a <param> tag for the current word at the end of the comment.
```

Further instruction is given in the tool.

### Save file
By default, a savestate is created in the current working directory.

Set env `REWRITER_SAVE_LOCATION` to a custom Windows file path, if desired.

## Requirements
Currently only runs on Windows with **Ubuntu** Windows Subsystem for Linux (WSL) installed.
Contibutions appreciated!