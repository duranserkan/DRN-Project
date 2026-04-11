export const DEFAULT_VERSIONS = {
    dotnet: '10',
    react: '19',
    bootstrap: '5.3',
    tailwind: '4.2',
    vite: '8'
};

export type AppVersions = Partial<typeof DEFAULT_VERSIONS>;

/**
 * Props for the HelloReact island component.
 *
 * **Callback Convention** — Components may accept function-typed props for
 * island→host event notification. Convention rules:
 * - Name callbacks with the `on` prefix: `onReady`, `onSelectionChange`, `onSubmit`
 * - Callbacks execute in host page JavaScript scope (outside Shadow DOM)
 * - Replaceable via `island.update({ onXxx: newHandler })`
 * - Removable via `island.update({ onXxx: undefined })`
 * - Included in `island.getProps()` return (no filtering)
 * - Callbacks must pass plain data only — no React internals
 */
export interface HelloReactProps {
    title: string;
    versions?: AppVersions;
}

export const HelloReactComponent = ({ title, versions }: HelloReactProps) => {
    const v = { ...DEFAULT_VERSIONS, ...versions };
    
    return (
        <div className="hello-react">
            <div className="hello-react-hero">
                <span className="hello-react-badge">{title}</span>
                <h2 className="hello-react-title">Build Better, Ship Faster</h2>
                <ul className="hello-react-stack">
                    <li><strong>.NET {v.dotnet}</strong> and <strong>Razor Pages</strong> for server-rendered content</li>
                    <li><strong>React {v.react}</strong> micro-frontends mounted as isolated islands</li>
                    <li><strong>Bootstrap {v.bootstrap}</strong> on the host page, <strong>Tailwind CSS {v.tailwind}</strong> inside Shadow DOM</li>
                    <li><strong>Vite {v.vite}</strong> for fast and effective builds</li>
                </ul>
            </div>

            <div className="hello-react-grid">
                <div className="hello-react-card">
                    <div className="hello-react-card-icon">⚡</div>
                    <h3 className="hello-react-card-title">Blazing Performance</h3>
                    <p className="hello-react-card-text">
                        Server-rendered Razor Pages with targeted React {v.react} islands for interactive components. No SPA overhead.
                    </p>
                </div>

                <div className="hello-react-card">
                    <div className="hello-react-card-icon">🎨</div>
                    <h3 className="hello-react-card-title">Dual Styling</h3>
                    <p className="hello-react-card-text">
                        Bootstrap {v.bootstrap} drives the host layout while Tailwind CSS {v.tailwind} powers React islands, scoped via Shadow DOM
                        with zero cascade leaks or class collisions.
                    </p>
                </div>

                <div className="hello-react-card">
                    <div className="hello-react-card-icon">🔒</div>
                    <h3 className="hello-react-card-title">Secure by Default</h3>
                    <p className="hello-react-card-text">
                        CSP nonces, CSRF protection, and anti-forgery tokens built into every request automatically.
                    </p>
                </div>

                <div className="hello-react-card">
                    <div className="hello-react-card-icon">🧩</div>
                    <h3 className="hello-react-card-title">Micro-Frontends</h3>
                    <p className="hello-react-card-text">
                        React {v.react} components mount inside Shadow DOM boundaries with adopted stylesheets, fully isolated
                        from the Bootstrap {v.bootstrap} host page.
                    </p>
                </div>
            </div>

            <div className="hello-react-cta">
                <p className="hello-react-cta-text">
                    This component is styled with Tailwind CSS {v.tailwind} inside a Shadow DOM.
                    The page around it uses Bootstrap {v.bootstrap}.
                </p>
                <p className="hello-react-cta-text">
                    .NET {v.dotnet} · React {v.react} · Vite {v.vite} · Bootstrap {v.bootstrap} · Tailwind CSS {v.tailwind}
                </p>
            </div>
        </div>
    );
};