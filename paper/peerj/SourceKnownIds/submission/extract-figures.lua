local fig_count = 0

function CodeBlock(el)
    fig_count = fig_count + 1
    local tex_filename   = "figures/Figure-" .. fig_count .. ".tex"
    local pdf_filename   = "figures/Figure-" .. fig_count .. ".pdf"
    local png_filename   = "figures/Figure-" .. fig_count .. ".png"
    
    -- 1. TeX output
    local f_tex = io.open(tex_filename, "w")
    f_tex:write([=[
\documentclass[fleqn,10pt,lineno]{wlpeerj}
\usepackage{fontspec}
\setmainfont{Times New Roman}
\setsansfont{Helvetica}
\setmonofont{Menlo}[Scale=0.85]
\raggedright
\pagestyle{empty}
\begin{document}
\begin{verbatim}
]=])
    f_tex:write(el.text)
    f_tex:write([=[

\end{verbatim}
\end{document}
]=])
    f_tex:close()
    
    -- 4. PDF compilation via lualatex (allows UTF8 without errors)
    os.execute("export TEXMFVAR=/tmp/texmfvar && lualatex -interaction=batchmode -output-directory=figures " .. tex_filename)
    
    -- 4. PNG extraction via sips (native vector scaling) to avoid ghostscript, followed by magick trim
    os.execute("sips -Z 6000 -s format png " .. pdf_filename .. " --out " .. png_filename .. " > /dev/null")
    os.execute("magick " .. png_filename .. " -trim +repage -resize 3000x3000 " .. png_filename)
    
    -- 3. Cleanup intermediary files
    os.remove(pdf_filename)
    os.remove("figures/Figure-" .. fig_count .. ".log")
    os.remove("figures/Figure-" .. fig_count .. ".aux")
    
    return pandoc.Para({ pandoc.Strong(pandoc.Str("[Figure " .. fig_count .. " should be inserted here]")) })
end
