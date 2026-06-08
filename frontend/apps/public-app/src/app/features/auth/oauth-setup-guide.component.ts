import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import type { OAuthSetupGuide } from './oauth-setup.models';

@Component({
  selector: 'SearchConsoleApp-oauth-setup-guide',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (guide(); as g) {
      <aside class="oauth-guide" role="alert">
        <header class="oauth-guide-head">
          <h3>{{ g.title }}</h3>
          <p class="oauth-summary">{{ g.summary }}</p>
        </header>

        @if (g.redirectUri) {
          <div class="oauth-block">
            <h4>Redirect URI</h4>
            <code class="oauth-uri">{{ g.redirectUri }}</code>
            <p class="oauth-note">Google Cloud Console → Credentials → OAuth client → Authorized redirect URIs listesine ekleyin.</p>
          </div>
        }

        <div class="oauth-block">
          <h4>Kurulum adımları</h4>
          <ol class="oauth-steps">
            @for (step of g.steps; track step.order) {
              <li>
                <strong>{{ step.title }}</strong>
                <span class="step-detail">{{ step.detail }}</span>
              </li>
            }
          </ol>
        </div>

        @if (g.configKeys.length) {
          <div class="oauth-block">
            <h4>Yapılandırma anahtarları</h4>
            <table class="oauth-config-table">
              <thead>
                <tr>
                  <th>Durum</th>
                  <th>appsettings</th>
                  <th>.env</th>
                  <th>Açıklama</th>
                </tr>
              </thead>
              <tbody>
                @for (key of g.configKeys; track key.appsettingsKey) {
                  <tr [class.missing]="!key.configured">
                    <td>
                      <span class="status-pill" [class.ok]="key.configured" [class.missing]="!key.configured">
                        {{ key.configured ? 'Tamam' : 'Eksik' }}
                      </span>
                    </td>
                    <td><code>{{ key.appsettingsKey }}</code></td>
                    <td><code>{{ key.envVariable }}</code></td>
                    <td>{{ key.description }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        @if (g.envFileHint) {
          <p class="oauth-env-hint">{{ g.envFileHint }}</p>
        }

        @if (g.links.length) {
          <div class="oauth-links">
            <h4>Faydalı bağlantılar</h4>
            <ul>
              @for (link of g.links; track link.url) {
                <li>
                  <a [href]="link.url" target="_blank" rel="noopener noreferrer">{{ link.label }} →</a>
                </li>
              }
            </ul>
          </div>
        }
      </aside>
    }
  `,
  styles: [`
    .oauth-guide {
      margin-top: 1rem;
      padding: 1rem 1.1rem;
      border: 1px solid #fcd34d;
      border-radius: 10px;
      background: linear-gradient(180deg, #fffbeb 0%, #fff 100%);
      text-align: left;
    }
    .oauth-guide-head h3 {
      margin: 0 0 0.35rem;
      font-size: 1rem;
      color: #92400e;
    }
    .oauth-summary {
      margin: 0;
      font-size: 0.88rem;
      color: #78350f;
      line-height: 1.45;
    }
    .oauth-block {
      margin-top: 1rem;
    }
    .oauth-block h4 {
      margin: 0 0 0.45rem;
      font-size: 0.82rem;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      color: #64748b;
    }
    .oauth-uri {
      display: block;
      padding: 0.45rem 0.55rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      font-size: 0.78rem;
      word-break: break-all;
    }
    .oauth-note, .oauth-env-hint {
      margin: 0.4rem 0 0;
      font-size: 0.8rem;
      color: #64748b;
      line-height: 1.4;
    }
    .oauth-steps {
      margin: 0;
      padding-left: 1.2rem;
      font-size: 0.84rem;
      color: #334155;
    }
    .oauth-steps li {
      margin-bottom: 0.65rem;
    }
    .oauth-steps li strong {
      display: block;
      color: #1e293b;
    }
    .step-detail {
      display: block;
      margin-top: 0.15rem;
      color: #64748b;
      white-space: pre-line;
      line-height: 1.4;
    }
    .oauth-config-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.78rem;
      background: #fff;
      border: 1px solid #e8ecf0;
      border-radius: 6px;
      overflow: hidden;
    }
    .oauth-config-table th, .oauth-config-table td {
      padding: 0.4rem 0.5rem;
      border-bottom: 1px solid #f1f5f9;
      text-align: left;
      vertical-align: top;
    }
    .oauth-config-table th { background: #f8fafc; color: #475569; }
    .oauth-config-table tr:last-child td { border-bottom: none; }
    .oauth-config-table tr.missing { background: #fef2f2; }
    .oauth-config-table code {
      font-size: 0.72rem;
      background: #f1f5f9;
      padding: 0.1rem 0.25rem;
      border-radius: 3px;
    }
    .status-pill {
      display: inline-block;
      padding: 0.1rem 0.35rem;
      border-radius: 999px;
      font-size: 0.68rem;
      font-weight: 600;
    }
    .status-pill.ok { background: #dcfce7; color: #166534; }
    .status-pill.missing { background: #fee2e2; color: #991b1b; }
    .oauth-links ul {
      margin: 0;
      padding-left: 1.1rem;
      font-size: 0.84rem;
    }
    .oauth-links a {
      color: #2563eb;
      text-decoration: none;
    }
    .oauth-links a:hover { text-decoration: underline; }
  `],
})
export class OAuthSetupGuideComponent {
  guide = input.required<OAuthSetupGuide>();
}
