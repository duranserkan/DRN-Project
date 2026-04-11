import React from 'react';

export interface HelloReactProps {
    title: string;
    appName?: string;
}

export const HelloReactComponent = ({ title, appName }: HelloReactProps) => (
    <div className="hello-react">
        <div className="hello-react-hero">
            <span className="hello-react-badge">{title}</span>
            <h2 className="hello-react-title">Build Better, Ship Faster</h2>
            <ul className="hello-react-stack">
                <li><strong>.NET 10</strong> and <strong>Razor Pages</strong> for server-rendered content</li>
                <li><strong>React 19</strong> micro-frontends mounted as isolated islands</li>
                <li><strong>Bootstrap 5.3</strong> on the host page, <strong>Tailwind CSS 4.2</strong> inside Shadow DOM</li>
                <li><strong>Vite 8</strong> for fast and effective builds</li>
            </ul>
        </div>

        <div className="hello-react-grid">
            <div className="hello-react-card">
                <div className="hello-react-card-icon">⚡</div>
                <h3 className="hello-react-card-title">Blazing Performance</h3>
                <p className="hello-react-card-text">
                    Server-rendered Razor Pages with targeted React 19 islands for interactive components. No SPA overhead.
                </p>
            </div>

            <div className="hello-react-card">
                <div className="hello-react-card-icon">🎨</div>
                <h3 className="hello-react-card-title">Dual Styling</h3>
                <p className="hello-react-card-text">
                    Bootstrap 5.3 drives the host layout while Tailwind CSS 4.2 powers React islands, scoped via Shadow DOM
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
                    React 19 components mount inside Shadow DOM boundaries with adopted stylesheets, fully isolated
                    from the Bootstrap 5.3 host page.
                </p>
            </div>
        </div>

        <div className="hello-react-cta">
            <p className="hello-react-cta-text">
                This component is styled with Tailwind CSS 4.2 inside a Shadow DOM.
                The page around it uses Bootstrap 5.3.
            </p>
            <p className="hello-react-cta-text">
                .NET 10 · React 19 · Vite 8 · Bootstrap 5.3 · Tailwind CSS 4.2
            </p>
        </div>
    </div>
);