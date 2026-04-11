export const VERSIONS = {
    dotnet: '10',
    react: '19',
    bootstrap: '5.3',
    tailwind: '4.2',
    vite: '8'
};

export interface HelloReactProps {
    title: string;
    appName?: string;
}

export const HelloReactComponent = ({ title, appName }: HelloReactProps) => (
    <div className="hello-react">
        <div className="hello-react-hero">
            <span className="hello-react-badge">{appName ? `${appName} - ${title}` : title}</span>
            <h2 className="hello-react-title">Build Better, Ship Faster</h2>
            <ul className="hello-react-stack">
                <li><strong>.NET {VERSIONS.dotnet}</strong> and <strong>Razor Pages</strong> for server-rendered content</li>
                <li><strong>React {VERSIONS.react}</strong> micro-frontends mounted as isolated islands</li>
                <li><strong>Bootstrap {VERSIONS.bootstrap}</strong> on the host page, <strong>Tailwind CSS {VERSIONS.tailwind}</strong> inside Shadow DOM</li>
                <li><strong>Vite {VERSIONS.vite}</strong> for fast and effective builds</li>
            </ul>
        </div>

        <div className="hello-react-grid">
            <div className="hello-react-card">
                <div className="hello-react-card-icon">⚡</div>
                <h3 className="hello-react-card-title">Blazing Performance</h3>
                <p className="hello-react-card-text">
                    Server-rendered Razor Pages with targeted React {VERSIONS.react} islands for interactive components. No SPA overhead.
                </p>
            </div>

            <div className="hello-react-card">
                <div className="hello-react-card-icon">🎨</div>
                <h3 className="hello-react-card-title">Dual Styling</h3>
                <p className="hello-react-card-text">
                    Bootstrap {VERSIONS.bootstrap} drives the host layout while Tailwind CSS {VERSIONS.tailwind} powers React islands, scoped via Shadow DOM
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
                    React {VERSIONS.react} components mount inside Shadow DOM boundaries with adopted stylesheets, fully isolated
                    from the Bootstrap {VERSIONS.bootstrap} host page.
                </p>
            </div>
        </div>

        <div className="hello-react-cta">
            <p className="hello-react-cta-text">
                This component is styled with Tailwind CSS {VERSIONS.tailwind} inside a Shadow DOM.
                The page around it uses Bootstrap {VERSIONS.bootstrap}.
            </p>
            <p className="hello-react-cta-text">
                .NET {VERSIONS.dotnet} · React {VERSIONS.react} · Vite {VERSIONS.vite} · Bootstrap {VERSIONS.bootstrap} · Tailwind CSS {VERSIONS.tailwind}
            </p>
        </div>
    </div>
);