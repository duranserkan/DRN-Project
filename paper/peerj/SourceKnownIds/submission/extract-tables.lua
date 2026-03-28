local tab_count = 0

function Table(el)
    tab_count = tab_count + 1
    
    local tex_filename = "tables/Table-" .. tab_count .. ".tex"
    local pdf_filename = "tables/Table-" .. tab_count .. ".pdf"
    local png_filename = "tables/Table-" .. tab_count .. ".png"
    
    local latex_markup = pandoc.write(pandoc.Pandoc({el}), 'latex')
    
    local f_tex = io.open(tex_filename, "w")
    f_tex:write([=[
\documentclass[fleqn,10pt,lineno]{wlpeerj}
\usepackage{fontspec}
\setmainfont{Times New Roman}
\setsansfont{Helvetica}
\setmonofont{Menlo}[Scale=0.85]
\usepackage{longtable}
\usepackage{array}
\usepackage{calc}
\usepackage{etoolbox}
\AtBeginEnvironment{longtable}{%
  \scriptsize%
  \setlength{\tabcolsep}{2pt}%
}
\raggedright
\pagestyle{empty}
\begin{document}
]=])
    f_tex:write(latex_markup)
    f_tex:write([=[

\end{document}
]=])
    f_tex:close()
    
    os.execute("export TEXMFVAR=/tmp/texmfvar && lualatex -interaction=batchmode -output-directory=tables " .. tex_filename)
    os.execute("sips -Z 3000 -s format png " .. pdf_filename .. " --out " .. png_filename .. " > /dev/null")
    os.execute("magick " .. png_filename .. " -trim +repage " .. png_filename)
    
    return pandoc.Para({ pandoc.Strong(pandoc.Str("[Table " .. tab_count .. " should be inserted here]")) })
end
