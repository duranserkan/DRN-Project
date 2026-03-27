# Paper Pipeline Changes Summary

## Goal

Consolidate the paper build pipeline so `paper-peerj.md` → `wlpeerj.cls`-formatted output (PDF and LaTeX) via a single custom Pandoc template, eliminating redundancies.

---

## Files Created

### `template-peerj.tex` (NEW)

Custom Pandoc template that maps YAML frontmatter to `wlpeerj.cls` macros. Consolidates:

- **`preamble.tex`** (deleted) — longtable font reduction, widow/orphan penalties, `\raggedright`
- **`sample.tex`** (deleted) — `\documentclass{wlpeerj}`, author/affiliation macros, abstract placement, `\maketitle`
- **Pandoc support** — CSL citations, longtable, image scaling, tight lists, code highlighting, hyperref
- **fontspec** — Unicode font support (Times New Roman, Helvetica, Menlo) via XeLaTeX for Turkish characters and box-drawing glyphs
- **natbib compatibility** — `\PassOptionsToPackage{numbers}{natbib}` prevents conflict with `--citeproc`
- **Author font override** — replaces wlpeerj.cls's hardcoded `\usefont{OT1}{phv}` with fontspec's Helvetica for full Unicode support

### `build-commands.md` (UPDATED)

Consolidated from `PaperLatexCommand` + `PaperPdfCommand` (both deleted). Now includes:

- Prerequisites (Pandoc, BasicTeX, TeX packages)
- LaTeX generation command
- PDF generation command
- File inventory table

---

## Files Modified

### `paper-peerj.md`

- Moved abstract from body `# Abstract` section → YAML `abstract: |` block scalar (required by wlpeerj.cls's `\maketitle` colored abstract box)
- Escaped `C#` as `C\#` in YAML abstract
- Updated pipeline comment to reference `template-peerj.tex`
- Minor ASCII art alignment fixes (SequenceId, MAC field labels)

---

## Files Deleted

| File | Reason |
|------|--------|
| `preamble.tex` | Absorbed into `template-peerj.tex` |
| `sample.tex` | Replaced by `template-peerj.tex` |
| `PaperLatexCommand` | Consolidated into `build-commands.md` |
| `PaperPdfCommand` | Consolidated into `build-commands.md` |

---

## Key Design Decisions

1. **Keep `peerj.csl`** — handles citation formatting (CSL), not redundant with `wlpeerj.cls` (document layout)
2. **`--citeproc` over `--natbib`** — single-pass compilation, Pandoc handles everything. wlpeerj.cls's natbib forced to numerical mode to prevent conflict
3. **Cover page content in YAML, layout in template** — `paper-peerj.md` YAML has title/author/abstract; `template-peerj.tex` maps to wlpeerj.cls's `\maketitle`
4. **fontspec for Unicode** — XeLaTeX system fonts replace legacy OT1-encoded TeX fonts for Turkish character support
5. **`collection-fontsrecommended`** — installed once to avoid chasing individual missing font packages

---

## Prerequisites (for clean setup)

```bash
brew install pandoc
brew install --cask basictex
sudo tlmgr update --self
sudo tlmgr install collection-fontsrecommended preprint titlesec lastpage enumitem lipsum
```

---

## Build Commands

```bash
# LaTeX output (for Overleaf)
pandoc ./paper-peerj.md --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl -t latex -o manuscript.tex

# PDF output (local preview)
TMPDIR=/tmp pandoc ./paper-peerj.md --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl --pdf-engine=/Library/TeX/texbin/xelatex -o ./paper-peerj.pdf
```

---

## Final File Inventory

```
paper/peerj/SourceKnownIds/
├── build-commands.md       ← Build instructions + prerequisites
├── paper-peerj.md          ← Source of truth (content + metadata)
├── paper-peerj.pdf         ← Generated PDF
├── paper.bib               ← Bibliography database
├── peerj.csl               ← Citation style for --citeproc
├── summary.md              ← This file
├── supplementary/          ← Benchmark data
├── template-peerj.tex      ← Custom Pandoc template (wlpeerj.cls)
└── wlpeerj.cls             ← PeerJ document class
```
