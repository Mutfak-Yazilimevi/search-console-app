import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  DestroyRef,
  OnInit,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ButtonComponent, HintIconComponent } from '@SearchConsoleApp/shared/ui';
import { AuthService, NotificationService } from '@SearchConsoleApp/shared/core';
import { AuditApiService, AuditDashboardSiteDto } from '../audit/audit-api.service';
import { SearchConsoleApiService } from '../audit/search-console-api.service';
import { AuditDetailDto, AuditIssueDto, BacklinkDto, ContentQualityDto, IndexStatusDto, KeywordDto, KeywordSerpDto, KeywordWatchDto, PageSpeedDto, PerformanceDto, SearchConsoleCoverageDto } from '../audit/audit.models';
import { getRulePlaybook, getRulePlaybookOrFallback, RulePlaybookEntry } from '../audit/rule-playbook';
import { categoryLabel, severityLabel, auditStatusLabel } from '../audit/audit-labels';
import {
  computeSeoScoreBreakdown,
  filterIssuesBySeverity,
  groupIssues,
  sortIssuesBySeverity,
  type IssueGroup,
  type IssueGroupMode,
} from '../audit/audit-issue.utils';
import { AuditPollService } from '../audit/audit-poll.service';
import { AUDIT_HINTS } from '../audit/audit-metric-hints';
import { AuditAiActionsComponent, AuditAiAction, MetaAiTarget } from '../audit/audit-ai-actions.component';
import { IssueH1TableComponent } from '../audit/issue-h1-table.component';
import { IssueImgAltTableComponent } from '../audit/issue-img-alt-table.component';
import { IssueDetailTableComponent } from '../audit/issue-detail-table.component';
import { detectStructuredEvidenceKind } from '../audit/audit-evidence.utils';
import { OAuthSetupGuideComponent } from '../auth/oauth-setup-guide.component';
import { OAuthSetupGuide, oauthErrorMessage, parseOAuthSetupError } from '../auth/oauth-setup.models';
import { IntegrationStatusPanelComponent } from '../audit/integration-status-panel.component';
import { ScannedPagesPanelComponent } from '../audit/scanned-pages-panel.component';
import { CriticalIssuesReportComponent } from '../audit/critical-issues-report.component';
import { IntegrationItemDto, ScheduledAuditDto } from '../audit/audit-api.service';
import { SearchConsolePropertyDto } from '../audit/search-console-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonComponent, HintIconComponent, AuditAiActionsComponent, IssueH1TableComponent, IssueImgAltTableComponent, IssueDetailTableComponent, OAuthSetupGuideComponent, IntegrationStatusPanelComponent, ScannedPagesPanelComponent, CriticalIssuesReportComponent, DecimalPipe, RouterLink],
  template: `
    <section class="audit-hero surface">
      <p class="hero-nav"><a routerLink="/merchant-center">Ürün Uyumluluğu (Merchant Center) →</a></p>
      <h1>SEO Site Denetimi</h1>
      <p class="subtitle">
        Web sitenizin adresini girin; Google Search Central kurallarına göre tarayıp hataları raporlayalım.
      </p>

      <aside class="sc-banner">
        @if (scConnected()) {
          <span class="sc-ok">Search Console bağlı</span>
          @if (useConnectedAudit()) {
            <label class="sc-opt">
              <input type="checkbox" [(ngModel)]="connectedMode" />
              Bağlı mod (SC + index verisi)
              <SearchConsoleApp-hint [text]="hints.connectedMode" />
            </label>
          }
        } @else if (auth.isAuthenticated()) {
          @if (scProperties().length > 1) {
            <label class="sc-opt sc-property-select">
              SC property:
              <select [(ngModel)]="selectedScProperty" name="scProperty">
                @for (p of scProperties(); track p.siteUrl) {
                  <option [ngValue]="p.siteUrl">{{ p.siteUrl }}</option>
                }
              </select>
            </label>
          }
          <SearchConsoleApp-button variant="secondary" (click)="connectSearchConsole()">
            Google Search Console Bağla
          </SearchConsoleApp-button>
        } @else {
          <SearchConsoleApp-button variant="secondary" (click)="loginWithGoogle()">
            Google ile giriş yap → Search Console raporu
          </SearchConsoleApp-button>
        }
      </aside>

      @if (auth.isAuthenticated() && scConnected()) {
        <section class="dashboard-panel">
          <h3>Site paneli</h3>
          @if (dashboardLoading()) {
            <p class="panel-note">Yükleniyor…</p>
          } @else if (dashboardSites().length === 0) {
            <p class="panel-note">Henüz tamamlanmış bağlı tarama yok. Aşağıdan zamanlanmış denetim ekleyebilirsiniz.</p>
          } @else {
            <table class="perf-table">
              <thead>
                <tr>
                  <th>Site</th>
                  <th>Skor <SearchConsoleApp-hint [text]="hints.dashboardScore" /></th>
                  <th>Kritik <SearchConsoleApp-hint [text]="hints.dashboardCritical" /></th>
                  <th>Uyarı <SearchConsoleApp-hint [text]="hints.dashboardWarning" /></th>
                  <th>Δ <SearchConsoleApp-hint [text]="hints.dashboardDelta" /></th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (site of dashboardSites(); track site.normalizedUrl) {
                  <tr>
                    <td>
                      <strong>{{ site.label || site.normalizedUrl }}</strong>
                      @if (site.scheduleEnabled) { <span class="mode-badge">Zamanlanmış</span> }
                    </td>
                    <td>{{ site.latestScore ?? '—' }}</td>
                    <td>{{ site.latestCriticalCount }}</td>
                    <td>{{ site.latestWarningCount }}</td>
                    <td>
                      @if (site.scoreDelta != null) {
                        <span [class.low-score]="site.scoreDelta < 0">{{ site.scoreDelta > 0 ? '+' : '' }}{{ site.scoreDelta }}</span>
                      } @else { — }
                    </td>
                    <td>
                      @if (site.lastAuditEntityId) {
                        <button
                          type="button"
                          class="link-btn"
                          [class.link-btn-disabled]="hasActiveScan() && activeScanEntityId() !== site.lastAuditEntityId"
                          [disabled]="hasActiveScan() && activeScanEntityId() !== site.lastAuditEntityId"
                          (click)="openAudit(site.lastAuditEntityId!)">Görüntüle</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }

          <details class="schedule-form">
            <summary>Anahtar kelime takibi ekle</summary>
            <form (submit)="onKeywordWatchSubmit($event)" class="audit-form schedule-inner">
              <input type="text" [(ngModel)]="watchSiteUrl" name="watchSiteUrl" placeholder="Site URL" required />
              <input type="text" [(ngModel)]="watchKeyword" name="watchKeyword" placeholder="Anahtar kelime" required />
              <SearchConsoleApp-button variant="secondary" type="submit" [disabled]="watchSaving()">Takibe al</SearchConsoleApp-button>
            </form>
            @if (keywordWatches().length > 0) {
              <ul class="issue-list compact-watches">
                @for (w of keywordWatches(); track w.entityId) {
                  <li>
                    <span>{{ w.siteHost }} — <strong>{{ w.keyword }}</strong></span>
                    <button type="button" class="link-btn" (click)="removeKeywordWatch(w.entityId)">Kaldır</button>
                  </li>
                }
              </ul>
            }
          </details>

          <details class="schedule-form">
            <summary>Zamanlanmış denetimler</summary>
            @if (schedules().length > 0) {
              <ul class="schedule-list">
                @for (sch of schedules(); track sch.entityId) {
                  <li>
                    <strong>{{ sch.label || sch.url }}</strong>
                    <span class="sch-meta">{{ sch.intervalDays }} gün · {{ sch.isEnabled ? 'Aktif' : 'Pasif' }}</span>
                    @if (sch.notifyOnCriticalOnly) {
                      <span class="mode-badge">Kritik bildirim</span>
                    }
                    <button type="button" class="link-btn" (click)="toggleSchedule(sch)">
                      {{ sch.isEnabled ? 'Durdur' : 'Etkinleştir' }}
                    </button>
                    <button type="button" class="link-btn" (click)="deleteSchedule(sch.entityId)">Sil</button>
                  </li>
                }
              </ul>
            } @else {
              <p class="panel-note">Henüz zamanlanmış denetim yok.</p>
            }
            <h4 class="guide-title">Yeni zamanlama</h4>
            <form (submit)="onScheduleSubmit($event)" class="audit-form schedule-inner">
              <input type="text" [(ngModel)]="scheduleUrl" name="scheduleUrl" placeholder="Site URL" required />
              <input type="text" [(ngModel)]="scheduleLabel" name="scheduleLabel" placeholder="Etiket (isteğe bağlı)" />
              <input type="text" [(ngModel)]="scheduleMigrationUrl" name="scheduleMigrationUrl" placeholder="Eski domain (taşıma)" />
              <input type="text" [(ngModel)]="scheduleGa4Id" name="scheduleGa4Id" placeholder="GA4 property ID" />
              <input type="text" [(ngModel)]="scheduleWebhookUrl" name="scheduleWebhookUrl" placeholder="Webhook URL (isteğe bağlı)" />
              <label class="sc-opt"><input type="checkbox" [(ngModel)]="scheduleNotifyEmail" name="scheduleNotifyEmail" /> E-posta bildirimi</label>
              <label class="sc-opt"><input type="checkbox" [(ngModel)]="scheduleNotifyCriticalOnly" name="scheduleNotifyCriticalOnly" /> Yalnızca kritik sorunlarda bildir</label>
              <select [(ngModel)]="scheduleIntervalDays" name="scheduleIntervalDays">
                <option [ngValue]="7">Haftalık</option>
                <option [ngValue]="14">2 haftada bir</option>
                <option [ngValue]="30">Aylık</option>
              </select>
              <SearchConsoleApp-button variant="secondary" type="submit" [disabled]="scheduleSaving()">
                {{ scheduleSaving() ? 'Kaydediliyor…' : 'Zamanla' }}
              </SearchConsoleApp-button>
            </form>
          </details>
        </section>
      }

      @if (hasActiveScan()) {
        <p class="active-scan-note">Devam eden bir tarama var. Yeni tarama veya başka sonuç görüntüleme için önce durdurun.</p>
      }

      <form class="audit-form" (submit)="onSubmit($event)">
        <input
          type="text"
          name="url"
          [(ngModel)]="urlInput"
          placeholder="ornek.com veya https://ornek.com"
          [disabled]="loading() || hasActiveScan()"
          required
        />
        <SearchConsoleApp-button variant="primary" type="submit" [disabled]="loading() || hasActiveScan()">
          {{ loading() ? 'Taranıyor…' : 'Taramayı Başlat' }}
        </SearchConsoleApp-button>
      </form>

      @if (globalIntegrations().length) {
        <SearchConsoleApp-integration-status-panel
          title="Sistem entegrasyonları"
          [editable]="true"
          [collapsible]="true"
          [defaultOpen]="integrationsPanelOpen()"
          [items]="globalIntegrations()"
          (changed)="loadGlobalIntegrations()" />
      }

      @if (oauthGuide()) {
        <SearchConsoleApp-oauth-setup-guide [guide]="oauthGuide()!" />
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      }
    </section>

    @if (detail()) {
      @if (completionAlert()) {
        <div class="completion-alert" [class.alert-warning]="completionAlert()!.severity === 'warning'">
          <strong>{{ completionAlert()!.title }}</strong>
          <span>{{ completionAlert()!.message }}</span>
          <button type="button" class="alert-dismiss" (click)="completionAlert.set(null)" aria-label="Kapat">×</button>
        </div>
      }

      <section class="audit-results surface">
        <header class="results-header">
          <div>
            <h2>{{ detail()!.run.normalizedUrl }}</h2>
            <span class="status" [class]="statusClass()">{{ statusLabel() }}</span>
            @if (detail()!.run.mode === 'Connected') {
              <span class="mode-badge">Search Console</span>
            }
          </div>
          <div class="results-actions">
            @if (isRunning()) {
              <SearchConsoleApp-button
                variant="danger"
                size="sm"
                [disabled]="stopping()"
                (click)="stopAudit()">
                {{ stopping() ? 'Durduruluyor…' : 'Taramayı Durdur' }}
              </SearchConsoleApp-button>
            }
            @if (detail()!.run.score != null && !isRunning()) {
              <div class="score" [class]="scoreClass()">
                <span class="score-value">{{ detail()!.run.score }}<span class="score-max">/100</span></span>
                <span class="score-label">SEO Skoru <SearchConsoleApp-hint [text]="hints.seoScore" /></span>
                @if (scoreBreakdown(); as sb) {
                  <span class="score-hint">
                    {{ sb.distinctCritical }} kritik · {{ sb.distinctWarning }} uyarı · {{ sb.distinctInfo }} bilgi kuralı
                  </span>
                }
              </div>
            }
          </div>
        </header>

        <div class="stats">
          <div class="stat critical">
            <strong>{{ detail()!.run.criticalCount }}</strong>
            <span class="stat-label">Kritik <SearchConsoleApp-hint [text]="hints.critical" /></span>
          </div>
          <div class="stat warning">
            <strong>{{ detail()!.run.warningCount }}</strong>
            <span class="stat-label">Uyarı <SearchConsoleApp-hint [text]="hints.warning" /></span>
          </div>
          <div class="stat info">
            <strong>{{ detail()!.run.infoCount }}</strong>
            <span class="stat-label">Bilgi <SearchConsoleApp-hint [text]="hints.info" /></span>
          </div>
          <div class="stat">
            <strong>{{ detail()!.run.pagesCrawled }}</strong>
            <span class="stat-label">Sayfa <SearchConsoleApp-hint [text]="hints.pagesCrawled" /></span>
          </div>
        </div>

        @if (detail()!.run.status === 'Completed') {
          <SearchConsoleApp-critical-issues-report
            [issues]="detail()!.issues"
            [criticalCount]="detail()!.run.criticalCount"
            [completed]="true"
            [canExport]="true"
            (exportClicked)="exportCriticalHtmlReport()" />
        }

        @if (runIntegrations().length) {
          <SearchConsoleApp-integration-status-panel
            title="Bu taramada çalışan adımlar"
            [collapsible]="true"
            [defaultOpen]="false"
            [items]="runIntegrations()" />
        }

        <SearchConsoleApp-scanned-pages-panel
          [pages]="detail()!.pages"
          [issues]="detail()!.issues" />

        @if (panelUnavailable().pageSpeed) {
          <p class="panel-unavailable">{{ panelUnavailable().pageSpeed }}</p>
        }
        @if (panelUnavailable().indexStatus) {
          <p class="panel-unavailable">{{ panelUnavailable().indexStatus }}</p>
        }
        @if (panelUnavailable().contentQuality) {
          <p class="panel-unavailable">{{ panelUnavailable().contentQuality }}</p>
        }

        @if (performance()) {
          <section class="panel">
            <h3>Search Console — Son 28 Gün <SearchConsoleApp-hint [text]="hints.scPerformance28" /></h3>
            <div class="stats">
              <div class="stat">
                <strong>{{ performance()!.totalClicks28d }}</strong>
                <span class="stat-label">Tıklama <SearchConsoleApp-hint [text]="hints.clicks" /></span>
              </div>
              <div class="stat">
                <strong>{{ performance()!.totalImpressions28d }}</strong>
                <span class="stat-label">Gösterim <SearchConsoleApp-hint [text]="hints.impressions" /></span>
              </div>
            </div>
            @if (performance()!.topQueries.length > 0) {
              <table class="perf-table">
                <thead><tr>
                  <th>Sorgu</th>
                  <th>Tıklama <SearchConsoleApp-hint [text]="hints.clicks" /></th>
                  <th>Gösterim <SearchConsoleApp-hint [text]="hints.impressions" /></th>
                  <th>CTR <SearchConsoleApp-hint [text]="hints.ctr" /></th>
                  <th>Pozisyon <SearchConsoleApp-hint [text]="hints.position" /></th>
                </tr></thead>
                <tbody>
                  @for (q of performance()!.topQueries.slice(0, 10); track q.query) {
                    <tr>
                      <td>{{ q.query || '(sağlanmadı)' }}</td>
                      <td>{{ q.clicks }}</td>
                      <td>{{ q.impressions }}</td>
                      <td>{{ (q.ctr * 100) | number:'1.1-1' }}%</td>
                      <td>{{ q.position | number:'1.1-1' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </section>
        }

        @if (scCoverage()) {
          <section class="panel">
            <h3>Search Console — İndeks Kapsamı <SearchConsoleApp-hint [text]="hints.scCoverage" /></h3>
            <div class="stats">
              <div class="stat">
                <strong>{{ scCoverage()!.passedCount ?? scCoverage()!.indexedPages ?? 0 }}</strong>
                <span class="stat-label">İndekslendi <SearchConsoleApp-hint [text]="hints.indexed" /></span>
              </div>
              <div class="stat">
                <strong>{{ scCoverage()!.failedCount ?? scCoverage()!.excludedPages ?? 0 }}</strong>
                <span class="stat-label">Hariç / sorun <SearchConsoleApp-hint [text]="hints.excluded" /></span>
              </div>
              <div class="stat">
                <strong>{{ scCoverage()!.inspectedCount ?? 0 }}</strong>
                <span class="stat-label">URL denetlendi <SearchConsoleApp-hint [text]="hints.inspected" /></span>
              </div>
            </div>
            @if (scCoverage()!.sitemaps && scCoverage()!.sitemaps!.length > 0) {
              <table class="perf-table">
                <thead><tr>
                  <th>Site Haritası <SearchConsoleApp-hint [text]="hints.sitemapPath" /></th>
                  <th>Hata <SearchConsoleApp-hint [text]="hints.sitemapErrors" /></th>
                  <th>Uyarı <SearchConsoleApp-hint [text]="hints.sitemapWarnings" /></th>
                </tr></thead>
                <tbody>
                  @for (sm of scCoverage()!.sitemaps!; track sm.path) {
                    <tr>
                      <td class="issue-url">{{ sm.path }}</td>
                      <td [class.low-score]="sm.errors > 0">{{ sm.errors }}</td>
                      <td>{{ sm.warnings }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </section>
        }

        @if (keywordSerp().length > 0) {
          <section class="panel">
            <h3>SERP — Takip edilen anahtar kelimeler <SearchConsoleApp-hint [text]="hints.serpKeywords" /></h3>
            <table class="perf-table">
              <thead><tr>
                <th>Kelime</th>
                <th>Pozisyon <SearchConsoleApp-hint [text]="hints.serpPosition" /></th>
                <th>URL <SearchConsoleApp-hint [text]="hints.serpMatchedUrl" /></th>
              </tr></thead>
              <tbody>
                @for (s of keywordSerp(); track s.keyword) {
                  <tr>
                    <td>{{ s.keyword }}</td>
                    <td [class.low-score]="s.position === 0">{{ s.position > 0 ? s.position : '10+' }}</td>
                    <td class="issue-url">{{ s.matchedUrl || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        }

        @if (keywords().length > 0) {
          <section class="panel">
            <h3>Search Console — Anahtar Kelimeler <SearchConsoleApp-hint [text]="hints.scKeywords" /></h3>
            <table class="perf-table">
              <thead><tr>
                <th>Sorgu</th>
                <th>Tıklama <SearchConsoleApp-hint [text]="hints.clicks" /></th>
                <th>Gösterim <SearchConsoleApp-hint [text]="hints.impressions" /></th>
                <th>CTR <SearchConsoleApp-hint [text]="hints.ctr" /></th>
                <th>Pozisyon <SearchConsoleApp-hint [text]="hints.position" /></th>
              </tr></thead>
              <tbody>
                @for (k of keywords().slice(0, 15); track k.keyword) {
                  <tr>
                    <td>{{ k.keyword }}</td>
                    <td>{{ k.clicks }}</td>
                    <td>{{ k.impressions }}</td>
                    <td>{{ (k.ctr * 100) | number:'1.1-1' }}%</td>
                    <td>{{ k.position | number:'1.1-1' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        }

        @if (contentQuality().length > 0) {
          <section class="panel">
            <h3>İçerik Kalitesi (E-E-A-T) <SearchConsoleApp-hint [text]="hints.contentQuality" /> <span class="ai-badge">AI</span></h3>
            @for (cq of contentQuality(); track cq.url) {
              <div class="cq-row">
                <div class="cq-head">
                  <span class="cq-score" [class.low]="cq.eeatScore < 50" title="E-E-A-T skoru">{{ cq.eeatScore }}</span>
                  <SearchConsoleApp-hint [text]="hints.eeatScore" />
                  <span class="cq-url">{{ cq.url }}</span>
                </div>
                @if (parseChecklist(cq.checklistJson); as items) {
                  @if (items.length) {
                    <ul class="cq-checklist">
                      @for (item of items; track item) {
                        <li>{{ item }}</li>
                      }
                    </ul>
                  }
                }
                @if (parseSuggestions(cq.suggestionsJson); as tips) {
                  @if (tips.length) {
                    <h5 class="ai-suggestions-title">AI önerileri</h5>
                    <ul class="cq-suggestions">
                      @for (tip of tips; track tip) {
                        <li>{{ tip }}</li>
                      }
                    </ul>
                  }
                }
              </div>
            }
          </section>
        }

        @if (isRunning() || detail()!.run.progressMessage) {
          <p class="progress">{{ progressMessage() }}</p>
        }

        @if (pageSpeed().length > 0) {
          <section class="panel">
            <h3>Core Web Vitals (PageSpeed) <SearchConsoleApp-hint [text]="hints.pageSpeed" /></h3>
            <table class="perf-table">
              <thead><tr>
                <th>URL</th>
                <th>Skor <SearchConsoleApp-hint [text]="hints.performanceScore" /></th>
                <th>LCP <SearchConsoleApp-hint [text]="hints.lcp" /></th>
                <th>INP <SearchConsoleApp-hint [text]="hints.inp" /></th>
                <th>CLS <SearchConsoleApp-hint [text]="hints.cls" /></th>
              </tr></thead>
              <tbody>
                @for (p of pageSpeed().slice(0, 10); track p.url) {
                  <tr>
                    <td class="issue-url">{{ p.url }}</td>
                    <td [class.low-score]="p.performanceScore < 50">{{ p.performanceScore }}</td>
                    <td>{{ p.lcp || '—' }}</td>
                    <td>{{ p.inp || '—' }}</td>
                    <td>{{ p.cls || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        }

        @if (indexStatus()) {
          <section class="panel">
            <h3>Google İndeks Durumu <SearchConsoleApp-hint [text]="hints.indexStatus" /></h3>
            <div class="stats">
              <div class="stat">
                <strong>{{ indexStatus()!.estimatedIndexedPages }}</strong>
                <span class="stat-label">Tahmini indeks <SearchConsoleApp-hint [text]="hints.estimatedIndexed" /></span>
              </div>
              <div class="stat">
                <strong>{{ indexStatus()!.crawledPages }}</strong>
                <span class="stat-label">Taranan <SearchConsoleApp-hint [text]="hints.crawledPages" /></span>
              </div>
              <div class="stat">
                <strong>{{ (indexStatus()!.coverageRatio * 100) | number:'1.0-0' }}%</strong>
                <span class="stat-label">Kapsama <SearchConsoleApp-hint [text]="hints.coverageRatio" /></span>
              </div>
            </div>
            <p class="panel-note">Kaynak: {{ indexStatus()!.source }}</p>
          </section>
        }

        @if (backlinks()) {
          <section class="panel">
            <h3>Link profili <SearchConsoleApp-hint [text]="hints.linkProfile" /></h3>
            <div class="stats">
              <div class="stat">
                <strong>{{ backlinks()!.internalLinkCount }}</strong>
                <span class="stat-label">Dahili link <SearchConsoleApp-hint [text]="hints.internalLinks" /></span>
              </div>
              <div class="stat">
                <strong>{{ backlinks()!.uniqueInternalTargets }}</strong>
                <span class="stat-label">Hedef sayfa <SearchConsoleApp-hint [text]="hints.targetPages" /></span>
              </div>
              <div class="stat">
                <strong>{{ backlinks()!.orphanPageCount }}</strong>
                <span class="stat-label">Yetim sayfa <SearchConsoleApp-hint [text]="hints.orphanPages" /></span>
              </div>
              @if (backlinks()!.externalReferringDomainCount != null) {
                <div class="stat">
                  <strong>{{ backlinks()!.externalReferringDomainCount }}</strong>
                  <span class="stat-label">Harici domain ({{ backlinks()!.externalSource }}) <SearchConsoleApp-hint [text]="hints.externalDomains" /></span>
                </div>
              }
            </div>
            @if (backlinks()!.externalTopDomains && backlinks()!.externalTopDomains!.length > 0) {
              <p class="panel-note">Top referring: {{ backlinks()!.externalTopDomains!.join(', ') }}</p>
            }
          </section>
        }

        @if (detail()!.run.status === 'Completed') {
          <p class="export-row">
            <SearchConsoleApp-button variant="secondary" (click)="exportReport()">JSON olarak dışa aktar</SearchConsoleApp-button>
            <SearchConsoleApp-button variant="secondary" (click)="exportHtmlReport()">HTML / PDF rapor</SearchConsoleApp-button>
            @if (detail()!.run.criticalCount > 0) {
              <SearchConsoleApp-button variant="secondary" (click)="exportCriticalHtmlReport()">Kritik sorunlar raporu</SearchConsoleApp-button>
            }
          </p>
        }

        @if (detail()!.issues.length > 0) {
          <div class="issue-toolbar">
            <h3>Bulunan Sorunlar ({{ filteredIssues().length }})</h3>
            <div class="issue-filters">
              <label class="filter-label">
                Grupla:
                <select [ngModel]="groupBy()" (ngModelChange)="onGroupByChange($event)">
                  <option value="message">Soruna göre</option>
                  <option value="category">Kategoriye göre</option>
                  <option value="page">Sayfaya göre</option>
                  <option value="severity">Öneme göre</option>
                </select>
                <SearchConsoleApp-hint [text]="hints.issueGroupBy" />
              </label>
              @if (categories().length > 1) {
                <label class="filter-label">
                  Filtre:
                  <select [ngModel]="categoryFilter()" (ngModelChange)="categoryFilter.set($event)">
                    <option value="">Tüm kategoriler</option>
                    @for (cat of categories(); track cat) {
                      <option [value]="cat">{{ categoryLabel(cat) }}</option>
                    }
                  </select>
                  <SearchConsoleApp-hint [text]="hints.issueCategoryFilter" />
                </label>
              }
              <label class="filter-label">
                Önem:
                <select [ngModel]="severityFilter()" (ngModelChange)="severityFilter.set($event)">
                  <option value="">Tümü</option>
                  <option value="Critical">Kritik</option>
                  <option value="Warning">Uyarı</option>
                  <option value="Info">Bilgi</option>
                </select>
                <SearchConsoleApp-hint [text]="hints.issueSeverityFilter" />
              </label>
            </div>
          </div>

          @if (groupBy() === 'message') {
            <div class="issue-msg-groups">
              @for (group of groupedIssues(); track group.key) {
                <details class="issue-msg-group" [class]="'issue-' + group.topSeverity.toLowerCase()">
                  <summary class="issue-msg-summary">
                    <span class="badge">{{ severityLabel(group.topSeverity) }}</span>
                    <span class="issue-msg">{{ group.label }}</span>
                    <span class="group-meta">{{ group.issues.length }} kez</span>
                    <span class="cat-badge">{{ categoryLabel(group.category) }}</span>
                    <span class="rule-id">{{ group.ruleId }}</span>
                    <span class="expand-hint">Detay</span>
                  </summary>
                  <div class="issue-msg-body">
                    @if (group.fixHint) {
                      <p class="issue-fix"><strong>Öneri:</strong> {{ group.fixHint }}</p>
                    }
                    @if (playbookForGroup(group); as guide) {
                      <div class="issue-guide">
                        <h5 class="guide-title">Özet öneriler</h5>
                        <ul class="guide-summary">
                          @for (tip of guide.summary; track tip) {
                            <li>{{ tip }}</li>
                          }
                        </ul>
                        <h5 class="guide-title">Uygulama örneği</h5>
                        <pre class="guide-example"><code>{{ guide.example }}</code></pre>
                      </div>
                    }
                    <h5 class="guide-title affected-title">Etkilenen sayfalar ({{ group.issues.length }})</h5>
                    <ul class="issue-detail-list">
                      @for (issue of group.issues; track issue.entityId) {
                        <li>
                          <p class="issue-url">{{ issue.pageUrl || 'Site geneli' }}</p>
                          @switch (evidenceKind(issue.evidence)) {
                            @case ('h1-multiple') {
                              <SearchConsoleApp-issue-h1-table [evidence]="issue.evidence" />
                            }
                            @case ('img-alt-missing') {
                              <SearchConsoleApp-issue-img-alt-table [evidence]="issue.evidence" />
                            }
                            @case ('issue-detail') {
                              <SearchConsoleApp-issue-detail-table [evidence]="issue.evidence" />
                            }
                            @default {
                              @if (issue.evidence) {
                                <p class="issue-evidence">{{ issue.evidence }}</p>
                              }
                            }
                          }
                          @if (aiActionKind(group.ruleId) && issue.pageUrl && detail()?.run.entityId) {
                            <SearchConsoleApp-audit-ai-actions
                              [auditEntityId]="detail()!.run.entityId"
                              [pageUrl]="issue.pageUrl"
                              [action]="aiActionKind(group.ruleId)!"
                              [metaTarget]="aiMetaTarget(group.ruleId)"
                              [evidence]="issue.evidence"
                              [geminiEnabled]="geminiAvailable()" />
                          }
                        </li>
                      }
                    </ul>
                    @if (group.docUrl) {
                      <a class="doc-link" [href]="group.docUrl" target="_blank" rel="noopener">Google dokümantasyonu →</a>
                    }
                  </div>
                </details>
              }
            </div>
          } @else {
            @for (group of groupedIssues(); track group.key) {
              <section class="issue-group">
                <header class="issue-group-head">
                  <h4>{{ group.label }}</h4>
                  <span class="group-count">{{ group.issues.length }} sorun</span>
                  @if (group.criticalCount > 0) {
                    <span class="group-pill critical">{{ group.criticalCount }} kritik</span>
                  }
                  @if (group.warningCount > 0) {
                    <span class="group-pill warning">{{ group.warningCount }} uyarı</span>
                  }
                  @if (group.infoCount > 0) {
                    <span class="group-pill info">{{ group.infoCount }} bilgi</span>
                  }
                </header>
                <ul class="issue-list">
                  @for (issue of group.issues; track issue.entityId) {
                    <li [class]="'issue-' + issue.severity.toLowerCase()">
                      <div class="issue-head">
                        <span class="badge">{{ severityLabel(issue.severity) }}</span>
                        @if (groupBy() !== 'category') {
                          <span class="cat-badge">{{ categoryLabel(issue.category) }}</span>
                        }
                        @if (groupBy() !== 'page' && issue.pageUrl) {
                          <span class="issue-url-inline">{{ issue.pageUrl }}</span>
                        }
                        <span class="rule-id">{{ issue.ruleId }}</span>
                      </div>
                      <p class="issue-msg">{{ issue.message }}</p>
                      @if (playbookForIssue(issue); as guide) {
                        <div class="issue-guide">
                          <h5 class="guide-title">Özet öneriler</h5>
                          <ul class="guide-summary">
                            @for (tip of guide.summary; track tip) {
                              <li>{{ tip }}</li>
                            }
                          </ul>
                          <h5 class="guide-title">Uygulama örneği</h5>
                          <pre class="guide-example"><code>{{ guide.example }}</code></pre>
                        </div>
                      } @else if (issue.fixHint) {
                        <p class="issue-fix"><strong>Öneri:</strong> {{ issue.fixHint }}</p>
                      }
                      @switch (evidenceKind(issue.evidence)) {
                        @case ('h1-multiple') {
                          <SearchConsoleApp-issue-h1-table [evidence]="issue.evidence" />
                        }
                        @case ('img-alt-missing') {
                          <SearchConsoleApp-issue-img-alt-table [evidence]="issue.evidence" />
                        }
                        @case ('issue-detail') {
                          <SearchConsoleApp-issue-detail-table [evidence]="issue.evidence" />
                        }
                        @default {
                          @if (issue.evidence) {
                            <p class="issue-evidence">{{ issue.evidence }}</p>
                          }
                        }
                      }
                      @if (aiActionKind(issue.ruleId) && issue.pageUrl && detail()?.run.entityId) {
                        <SearchConsoleApp-audit-ai-actions
                          [auditEntityId]="detail()!.run.entityId"
                          [pageUrl]="issue.pageUrl"
                          [action]="aiActionKind(issue.ruleId)!"
                          [metaTarget]="aiMetaTarget(issue.ruleId)"
                          [evidence]="issue.evidence"
                          [geminiEnabled]="geminiAvailable()" />
                      }
                      @if (issue.docUrl) {
                        <a [href]="issue.docUrl" target="_blank" rel="noopener">Google dokümantasyonu →</a>
                      }
                    </li>
                  }
                </ul>
              </section>
            }
          }
        }

        @if (detail()!.run.errorMessage) {
          <p class="error">{{ detail()!.run.errorMessage }}</p>
        }
      </section>
    }
  `,
  styles: [`
    .audit-hero { padding: var(--space-6); margin-bottom: var(--space-4); }
    .hero-nav { margin: 0 0 var(--space-3); font-size: 0.95rem; }
    .hero-nav a { color: #4285f4; text-decoration: none; }
    .hero-nav a:hover { text-decoration: underline; }
    .subtitle { color: var(--color-text-muted, #666); margin-bottom: var(--space-4); }
    .audit-form { display: flex; gap: var(--space-3); flex-wrap: wrap; }
    .audit-form input {
      flex: 1; min-width: 240px; padding: 0.6rem 1rem;
      border: 1px solid var(--color-border, #ccc); border-radius: 6px; font-size: 1rem;
    }
    .error { color: #c0392b; margin-top: var(--space-3); }
    .audit-results { padding: var(--space-6); }
    .results-header { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--space-4); flex-wrap: wrap; }
    .status { font-size: 0.85rem; padding: 0.2rem 0.6rem; border-radius: 4px; background: #eee; }
    .status.completed { background: #d4edda; color: #155724; }
    .status.failed { background: #f8d7da; color: #721c24; }
    .status.crawling, .status.analyzing, .status.pending { background: #fff3cd; color: #856404; }
    .status.cancelled { background: #e8ecf0; color: #475569; }
    .active-scan-note {
      margin: 0 0 var(--space-3); padding: var(--space-2) var(--space-3);
      background: #fff8e6; border: 1px solid #f0d78c; border-radius: 6px;
      font-size: 0.9rem; color: #856404;
    }
    .results-actions { display: flex; align-items: flex-start; gap: var(--space-3); flex-wrap: wrap; }
    .link-btn-disabled { opacity: 0.45; cursor: not-allowed; pointer-events: none; }
    .score { text-align: center; min-width: 80px; }
    .score-value { display: block; font-size: 2.5rem; font-weight: 700; line-height: 1; }
    .score-max { font-size: 1rem; font-weight: 500; color: #888; margin-left: 0.1rem; }
    .score-label { font-size: 0.75rem; color: #666; }
    .score-hint { display: block; font-size: 0.65rem; color: #888; margin-top: 0.15rem; max-width: 12rem; line-height: 1.3; }
    .score.good .score-value { color: #27ae60; }
    .score.mid .score-value { color: #f39c12; }
    .score.bad .score-value { color: #e74c3c; }
    .stats { display: flex; gap: var(--space-4); margin: var(--space-4) 0; flex-wrap: wrap; }
    .stat { padding: var(--space-3); background: var(--color-surface-2, #f5f5f5); border-radius: 6px; min-width: 90px; text-align: center; }
    .stat-label { display: inline-flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 0.1rem; font-size: 0.85rem; color: var(--color-text-muted, #555); margin-top: 0.15rem; }
    .panel h3 { display: inline-flex; align-items: center; flex-wrap: wrap; gap: 0.15rem; }
    .perf-table th { white-space: nowrap; }
    .stat.critical strong { color: #e74c3c; }
    .stat.warning strong { color: #f39c12; }
    .stat.info strong { color: #3498db; }
    .progress { color: #666; font-style: italic; }
    .issue-toolbar { display: flex; justify-content: space-between; align-items: center; gap: var(--space-3); flex-wrap: wrap; margin-bottom: var(--space-4); }
    .issue-filters { display: flex; gap: var(--space-3); flex-wrap: wrap; align-items: center; }
    .filter-label { font-size: 0.85rem; color: #555; display: inline-flex; align-items: center; gap: 0.4rem; }
    .issue-toolbar select { padding: 0.4rem 0.6rem; border-radius: 6px; border: 1px solid #ccc; }
    .issue-group { margin-bottom: var(--space-5); }
    .issue-group-head {
      display: flex; align-items: center; gap: var(--space-2); flex-wrap: wrap;
      padding-bottom: var(--space-2); margin-bottom: var(--space-2);
      border-bottom: 2px solid var(--color-border, #e0e0e0);
    }
    .issue-group-head h4 { margin: 0; font-size: 1rem; flex: 1; min-width: 120px; word-break: break-all; }
    .group-count { font-size: 0.8rem; color: #666; }
    .group-pill { font-size: 0.7rem; font-weight: 600; padding: 0.15rem 0.45rem; border-radius: 4px; }
    .group-pill.critical { background: #fdecea; color: #c0392b; }
    .group-pill.warning { background: #fef5e7; color: #d68910; }
    .group-pill.info { background: #ebf5fb; color: #2980b9; }
    .issue-msg-groups { display: flex; flex-direction: column; gap: var(--space-2); }
    .issue-msg-group {
      border: 1px solid #eee; border-radius: 8px; overflow: hidden;
      background: var(--color-surface, #fff);
    }
    .issue-msg-group.issue-critical { border-left: 4px solid #e74c3c; }
    .issue-msg-group.issue-warning { border-left: 4px solid #f39c12; }
    .issue-msg-group.issue-info { border-left: 4px solid #3498db; }
    .issue-msg-summary {
      display: flex; align-items: center; gap: var(--space-2); flex-wrap: wrap;
      padding: var(--space-3); cursor: pointer; list-style: none;
    }
    .issue-msg-summary::-webkit-details-marker { display: none; }
    .issue-msg-summary .issue-msg { flex: 1; min-width: 200px; margin: 0; font-weight: 500; }
    .group-meta { font-size: 0.8rem; color: #666; white-space: nowrap; }
    .expand-hint { font-size: 0.75rem; color: #888; margin-left: auto; }
    .issue-msg-group[open] .expand-hint { color: var(--color-primary, #2563eb); }
    .issue-msg-body { padding: 0 var(--space-3) var(--space-3); border-top: 1px solid #f0f0f0; }
    .issue-detail-list { list-style: none; padding: 0; margin: var(--space-2) 0 0; }
    .issue-detail-list li {
      padding: var(--space-2) 0; border-bottom: 1px solid #f5f5f5;
    }
    .issue-detail-list li:last-child { border-bottom: none; }
    .doc-link { display: inline-block; margin-top: var(--space-2); font-size: 0.85rem; }
    .issue-guide {
      margin: var(--space-3) 0; padding: var(--space-3);
      background: #f8fafc; border-radius: 8px; border: 1px solid #e8ecf0;
    }
    .guide-title { margin: 0 0 var(--space-2); font-size: 0.85rem; color: #444; font-weight: 600; }
    .guide-title.affected-title { margin-top: var(--space-3); }
    .guide-summary { margin: 0 0 var(--space-3); padding-left: 1.25rem; font-size: 0.85rem; color: #333; }
    .guide-summary li { margin-bottom: 0.35rem; }
    .guide-example {
      margin: 0; padding: var(--space-3); overflow-x: auto;
      background: #1e293b; color: #e2e8f0; border-radius: 6px;
      font-size: 0.75rem; line-height: 1.45;
    }
    .guide-example code { font-family: ui-monospace, "SF Mono", Menlo, monospace; white-space: pre-wrap; word-break: break-word; }
    .cat-badge { font-size: 0.7rem; background: #eef; color: #446; padding: 0.1rem 0.4rem; border-radius: 4px; }
    .issue-list { list-style: none; padding: 0; margin: 0; }
    .issue-list li { border: 1px solid #eee; border-radius: 8px; padding: var(--space-3); margin-bottom: var(--space-3); }
    .issue-critical { border-left: 4px solid #e74c3c; }
    .issue-warning { border-left: 4px solid #f39c12; }
    .issue-info { border-left: 4px solid #3498db; }
    .issue-head { display: flex; gap: var(--space-2); align-items: center; margin-bottom: 0.4rem; }
    .badge { font-size: 0.7rem; font-weight: 600; text-transform: uppercase; }
    .rule-id { font-family: monospace; font-size: 0.8rem; color: #888; }
    .issue-msg { margin: 0.25rem 0; font-weight: 500; }
    .issue-url { font-size: 0.8rem; color: #666; word-break: break-all; }
    .issue-url-inline { font-size: 0.75rem; color: #666; word-break: break-all; flex: 1; min-width: 0; }
    .issue-evidence { font-size: 0.8rem; font-family: monospace; background: #f8f8f8; padding: 0.3rem 0.5rem; border-radius: 4px; }
    .issue-fix { font-size: 0.85rem; color: #555; }
    .sc-banner { margin-bottom: var(--space-4); padding: var(--space-3); background: #f0f7ff; border-radius: 8px; display: flex; gap: var(--space-3); align-items: center; flex-wrap: wrap; }
    .sc-ok { color: #155724; font-weight: 600; }
    .sc-opt { font-size: 0.9rem; }
    .mode-badge { font-size: 0.75rem; background: #4285f4; color: #fff; padding: 0.15rem 0.5rem; border-radius: 4px; margin-left: 0.5rem; }
    .panel-note { font-size: 0.8rem; color: #666; margin: 0; }
    .export-row { margin: var(--space-3) 0; display: flex; gap: var(--space-2); flex-wrap: wrap; }
    .completion-alert {
      display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem 1rem;
      padding: var(--space-3); margin-bottom: var(--space-3); border-radius: 8px;
      background: #d4edda; border: 1px solid #c3e6cb; color: #155724; font-size: 0.9rem;
    }
    .completion-alert.alert-warning {
      background: #fff3cd; border-color: #ffeeba; color: #856404;
    }
    .alert-dismiss {
      margin-left: auto; border: none; background: transparent; font-size: 1.25rem;
      cursor: pointer; line-height: 1; color: inherit; opacity: 0.7;
    }
    .alert-dismiss:hover { opacity: 1; }
    .low-score { color: #e74c3c; font-weight: 700; }
    .perf-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; margin-top: var(--space-3); }
    .perf-table th, .perf-table td { padding: 0.4rem 0.6rem; border-bottom: 1px solid #eee; text-align: left; }
    .cq-row { margin-bottom: var(--space-2); }
    .cq-head { display: flex; gap: var(--space-2); align-items: center; }
    .cq-score { font-weight: 700; font-size: 1.2rem; min-width: 2.5rem; color: #27ae60; }
    .cq-score.low { color: #e74c3c; }
    .cq-url { font-size: 0.8rem; word-break: break-all; color: #555; }
    .dashboard-panel { margin-bottom: var(--space-4); padding: var(--space-3); background: #fafafa; border-radius: 8px; border: 1px solid #eee; }
    .dashboard-panel h3 { margin-top: 0; font-size: 1rem; }
    .schedule-form { margin-top: var(--space-3); }
    .schedule-inner { margin-top: var(--space-2); flex-wrap: wrap; }
    .link-btn { background: none; border: none; color: #4285f4; cursor: pointer; text-decoration: underline; font-size: 0.85rem; }
    .compact-watches li { display: flex; justify-content: space-between; align-items: center; padding: 0.35rem 0; border: none; }
    .schedule-list { list-style: none; margin: 0 0 1rem; padding: 0; font-size: 0.85rem; }
    .schedule-list li { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; padding: 0.4rem 0; border-bottom: 1px solid #eee; }
    .sch-meta { color: #64748b; font-size: 0.78rem; }
    .panel-note { font-size: 0.85rem; color: #64748b; margin: 0 0 0.75rem; }
    .panel-unavailable { font-size: 0.82rem; color: #92400e; background: #fffbeb; border: 1px solid #fde68a; padding: 0.5rem 0.75rem; border-radius: 6px; margin: 0.5rem 0; }
    .sc-property-select { display: flex; align-items: center; gap: 0.5rem; font-size: 0.85rem; margin-right: 0.5rem; }
    .cq-checklist, .cq-suggestions { margin: 0.35rem 0 0.5rem 1.5rem; font-size: 0.8rem; color: #475569; }
    .cq-suggestions { color: #0369a1; }
    .ai-suggestions-title { margin: 0.5rem 0 0.25rem 1.5rem; font-size: 0.78rem; font-weight: 600; color: #7c3aed; }
    .ai-badge { font-size: 0.65rem; background: #ede9fe; color: #6d28d9; padding: 0.1rem 0.35rem; border-radius: 4px; font-weight: 600; vertical-align: middle; }
  `],
})
export class HomeComponent implements OnInit {
  private auditApi = inject(AuditApiService);
  private scApi = inject(SearchConsoleApiService);
  private auditPoll = inject(AuditPollService);
  private notifications = inject(NotificationService);
  auth = inject(AuthService);
  private destroyRef = inject(DestroyRef);

  protected readonly categoryLabel = categoryLabel;
  protected readonly severityLabel = severityLabel;
  protected readonly hints = AUDIT_HINTS;

  urlInput = '';
  connectedMode = false;
  loading = signal(false);
  error = signal<string | null>(null);
  oauthGuide = signal<OAuthSetupGuide | null>(null);
  detail = signal<AuditDetailDto | null>(null);
  categoryFilter = signal('');
  severityFilter = signal('');
  completionAlert = signal<{ title: string; message: string; severity: 'info' | 'warning' } | null>(null);
  groupBy = signal<IssueGroupMode>('message');
  scConnected = signal(false);
  performance = signal<PerformanceDto | null>(null);
  contentQuality = signal<ContentQualityDto[]>([]);
  pageSpeed = signal<PageSpeedDto[]>([]);
  indexStatus = signal<IndexStatusDto | null>(null);
  backlinks = signal<BacklinkDto | null>(null);
  scCoverage = signal<SearchConsoleCoverageDto | null>(null);
  keywords = signal<KeywordDto[]>([]);
  keywordSerp = signal<KeywordSerpDto[]>([]);
  keywordWatches = signal<KeywordWatchDto[]>([]);
  watchSiteUrl = '';
  watchKeyword = '';
  watchSaving = signal(false);
  dashboardSites = signal<AuditDashboardSiteDto[]>([]);
  dashboardLoading = signal(false);
  scheduleSaving = signal(false);
  scheduleUrl = '';
  scheduleLabel = '';
  scheduleMigrationUrl = '';
  scheduleGa4Id = '';
  scheduleWebhookUrl = '';
  scheduleNotifyEmail = true;
  scheduleNotifyCriticalOnly = false;
  scheduleIntervalDays = 7;
  activeScanEntityId = signal<string | null>(null);
  stopping = signal(false);
  globalIntegrations = signal<IntegrationItemDto[]>([]);
  geminiAvailable = computed(() => {
    const gemini = this.globalIntegrations().find((i) => i.id === 'gemini');
    if (!gemini) return true;
    return gemini.enabled !== false && gemini.status === 'configured';
  });
  integrationsPanelOpen = computed(() =>
    this.globalIntegrations().some((i) => i.status === 'missing' || i.status === 'not_configured'),
  );
  runIntegrations = signal<IntegrationItemDto[]>([]);
  schedules = signal<ScheduledAuditDto[]>([]);
  scProperties = signal<SearchConsolePropertyDto[]>([]);
  selectedScProperty = '';
  panelUnavailable = signal<{ pageSpeed?: string; indexStatus?: string; contentQuality?: string }>({});

  useConnectedAudit = computed(() => this.scConnected() && this.auth.isAuthenticated());
  hasActiveScan = computed(() => this.activeScanEntityId() != null);

  ngOnInit(): void {
    this.loadGlobalIntegrations();

    if (this.auth.isAuthenticated()) {
      this.scApi.getStatus().pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of(null)),
      ).subscribe((s) => {
        this.scConnected.set(s?.connected ?? false);
        this.scProperties.set(s?.properties ?? []);
        if (s?.properties?.length && !this.selectedScProperty) {
          this.selectedScProperty = s.properties[0].siteUrl;
        }
        this.patchScIntegrationStatus();
        if (s?.connected) {
          this.loadDashboard();
          this.loadKeywordWatches();
          this.loadSchedules();
        }
      });

      this.notifications.userNotification$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
        if (/denetim|kritik/i.test(n.title)) {
          this.loadDashboard();
        }
      });
    }
  }

  private loadGlobalIntegrations(): void {
    this.auditApi.getIntegrationStatus().pipe(
      catchError(() => of({ integrations: [] as IntegrationItemDto[] })),
    ).subscribe((r) => {
      this.globalIntegrations.set(r.integrations);
      this.patchScIntegrationStatus();
    });
  }

  private patchScIntegrationStatus(): void {
    if (!this.scConnected()) return;
    this.globalIntegrations.update((items) =>
      items.map((i) => i.id === 'search-console'
        ? { ...i, status: 'connected', detail: 'Hesap bağlı' }
        : i),
    );
  }

  loadSchedules(): void {
    this.auditApi.listSchedules().pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of([])),
    ).subscribe((rows) => this.schedules.set(rows));
  }

  toggleSchedule(sch: ScheduledAuditDto): void {
    this.auditApi.updateSchedule(sch.entityId, { isEnabled: !sch.isEnabled }).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? 'Zamanlama güncellenemedi.');
        return of(null);
      }),
    ).subscribe((updated) => {
      if (updated) this.loadSchedules();
    });
  }

  deleteSchedule(entityId: string): void {
    this.auditApi.deleteSchedule(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? 'Zamanlama silinemedi.');
        return of(null);
      }),
    ).subscribe((r) => {
      if (r?.ok) {
        this.loadSchedules();
        this.loadDashboard();
      }
    });
  }

  parseChecklist(json: string | null | undefined): string[] {
    if (!json?.trim()) return [];
    try {
      const parsed = JSON.parse(json);
      if (Array.isArray(parsed)) return parsed.filter((x): x is string => typeof x === 'string');
      if (parsed && typeof parsed === 'object') {
        return Object.entries(parsed)
          .filter(([, v]) => v === true || v === false)
          .map(([k, v]) => `${k}: ${v ? '✓' : '✗'}`);
      }
    } catch { /* ignore */ }
    return [];
  }

  parseSuggestions(json: string | null | undefined): string[] {
    if (!json?.trim()) return [];
    try {
      const parsed = JSON.parse(json);
      if (Array.isArray(parsed)) return parsed.filter((x): x is string => typeof x === 'string');
    } catch { /* ignore */ }
    return [];
  }

  loadDashboard(): void {
    this.dashboardLoading.set(true);
    this.auditApi.getDashboard().pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of([])),
    ).subscribe((rows) => {
      this.dashboardSites.set(rows);
      this.dashboardLoading.set(false);
    });
  }

  onScheduleSubmit(event: Event): void {
    event.preventDefault();
    if (!this.scheduleUrl.trim()) return;
    this.scheduleSaving.set(true);
    this.auditApi.createSchedule({
      url: this.scheduleUrl.trim(),
      label: this.scheduleLabel.trim() || undefined,
      searchConsolePropertyUrl: this.selectedScProperty || undefined,
      migrationSourceUrl: this.scheduleMigrationUrl.trim() || undefined,
      ga4PropertyId: this.scheduleGa4Id.trim() || undefined,
      webhookUrl: this.scheduleWebhookUrl.trim() || undefined,
      notifyOnComplete: this.scheduleNotifyEmail,
      notifyOnCriticalOnly: this.scheduleNotifyCriticalOnly,
      intervalDays: this.scheduleIntervalDays,
    }).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? 'Zamanlama kaydedilemedi.');
        this.scheduleSaving.set(false);
        return of(null);
      }),
    ).subscribe((s) => {
      this.scheduleSaving.set(false);
      if (!s) return;
      this.scheduleUrl = '';
      this.scheduleLabel = '';
      this.scheduleMigrationUrl = '';
      this.scheduleGa4Id = '';
      this.scheduleWebhookUrl = '';
      this.scheduleNotifyEmail = true;
      this.scheduleNotifyCriticalOnly = false;
      this.loadDashboard();
      this.loadSchedules();
    });
  }

  openAudit(entityId: string): void {
    if (this.hasActiveScan() && this.activeScanEntityId() !== entityId) {
      this.error.set('Devam eden tarama var. Başka bir sonuç görüntülemek için önce taramayı durdurun.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.auditPoll.close();
    this.pollAudit(entityId);
  }

  loadKeywordWatches(): void {
    this.auditApi.listKeywordWatches().pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of([])),
    ).subscribe((rows) => this.keywordWatches.set(rows));
  }

  onKeywordWatchSubmit(event: Event): void {
    event.preventDefault();
    if (!this.watchSiteUrl.trim() || !this.watchKeyword.trim()) return;
    this.watchSaving.set(true);
    this.auditApi.createKeywordWatch(this.watchSiteUrl.trim(), this.watchKeyword.trim()).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? 'Anahtar kelime eklenemedi.');
        this.watchSaving.set(false);
        return of(null);
      }),
    ).subscribe((w) => {
      this.watchSaving.set(false);
      if (!w) return;
      this.watchKeyword = '';
      this.loadKeywordWatches();
    });
  }

  removeKeywordWatch(entityId: string): void {
    this.auditApi.deleteKeywordWatch(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of(null)),
    ).subscribe(() => this.loadKeywordWatches());
  }

  loginWithGoogle(): void {
    this.oauthGuide.set(null);
    this.error.set(null);
    this.auditApi.getGoogleLoginUrl(window.location.origin + '/').pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        const guide = parseOAuthSetupError(err);
        if (guide) {
          this.oauthGuide.set(guide);
          this.error.set(null);
        } else {
          this.error.set(oauthErrorMessage(err, 'Google girişi yapılandırılmamış.'));
        }
        return of(null);
      }),
    ).subscribe((r) => { if (r?.authorizeUrl) window.location.href = r.authorizeUrl; });
  }

  connectSearchConsole(): void {
    this.oauthGuide.set(null);
    this.error.set(null);
    this.scApi.getAuthorizeUrl(window.location.origin + '/auth/search-console/callback').pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        const guide = parseOAuthSetupError(err);
        if (guide) {
          this.oauthGuide.set(guide);
          this.error.set(null);
        } else {
          this.error.set(oauthErrorMessage(err, 'Search Console OAuth yapılandırılmamış.'));
        }
        return of(null);
      }),
    ).subscribe((r) => { if (r?.authorizeUrl) window.location.href = r.authorizeUrl; });
  }

  sortedIssues = computed(() => {
    const d = this.detail();
    if (!d) return [];
    return sortIssuesBySeverity(d.issues);
  });

  categories = computed(() => {
    const cats = new Set(this.sortedIssues().map((i) => i.category));
    return [...cats].sort();
  });

  filteredIssues = computed(() => {
    const cat = this.categoryFilter();
    const sev = this.severityFilter();
    let issues = this.sortedIssues();
    if (cat) issues = issues.filter((i) => i.category === cat);
    return filterIssuesBySeverity(issues, sev);
  });

  groupedIssues = computed(() => groupIssues(this.filteredIssues(), this.groupBy()));

  statusLabel = computed(() => auditStatusLabel(this.detail()?.run.status));

  progressMessage = computed(() => {
    const d = this.detail();
    if (!d) return '';
    if (d.run.progressMessage) return d.run.progressMessage;
    if (d.run.status === 'Analyzing') {
      return `Tarama bitti, PageSpeed ve ek kontroller çalışıyor… (${d.run.pagesCrawled} sayfa)`;
    }
    return `Tarama devam ediyor… ${d.run.pagesCrawled} sayfa tarandı.`;
  });

  onGroupByChange(value: IssueGroupMode): void {
    this.groupBy.set(value);
  }

  isRunning = computed(() => {
    const s = this.detail()?.run.status;
    return s === 'Pending' || s === 'Crawling' || s === 'Analyzing';
  });

  statusClass = computed(() => this.detail()?.run.status.toLowerCase() ?? '');

  scoreClass = computed(() => {
    const score = this.detail()?.run.score ?? 100;
    if (score >= 80) return 'score good';
    if (score >= 50) return 'score mid';
    return 'score bad';
  });

  scoreBreakdown = computed(() => {
    const issues = this.detail()?.issues ?? [];
    if (issues.length === 0) return null;
    return computeSeoScoreBreakdown(issues);
  });

  playbookFor(ruleId: string): RulePlaybookEntry | null {
    return getRulePlaybook(ruleId);
  }

  playbookForIssue(issue: AuditIssueDto): RulePlaybookEntry | null {
    return getRulePlaybookOrFallback(issue);
  }

  playbookForGroup(group: IssueGroup): RulePlaybookEntry | null {
    const pb = getRulePlaybook(group.ruleId);
    if (pb) return pb;
    if (group.fixHint || group.label) {
      return getRulePlaybookOrFallback({
        ruleId: group.ruleId,
        message: group.label,
        fixHint: group.fixHint,
        docUrl: group.docUrl,
      });
    }
    return null;
  }

  aiActionKind(ruleId: string): AuditAiAction | null {
    switch (ruleId) {
      case 'geo-faq-missing': return 'faq';
      case 'meta-title-missing':
      case 'meta-description-missing':
      case 'title-too-short':
      case 'title-too-long':
      case 'description-too-short':
      case 'description-too-long':
      case 'keyword-stuffing-title':
      case 'og-title-missing':
      case 'og-description-missing':
        return 'meta';
      case 'img-alt-missing': return 'alt-text';
      default: return null;
    }
  }

  aiMetaTarget(ruleId: string): MetaAiTarget {
    return ruleId === 'og-title-missing' || ruleId === 'og-description-missing' ? 'openGraph' : 'seo';
  }

  isFaqMissingRule(ruleId: string): boolean {
    return ruleId === 'geo-faq-missing';
  }

  isH1MultipleRule(ruleId: string): boolean {
    return ruleId === 'h1-multiple';
  }

  isImgAltMissingRule(ruleId: string): boolean {
    return ruleId === 'img-alt-missing';
  }

  evidenceKind(evidence: string | null | undefined): ReturnType<typeof detectStructuredEvidenceKind> {
    return detectStructuredEvidenceKind(evidence);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.hasActiveScan()) {
      this.error.set('Devam eden tarama var. Yeni tarama başlatmak için önce durdurun.');
      return;
    }

    const url = this.urlInput.trim();
    if (!url) return;

    this.loading.set(true);
    this.error.set(null);
    this.oauthGuide.set(null);
    this.detail.set(null);
    this.performance.set(null);
    this.contentQuality.set([]);
    this.pageSpeed.set([]);
    this.indexStatus.set(null);
    this.backlinks.set(null);
    this.scCoverage.set(null);
    this.keywords.set([]);
    this.keywordSerp.set([]);
    this.runIntegrations.set([]);
    this.panelUnavailable.set({});
    this.auditPoll.close();
    this.categoryFilter.set('');
    this.severityFilter.set('');
    this.completionAlert.set(null);
    this.groupBy.set('message');

    const scProperty = this.selectedScProperty.trim() || undefined;
    const start$ = this.connectedMode && this.scConnected() && this.auth.isAuthenticated()
      ? this.auditApi.startConnected(url, scProperty)
      : this.auditApi.start(url);

    start$.pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? 'Tarama başlatılamadı.');
        this.loading.set(false);
        return of(null);
      }),
    ).subscribe((run) => {
      if (!run) return;
      this.pollAudit(run.entityId);
    });
  }

  private pollAudit(entityId: string): void {
    this.activeScanEntityId.set(entityId);
    this.auditPoll.watch(
      entityId,
      this.destroyRef,
      (d) => {
        this.detail.set(d);
        this.loading.set(false);
        if (this.isTerminalStatus(d.run.status)) {
          this.activeScanEntityId.set(null);
          if (d.run.status === 'Completed') {
            this.showCompletionAlert(d);
            this.loadExtraPanels(d.run.entityId, d.run.mode);
          }
        }
      },
      (msg) => {
        this.error.set(msg);
        this.loading.set(false);
        this.activeScanEntityId.set(null);
      },
    );
  }

  stopAudit(): void {
    const id = this.detail()?.run.entityId ?? this.activeScanEntityId();
    if (!id || this.stopping()) return;

    this.stopping.set(true);
    this.auditApi.cancel(id).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.message ?? 'Tarama durdurulamadı.');
        this.stopping.set(false);
        return of(null);
      }),
    ).subscribe((run) => {
      this.stopping.set(false);
      if (!run) return;
      this.auditPoll.close();
      this.activeScanEntityId.set(null);
      this.loading.set(false);
      this.auditApi.get(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((d) => this.detail.set(d));
    });
  }

  private isTerminalStatus(status: string): boolean {
    return status === 'Completed' || status === 'Failed' || status === 'Cancelled';
  }

  exportReport(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    this.auditApi.exportJson(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `seo-audit-${id}.json`;
      a.click();
      URL.revokeObjectURL(a.href);
    });
  }

  exportHtmlReport(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    window.open(this.auditApi.exportHtmlUrl(id), '_blank', 'noopener');
  }

  exportCriticalHtmlReport(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    window.open(this.auditApi.exportCriticalHtmlUrl(id), '_blank', 'noopener');
  }

  private showCompletionAlert(d: AuditDetailDto): void {
    if (d.run.criticalCount > 0) {
      const distinct = new Set(
        d.issues.filter((i) => i.severity === 'Critical').map((i) => i.ruleId),
      ).size;
      this.completionAlert.set({
        title: `${d.run.criticalCount} kritik sorun tespit edildi`,
        message: `${distinct} farklı kritik kural — öncelikli düzeltme listesini inceleyin.`,
        severity: 'warning',
      });
      if (d.run.criticalCount > 0 && !this.severityFilter()) {
        this.severityFilter.set('Critical');
      }
      return;
    }
    this.completionAlert.set({
      title: 'Tarama tamamlandı',
      message: `SEO skoru ${d.run.score ?? '—'}/100 — kritik sorun bulunmadı.`,
      severity: 'info',
    });
  }

  private loadExtraPanels(entityId: string, mode: string): void {
    this.auditApi.getRunIntegrations(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of(null)),
    ).subscribe((r) => {
      if (r?.steps) this.runIntegrations.set(r.steps);
    });

    if (mode === 'Connected') {
      this.auditApi.getPerformance(entityId).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of(null)),
      ).subscribe((p) => { if (p) this.performance.set(p); });

      this.auditApi.getSearchConsoleCoverage(entityId).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of(null)),
      ).subscribe((c) => { if (c) this.scCoverage.set(c); });

      this.auditApi.getKeywords(entityId).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of([])),
      ).subscribe((k) => this.keywords.set(k));

      this.auditApi.getKeywordSerp(entityId).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of([])),
      ).subscribe((s) => this.keywordSerp.set(s));
    }

    this.auditApi.getContentQuality(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        const msg = err?.status === 503 ? err?.error?.message : null;
        if (msg) this.panelUnavailable.update((p) => ({ ...p, contentQuality: msg }));
        return of([]);
      }),
    ).subscribe((c) => this.contentQuality.set(c));

    this.auditApi.getPageSpeed(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        if (err?.status === 503 || (Array.isArray(err) && err.length === 0)) {
          this.panelUnavailable.update((p) => ({
            ...p,
            pageSpeed: 'PageSpeed Insights yapılandırılmadı (Google:PageSpeedApiKey).',
          }));
        }
        return of([]);
      }),
    ).subscribe((p) => {
      this.pageSpeed.set(p);
      if (!p.length && !this.panelUnavailable().pageSpeed) {
        this.panelUnavailable.update((prev) => ({
          ...prev,
          pageSpeed: 'PageSpeed verisi yok — API anahtarı eksik veya ölçüm atlandı.',
        }));
      }
    });

    this.auditApi.getIndexStatus(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.panelUnavailable.update((p) => ({
          ...p,
          indexStatus: 'İndeks durumu alınamadı — Custom Search yapılandırmasını kontrol edin.',
        }));
        return of(null);
      }),
    ).subscribe((i) => {
      this.indexStatus.set(i);
      if (!i && mode === 'Connected' && !this.panelUnavailable().indexStatus) {
        this.panelUnavailable.update((p) => ({
          ...p,
          indexStatus: 'İndeks durumu mevcut değil — bağlı mod veya Custom Search gerekir.',
        }));
      }
    });

    this.auditApi.getBacklinks(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of(null)),
    ).subscribe((b) => this.backlinks.set(b));
  }
}
