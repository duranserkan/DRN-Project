# Paper Build Commands

The PeerJ commands use `template-peerj.tex`; the arXiv commands use `template-arxiv.tex`. Both templates use `wlpeerj.cls` and `peerj.csl` (citation formatting via `--citeproc`).

The Elsevier commands use `template-elsevier.tex` with the supplied CAS single-column class and its BibTeX style. See [Elsevier Submission](#elsevier-submission) for its separate prerequisites and compilation sequence.

All conversion commands use `--number-sections`; the templates enable numbering through subsubsections. Keep heading text in `paper.md` free of manual numbers and mark closing sections with `{.unnumbered}`. Section numbers appear in generated manuscripts, not in the Markdown source or GitHub preview.

- [Prerequisites](#prerequisites)
- [Generate LaTeX](#generate-latex)
- [Generate PDF (direct)](#generate-pdf-direct)
- [Generate PDF (from LaTeX)](#generate-pdf-from-latex)
- [arXiv Submission](#arxiv-submission)
- [Elsevier Submission](#elsevier-submission)
- [Generated files and cleanup](#generated-files-and-cleanup)
- [File Inventory](#file-inventory)

Run all generation commands from `paper/SourceKnownIds/`. From the repository root:

```bash
cd paper/SourceKnownIds
```

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
pandoc ./paper.md --number-sections --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl -t latex -o manuscript.tex
```

## Generate PDF (direct)

Converts the Markdown paper directly to PDF using XeLaTeX with the PeerJ layout (wlpeerj.cls title page, colored abstract box, line numbers, 5cm left margin).

```bash
TMPDIR=/tmp pandoc ./paper.md --number-sections --template=template-peerj.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl --pdf-engine=/Library/TeX/texbin/xelatex -o ./paper-peerj.pdf
```

## Generate PDF (from LaTeX)

Compiles the previously generated `manuscript.tex` to PDF. Produces the same output as the direct path since both use identical TeX Gyre / DejaVu fonts resolved by filename.

```bash
/Library/TeX/texbin/xelatex ./manuscript.tex && /Library/TeX/texbin/xelatex ./manuscript.tex
```

> XeLaTeX is run twice to resolve cross-references (page numbers, table of contents). Both paths produce identical PDFs because the template specifies fonts by TeX filename (e.g., `texgyretermes-regular.otf`) rather than system font name, eliminating platform-dependent font resolution.

## arXiv Submission

The `template-arxiv.tex` template removes the `lineno` option and uses TeX filename fonts. Run these commands from the same `paper/SourceKnownIds/` directory.

### Generate arXiv LaTeX

```bash
pandoc ./paper.md --number-sections --template=template-arxiv.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl -t latex -o manuscript-arxiv.tex
```

### Generate arXiv PDF (direct)

```bash
TMPDIR=/tmp pandoc ./paper.md --number-sections --template=template-arxiv.tex --citeproc --bibliography=./paper.bib --csl=./peerj.csl --pdf-engine=/Library/TeX/texbin/xelatex -o ./manuscript-arxiv.pdf
```

### Generate arXiv PDF (from LaTeX)

```bash
/Library/TeX/texbin/xelatex ./manuscript-arxiv.tex && /Library/TeX/texbin/xelatex ./manuscript-arxiv.tex
```

## Elsevier Submission

`template-elsevier.tex` adapts the existing manuscript metadata and body to `cas-sc.cls` from [`../els-cas-templates/`](els-cas-templates/README). The vendor `cas-sc-template.tex` is a sample LaTeX document, not a Pandoc template. The adapter uses the single-column layout because the manuscript's Pandoc tables use `longtable`; changing only the class to `cas-dc` does not provide a compatible two-column build.

These commands use natbib and the bundled author-year style `cas-model2-names.bst`. They do not use `--citeproc` or `peerj.csl`; the `csl` metadata in `paper.md` does not control this path. Check the target journal's Guide for Authors for its required layout and reference style. CAS and `elsarticle` are separate template families; this path uses the supplied CAS bundle. See [Elsevier's LaTeX instructions](https://www.elsevier.com/researcher/author/policies-and-guidelines/latex-instructions).

### Elsevier prerequisites and submission folder

Use the shared Pandoc/XeLaTeX toolchain and TeX Gyre/DejaVu fonts above. For BasicTeX, update the TeX Live package manager first, then install the additional packages used by the CAS class and Pandoc adapter:

```bash
sudo tlmgr update --self &&
sudo tlmgr install natbib etoolbox booktabs makecell multirow colortbl tools sttools xstring footmisc moreverb wrapfig geometry hyperref xcolor fontspec footnotehyper fancyvrb framed
```

`tools` supplies `calc.sty`, `dcolumn.sty`, `xspace.sty`, `array.sty` and `longtable.sty`; `dcolumn` and `xspace` are not standalone TeX Live package names. The adapter explicitly loads `calc` for Pandoc's table-column width arithmetic. `sttools` supplies `stfloats.sty`. Standard LaTeX packages such as `amsmath`, `amsfonts`, `expl3` and `xparse` must also be available. CAS optionally uses STIX/Charis fonts when installed; the adapter explicitly selects the same Unicode text fonts as the existing templates. A full MacTeX installation normally includes these dependencies.

Run from `paper/SourceKnownIds/`, as above. Prepare or refresh the submission dependencies using the shared bibliography and vendor bundle as the sources of truth:

```bash
mkdir -p ./submission-elsevier/thumbnails &&
cp ./paper.bib ./highlights.txt ../els-cas-templates/cas-sc.cls ../els-cas-templates/cas-common.sty ../els-cas-templates/cas-model2-names.bst ./submission-elsevier/ &&
cp ../els-cas-templates/thumbnails/cas-email.jpeg ./submission-elsevier/thumbnails/
```

The submission folder contains local copies of the class, style, bibliography and email icon required by the current manuscript, plus the separate article highlights. Compilation runs inside that folder, so `TEXINPUTS` and `BSTINPUTS` exports are no longer needed. Keep editing the shared `paper.bib` and `highlights.txt` in `SourceKnownIds/`; the generation commands below refresh their submission copies. Repeat preparation after updating vendor files. If future manuscript changes add figures or other CAS icons, copy those assets while preserving their referenced paths.

### Generate Elsevier LaTeX

```bash
cp ./paper.bib ./highlights.txt ./coverletter.txt ./submission-elsevier/ &&
pandoc ./paper.md --number-sections --template=./template-elsevier.tex --natbib --bibliography=./paper.bib -t latex -o ./submission-elsevier/manuscript-elsevier.tex
```

The adapter maps the title, author, affiliation, email, ORCID and abstract to CAS frontmatter. Pandoc's natbib writer removes the trailing Markdown References heading; natbib supplies the heading for the generated bibliography. Keep Acknowledgements directly before References, after the other declarations. Citations with three or more authors use the first author's surname followed by "et al." from their first occurrence.

For web references, include the actual access date in the BibTeX `note` field so the bundled CAS style prints it; an `urldate` field alone is not rendered by this style.

The adapter uses CAS's default title mode. The bundled CAS 2.4 `longmktitle` implementation calls `\vbox_unpack_clear:N`, which is unavailable in the installed TeX Live 2026 LaTeX3 kernel and causes an undefined-control-sequence error at `\maketitle`. Do not enable that option without addressing this compatibility issue. Check title/abstract fit when inspecting the rendered PDF.

### Generate Elsevier PDF (from Markdown, one command chain)

Run from `paper/SourceKnownIds/` after preparing the submission folder above. This generates `submission-elsevier/manuscript-elsevier.tex` and compiles the PDF in the same folder, stopping if any step fails. The parentheses keep the directory change local to this command chain:

```bash
cp ./paper.bib ./highlights.txt ./coverletter.txt ./submission-elsevier/ &&
pandoc ./paper.md --number-sections --template=./template-elsevier.tex --natbib --bibliography=./paper.bib -t latex -o ./submission-elsevier/manuscript-elsevier.tex &&
(
cd ./submission-elsevier &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex &&
/Library/TeX/texbin/bibtex manuscript-elsevier &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex
)
```

The intermediate `.tex` and `.bbl` files remain available for source submission. This preserves the bundled Elsevier bibliography style: [Pandoc's `--natbib` option targets LaTeX processed by BibTeX](https://pandoc.org/MANUAL.html#option--natbib), so this shortcut chains those steps rather than using Pandoc's direct PDF output.

### Generate Elsevier PDF (from LaTeX)

From `paper/SourceKnownIds/`, after preparing the folder and generating the TeX file:

```bash
cp ./paper.bib ./highlights.txt ./coverletter.txt ./submission-elsevier/ &&
(
cd ./submission-elsevier &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex &&
/Library/TeX/texbin/bibtex manuscript-elsevier &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex &&
/Library/TeX/texbin/xelatex -interaction=nonstopmode -halt-on-error ./manuscript-elsevier.tex
)
```

Output: `submission-elsevier/manuscript-elsevier.pdf`. All Elsevier intermediates, including `.aux`, `.abs`, `.bbl`, `.blg`, `.out` and `.log`, are created inside `submission-elsevier/`. The first pass writes citation data; BibTeX generates the `.bbl`; the final two passes resolve citations and cross-references. Regenerate the TeX after editing `paper.md` or the template, then repeat the compilation sequence. For bibliography-only edits, repeat the compilation sequence directly.

### Elsevier source submission

Upload `submission-elsevier/highlights.txt` as the separate editable highlights file.

Use the contents of `submission-elsevier/`: `manuscript-elsevier.tex`, `paper.bib`, `manuscript-elsevier.bbl`, `cas-model2-names.bst`, `cas-sc.cls`, `cas-common.sty`, and `thumbnails/cas-email.jpeg`. The current manuscript has no external figure references. Preserve the thumbnail subdirectory when assembling an Overleaf project or source archive, and exclude disposable intermediates listed below. Select XeLaTeX and ensure the documented fonts/packages are available there. Include any additional source dependencies introduced by later manuscript changes.

Choose the upload route required by the journal and submission stage, following [Elsevier's LaTeX submission instructions](https://www.elsevier.com/researcher/author/policies-and-guidelines/latex-instructions):

**When PDF submission is allowed:** upload the complete manuscript PDF as **Manuscript** and bundle the editable sources and dependencies into an archive classified as **LaTeX Source Files**.

**When PDF submission is restricted:** upload the source files using these item types:

| File type | Submission item type |
|-----------|----------------------|
| `.tex`, `.bbl`, `.bst`, `.sty`, `.bib`, `.cls`, `.nls`, `.ilg`, `.nlo` | **Manuscript** |
| Images and graphic files | **Figure** |
| Separate tables in `.tex` files | **Table** |

For the current PDF-restricted submission, classify `manuscript-elsevier.tex`, `manuscript-elsevier.bbl`, `paper.bib`, `cas-model2-names.bst`, `cas-common.sty` and `cas-sc.cls` as **Manuscript**, and `thumbnails/cas-email.jpeg` as **Figure**. The current manuscript's tables are embedded in the main `.tex`; no separate Table upload is needed.

Do not classify manuscript LaTeX sources as supplementary research material. Item names and subfolder handling may vary by journal; follow its specific instructions and check that uploaded paths still resolve. Review the submission system's generated PDF before approval; these commands do not establish journal-specific compliance.

## Generated files and cleanup

TeX-to-PDF compilation creates intermediate files beside the generated manuscript: in `paper/SourceKnownIds/submission-elsevier/` for Elsevier, and `paper/SourceKnownIds/` for PeerJ/arXiv. After all compilation passes finish successfully, the following generated files can be removed without affecting the completed PDF:

| Extension | Purpose |
|-----------|---------|
| `.aux` | Citation, label and cross-reference data used between passes |
| `.out` | Hyperref bookmark data |
| `.log` | LaTeX diagnostic log; retain while troubleshooting |
| `.blg` | BibTeX diagnostic log; retain while troubleshooting |
| `.abs` | Abstract content written by the CAS class |

For the Elsevier build, these are `manuscript-elsevier.aux`, `manuscript-elsevier.out`, `manuscript-elsevier.log`, `manuscript-elsevier.blg` and `manuscript-elsevier.abs`. The same cleanup rule applies to generated `.aux`, `.out` and `.log` files for the PeerJ and arXiv builds. Remove only files belonging to the build being cleaned, not all files sharing an extension across the repository.

Keep the PDF, generated manuscript `.tex`, and `manuscript-elsevier.bbl` for submission. The `.bbl` is generated by BibTeX and can be recreated, but it contains the formatted references needed by the source submission. Keep `paper.md`, `paper.bib`, all `template-*.tex` files, class/style files, figures and thumbnails; these are build inputs, not disposable intermediates.

Do not clean between compilation passes. After removing intermediate files, rerun the complete documented compilation sequence (including BibTeX for Elsevier) to rebuild cross-references and citations; additional XeLaTeX passes may be needed if the log still requests a rerun.

## File Inventory

| File | Purpose |
|------|---------|
| `paper.md` | Single source of truth (content + metadata in YAML frontmatter) |
| `highlights.txt` | Article highlights; copied to `submission-elsevier/` for separate upload |
| `template-peerj.tex` | Custom Pandoc template — maps YAML to `wlpeerj.cls` macros (with line numbers) |
| `template-arxiv.tex` | arXiv Pandoc template — no line numbers, TeX filename fonts |
| `template-elsevier.tex` | Elsevier Pandoc adapter — CAS single-column frontmatter, Unicode fonts, natbib |
| `submission-elsevier/` | Elsevier submission files and build outputs; local dependency copies eliminate search-path exports |
| `../els-cas-templates/cas-sc.cls` | Elsevier CAS single-column document class |
| `../els-cas-templates/cas-common.sty` | Shared CAS frontmatter and layout support |
| `../els-cas-templates/cas-model2-names.bst` | CAS author-year BibTeX style |
| `../els-cas-templates/thumbnails/` | CAS frontmatter icons, including the email icon |
| `wlpeerj.cls` | PeerJ document class (page layout, fonts, title page) |
| `peerj.csl` | Citation Style Language — formats `[@ref]` citations |
| `paper.bib` | BibTeX bibliography database |
| `../build-commands.md` | This file |
