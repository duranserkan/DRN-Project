local fig_count = 0

function CodeBlock(el)
    fig_count = fig_count + 1
    local ascii_filename = "figures/Figure-" .. fig_count .. ".ascii.txt"
    local svg_filename   = "figures/Figure-" .. fig_count .. ".svg"
    local tex_filename   = "figures/Figure-" .. fig_count .. ".tex"
    local pdf_filename   = "figures/Figure-" .. fig_count .. ".pdf"
    local png_filename   = "figures/Figure-" .. fig_count .. ".png"
    
    -- 1. ASCII output
    local f_ascii = io.open(ascii_filename, "w")
    f_ascii:write(el.text)
    f_ascii:close()
    
    -- 2. SVG output (fallback text render to satisfy rule 2)
    local f_svg = io.open(svg_filename, "w")
    f_svg:write('<?xml version="1.0" encoding="UTF-8"?>\n')
    f_svg:write('<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="800">\n')
    f_svg:write('  <text x="10" y="20" font-family="monospace" xml:space="preserve">\n')
    local safe_text = el.text:gsub("<", "&lt;"):gsub(">", "&gt;"):gsub("&", "&amp;")
    f_svg:write(safe_text)
    f_svg:write('\n  </text>\n</svg>\n')
    f_svg:close()
    
    -- 3. TeX output
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
    
    -- 5. PNG extraction via sips and magick trim
    os.execute("sips -Z 3000 -s format png " .. pdf_filename .. " --out " .. png_filename .. " > /dev/null")
    os.execute("magick " .. png_filename .. " -trim +repage " .. png_filename)
    
    return pandoc.Para({ pandoc.Strong(pandoc.Str("[Figure " .. fig_count .. " should be inserted here]")) })
end
