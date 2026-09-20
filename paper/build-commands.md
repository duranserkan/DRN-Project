# Paper Build Commands

Both commands use `template-peerj.tex` (custom Pandoc template with `wlpeerj.cls`) and `peerj.csl` (citation formatting via `--citeproc`).

## Prerequisites

**Required Toolchain** (Pandoc for document conversions, ImageMagick and Diagram engines for extraction, and BasicTeX for PDF rendering):

```bash
brew install pandoc imagemagick ditaa plantuml basictex
```

*(Note: Depending on your Homebrew setup, `basictex` may alternatively require `brew install --cask basictex`)*

**TeX packages** required by `wlpeerj.cls` (not included in BasicTeX):

```bash
sudo tlmgr update --self
sudo tlmgr install collection-fontsrecommended dejavu preprint titlesec lastpage enumitem lipsum
```

> `collection-fontsrecommended` provides TeX Gyre Termes/Heros (Times/Helvetica metric-compatible clones) and math fonts (~200MB). `dejavu` provides DejaVu Sans Mono (monospace with full Unicode coverage). `preprint` provides `authblk.sty`; `titlesec` includes `titletoc.sty`.
>
> **Note**: If you already have the full MacTeX distribution installed (`mactex` instead of `basictex`), you can skip this `tlmgr` section entirely as all packages are pre-installed.

## Generate LaTeX

Converts the Markdown paper to a standalone `.tex` file. The output uses `wlpeerj.cls` and can be compiled directly with XeLaTeX or uploaded to Overleaf.

```bash
pandoc ./paper-peerj.md --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl -t latex -o manuscript.tex
```

## Generate PDF (direct)

Converts the Markdown paper directly to PDF using XeLaTeX with the PeerJ layout (wlpeerj.cls title page, colored abstract box, line numbers, 5cm left margin).

```bash
TMPDIR=/tmp pandoc ./paper-peerj.md --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl --pdf-engine=/Library/TeX/texbin/xelatex -o ./paper-peerj.pdf
```

## Generate PDF (from LaTeX)

Compiles the previously generated `manuscript.tex` to PDF. Produces the same output as the direct path since both use identical TeX Gyre / DejaVu fonts resolved by filename.

```bash
/Library/TeX/texbin/xelatex ./manuscript.tex && /Library/TeX/texbin/xelatex ./manuscript.tex
```

> XeLaTeX is run twice to resolve cross-references (page numbers, table of contents). Both paths produce identical PDFs because the template specifies fonts by TeX filename (e.g., `texgyretermes-regular.otf`) rather than system font name, eliminating platform-dependent font resolution.

## arXiv Submission

arXiv rejects PDFs with line numbers. The `submission/template-arxiv.tex` template removes the `lineno` option and uses TeX filename fonts (arXiv has no macOS system fonts). All commands below run from the `submission/` directory.

### Generate arXiv LaTeX

```bash
pandoc ./paper-peerj.md --template=template-arxiv.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl -t latex -o manuscript-arxiv.tex
```

### Generate arXiv PDF (direct)

```bash
TMPDIR=/tmp pandoc ./paper-peerj.md --template=template-arxiv.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl --pdf-engine=/Library/TeX/texbin/xelatex -o ./manuscript-arxiv.pdf
```

### Generate arXiv PDF (from LaTeX)

```bash
/Library/TeX/texbin/xelatex ./manuscript-arxiv.tex && /Library/TeX/texbin/xelatex ./manuscript-arxiv.tex
```

## File Inventory

| File | Purpose |
|------|---------|
| `paper-peerj.md` | Single source of truth (content + metadata in YAML frontmatter) |
| `template-peerj.tex` | Custom Pandoc template — maps YAML to `wlpeerj.cls` macros (with line numbers) |
| `template-arxiv.tex` | arXiv Pandoc template — no line numbers, TeX filename fonts |
| `wlpeerj.cls` | PeerJ document class (page layout, fonts, title page) |
| `peerj.csl` | Citation Style Language — formats `[@ref]` citations |
| `paper.bib` | BibTeX bibliography database |
| `build-commands.md` | This file |
