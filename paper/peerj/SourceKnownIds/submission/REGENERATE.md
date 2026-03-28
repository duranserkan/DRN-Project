# Manuscript Regeneration Guide

If you update the source `paper-peerj.md` and need to dynamically rebuild the extracted PeerJ manuscript alongside its high-resolution `.png` images, execute the commands detailed in this document.

## 1. Rebuild the Manuscript and Assets
Run the following from the `paper/peerj/SourceKnownIds/submission/` directory to invoke the Pandoc extraction pipeline. This will intercept all tables and figures, create LaTeX formatting fragments matching the `wlpeerj` template, and compile them natively using `lualatex` + macOS `sips` for perfect rasterization:

```bash
# Ensure asset directories exist
mkdir -p figures tables

# Run Pandoc extraction and generation
pandoc ./paper-peerj.md \
    --lua-filter=extract-figures.lua \
    --lua-filter=extract-tables.lua \
    --template=template-peerj.tex \
    --citeproc \
    --bibliography=./paper.bib \
    --csl=./peerj.csl \
    -t latex \
    -o manuscript.tex

# Compile the target manuscript to verify integrity
export TEXMFVAR=/tmp/texmfvar
lualatex -interaction=batchmode manuscript.tex
```

**Note:** Safe-to-delete files like `.log`, `.aux`, `.out`, or intermediate `.ascii.txt`/`.tex`/`.pdf` variants inside the directories do not need to be manually deleted—simply omitting them from your final journal submission package keeps it pristine, while letting you keep them locally to regenerate your outputs infinitely.
