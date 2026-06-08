import {
  buildIssueDetail,
  EVIDENCE_LIST_LIMIT,
  listEvidence,
  pageElementEvidence,
  truncateText,
  type EvidenceItem,
} from "./evidence.js";

function capItems(items: EvidenceItem[], total: number): EvidenceItem[] {
  return items.slice(0, EVIDENCE_LIST_LIMIT);
}

export function extractCanonicalHrefs(html: string): string[] {
  const hrefs: string[] = [];
  const regex = /<link\b[^>]*rel=["']canonical["'][^>]*>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    const tag = match[0];
    const href = tag.match(/\bhref=["']([^"']+)["']/i)?.[1]?.trim();
    if (href) hrefs.push(href);
  }
  return hrefs;
}

export function extractAnchorLinks(
  html: string,
  filter: (href: string) => boolean,
  max = EVIDENCE_LIST_LIMIT,
): { href: string; text: string }[] {
  const results: { href: string; text: string }[] = [];
  const regex = /<a\b([^>]*)>([\s\S]*?)<\/a>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    const attrs = match[1];
    const href = attrs.match(/\bhref=["']([^"']*)["']/i)?.[1]?.trim() ?? "";
    if (!filter(href)) continue;
    const text = match[2].replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim() || "(metin yok)";
    results.push({ href, text: truncateText(text, 80) });
    if (results.length >= max) break;
  }
  return results;
}

export function extractMixedContentUrls(html: string): string[] {
  const urls = new Set<string>();
  const regex = /(?:src|href)=["'](http:\/\/[^"']+)["']/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    urls.add(match[1]);
    if (urls.size >= EVIDENCE_LIST_LIMIT) break;
  }
  return [...urls];
}

export function extractImagesWithoutSrcset(html: string): { src: string }[] {
  const results: { src: string }[] = [];
  const regex = /<img\b[^>]*>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    const tag = match[0];
    if (/\bsrcset=/i.test(tag)) continue;
    const src =
      tag.match(/\bsrc=["']([^"']+)["']/i)?.[1]
      ?? tag.match(/\bdata-src=["']([^"']+)["']/i)?.[1]
      ?? "(src yok)";
    results.push({ src });
    if (results.length >= EVIDENCE_LIST_LIMIT) break;
  }
  return results;
}

export function extractVideoEmbeds(html: string): EvidenceItem[] {
  const items: EvidenceItem[] = [];
  const videoRegex = /<video\b[^>]*>([\s\S]*?)<\/video>/gi;
  let m: RegExpExecArray | null;
  let i = 0;
  while ((m = videoRegex.exec(html)) !== null) {
    i++;
    const src = m[0].match(/\bsrc=["']([^"']+)["']/i)?.[1] ?? "(src attribute yok)";
    items.push({ label: `Video #${i}`, value: src, detail: "VideoObject schema ekleyin" });
  }
  const iframeRegex = /<iframe\b[^>]+src=["']([^"']*(?:youtube|vimeo)[^"']*)["'][^>]*>/gi;
  while ((m = iframeRegex.exec(html)) !== null) {
    i++;
    items.push({ label: `Embed #${i}`, value: m[1], detail: "VideoObject schema ekleyin" });
    if (items.length >= EVIDENCE_LIST_LIMIT) break;
  }
  return items;
}

export function extractRobotsMeta(html: string): string | null {
  const match = html.match(/<meta[^>]+name=["']robots["'][^>]+content=["']([^"']+)["']/i)
    ?? html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+name=["']robots["']/i);
  return match?.[1]?.trim() || null;
}

export function extractMetaKeywords(html: string): string | null {
  const match = html.match(/<meta[^>]+name=["']keywords["'][^>]+content=["']([^"']+)["']/i)
    ?? html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+name=["']keywords["']/i);
  return match?.[1]?.trim() || null;
}

export function extractHiddenTextMatches(html: string): string[] {
  const patterns = [
    /display\s*:\s*none[^;"']*/gi,
    /font-size\s*:\s*0[^;"']*/gi,
    /visibility\s*:\s*hidden[^;"']*/gi,
  ];
  const found = new Set<string>();
  for (const p of patterns) {
    for (const m of html.matchAll(p)) {
      found.add(truncateText(m[0], 60));
      if (found.size >= EVIDENCE_LIST_LIMIT) break;
    }
  }
  return [...found];
}

export function extractInterstitialMatches(html: string): string[] {
  const found: string[] = [];
  const regex = /<(?:div|section|aside)\b[^>]*(?:class|id)=["'][^"']*(?:modal|popup|interstitial|overlay)[^"']*["'][^>]*>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    found.push(truncateText(match[0], 100));
    if (found.length >= EVIDENCE_LIST_LIMIT) break;
  }
  return found;
}

export function extractDataVocabularySnippet(html: string): string | null {
  const idx = html.search(/data-vocabulary\.org/i);
  if (idx < 0) return null;
  return truncateText(html.slice(Math.max(0, idx - 40), idx + 80), 120);
}

export function extractInvalidJsonLdPreview(block: string): string {
  return truncateText(block, 100);
}

export function extractHreflangItems(html: string): EvidenceItem[] {
  const regex = /<link\b[^>]*hreflang=["']([^"']+)["'][^>]*href=["']([^"']+)["'][^>]*>/gi;
  const altRegex = /<link\b[^>]*href=["']([^"']+)["'][^>]*hreflang=["']([^"']+)["'][^>]*>/gi;
  const items: EvidenceItem[] = [];
  const add = (lang: string, href: string) => {
    items.push({ label: lang, value: href, href: /^https?:\/\//i.test(href) ? href : undefined });
  };
  let m: RegExpExecArray | null;
  while ((m = regex.exec(html)) !== null) add(m[1], m[2]);
  while ((m = altRegex.exec(html)) !== null) add(m[2], m[1]);
  return items.slice(0, EVIDENCE_LIST_LIMIT);
}

export function pathSegments(url: string): string[] {
  try {
    return new URL(url).pathname.split("/").filter(Boolean);
  } catch {
    return [];
  }
}

// --- Evidence builders used by analyzer ---

export function evidenceHttpStatus(statusCode: number): string {
  return pageElementEvidence(
    `HTTP ${statusCode}`,
    "Sunucu yanıtı",
    `Durum kodu: ${statusCode}`,
    statusCode >= 400 ? "404/5xx sayfayı düzeltin veya yönlendirin" : "Erişilebilir bir sayfa döndürün",
  );
}

export function evidenceTitleMissing(): string {
  return pageElementEvidence(
    "Title etiketi yok",
    "<head> → <title>",
    "Bulunamadı",
    "Her sayfaya benzersiz, açıklayıcı bir <title> ekleyin",
  );
}

export function evidenceTitleTooShort(title: string): string {
  return buildIssueDetail(`Title çok kısa (${title.length} karakter)`, [
    { label: "Konum", value: "<head> → <title>" },
    { label: "Mevcut", value: truncateText(title, 100) },
    { label: "Ne yapmalı", value: "En az 10 karakter; sayfa konusunu net anlatın" },
  ]);
}

export function evidenceTitleTooLong(title: string): string {
  return buildIssueDetail(`Title çok uzun (${title.length} karakter)`, [
    { label: "Konum", value: "<head> → <title>" },
    { label: "Mevcut", value: truncateText(title, 150) },
    { label: "Ne yapmalı", value: "70 karakter civarında kısaltın; önemli kelimeleri başa alın" },
  ]);
}

export function evidenceMetaDescriptionMissing(): string {
  return pageElementEvidence(
    "Meta description yok",
    "<head> → <meta name=\"description\">",
    "Bulunamadı",
    "150–160 karakterlik özet açıklama ekleyin",
  );
}

export function evidenceDescriptionLength(desc: string, kind: "short" | "long"): string {
  const headline = kind === "short"
    ? `Description çok kısa (${desc.length} karakter)`
    : `Description çok uzun (${desc.length} karakter)`;
  return buildIssueDetail(headline, [
    { label: "Konum", value: "<head> → meta description" },
    { label: "Mevcut", value: truncateText(desc, 160) },
    {
      label: "Ne yapmalı",
      value: kind === "short" ? "En az 50 karakter; sayfa özetini yazın" : "160 karaktere indirin",
    },
  ]);
}

export function evidenceViewportMissing(): string {
  return pageElementEvidence(
    "Viewport meta yok",
    "<head>",
    "<meta name=\"viewport\" ...> bulunamadı",
    "Mobil uyumluluk için viewport meta ekleyin",
  );
}

export function evidenceHttpsRequired(url: string): string {
  return buildIssueDetail("HTTPS kullanılmıyor", [
    { label: "Sayfa URL", value: url, href: url.startsWith("http") ? url : undefined },
    { label: "Ne yapmalı", value: "301 ile HTTPS sürümüne yönlendirin; tüm kaynaklar https:// olsun" },
  ]);
}

export function evidenceCanonicalMissing(): string {
  return pageElementEvidence(
    "Canonical etiketi yok",
    "<head> → <link rel=\"canonical\">",
    "Bulunamadı",
    "Tercih edilen URL için canonical link ekleyin",
  );
}

export function evidenceCanonicalMultiple(html: string): string {
  const hrefs = extractCanonicalHrefs(html);
  const items = capItems(
    hrefs.map((href, i) => ({ label: `Canonical #${i + 1}`, value: href, href })),
    hrefs.length,
  );
  return listEvidence(`${hrefs.length} canonical etiketi`, items, hrefs.length);
}

export function evidenceH1Missing(): string {
  return pageElementEvidence(
    "H1 başlığı yok",
    "<body> → <h1>",
    "Sayfada H1 bulunamadı",
    "Ana konu için tek bir açıklayıcı H1 ekleyin",
  );
}

export function evidenceNoindex(html: string): string {
  const robots = extractRobotsMeta(html);
  return buildIssueDetail("Noindex algılandı", [
    { label: "Konum", value: "<meta name=\"robots\">" },
    { label: "İçerik", value: robots ?? "noindex (içerik okunamadı)" },
    { label: "Ne yapmalı", value: "İndekslenmesini istiyorsanız noindex kaldırın" },
  ]);
}

export function evidenceXRobotsNoindex(header: string): string {
  return buildIssueDetail("X-Robots-Tag: noindex", [
    { label: "Konum", value: "HTTP yanıt başlığı" },
    { label: "X-Robots-Tag", value: header },
    { label: "Ne yapmalı", value: "Sunucu başlığından noindex kaldırın veya meta ile uyumlu hale getirin" },
  ]);
}

export function evidenceHtmlLangMissing(): string {
  return pageElementEvidence(
    "html lang yok",
    "<html lang=\"...\">",
    "lang attribute bulunamadı",
    "Sayfa dilini belirtin (ör. lang=\"tr\")",
  );
}

export function evidenceHreflangMissingXDefault(html: string): string {
  const items = extractHreflangItems(html);
  return listEvidence(
    "x-default hreflang eksik",
    [
      ...items,
      { label: "Eksik", value: "hreflang=\"x-default\"", detail: "Varsayılan dil/ülke sayfasını ekleyin" },
    ],
    items.length + 1,
  );
}

export function evidenceHreflangMissingReturn(pageUrl: string, html: string): string {
  const items = extractHreflangItems(html);
  return buildIssueDetail("Bu sayfa için hreflang self-link yok", [
    { label: "Sayfa", value: pageUrl },
    ...items.slice(0, 8),
    { label: "Ne yapmalı", value: "Her dil alternatifinde sayfanın kendi URL'si de hreflang olarak yer almalı" },
  ]);
}

export function evidenceDataVocabulary(html: string): string {
  const snippet = extractDataVocabularySnippet(html);
  return buildIssueDetail("Eski data-vocabulary.org kullanımı", [
    { label: "Konum", value: "HTML içeriği / schema" },
    { label: "Bulunan", value: snippet ?? "data-vocabulary.org referansı" },
    { label: "Ne yapmalı", value: "Schema.org JSON-LD veya microdata'ya geçin" },
  ]);
}

export function evidenceJsonLdMissing(): string {
  return pageElementEvidence(
    "JSON-LD yok",
    "<head> veya <body> → <script type=\"application/ld+json\">",
    "Structured data bulunamadı",
    "Sayfa türüne uygun JSON-LD ekleyin (Organization, Article, Product vb.)",
  );
}

export function evidenceJsonLdInvalid(block: string): string {
  return buildIssueDetail("Geçersiz JSON-LD", [
    { label: "Konum", value: "<script type=\"application/ld+json\">" },
    { label: "Hatalı blok", value: extractInvalidJsonLdPreview(block) },
    { label: "Ne yapmalı", value: "JSON sözdizimini düzeltin; Google Rich Results Test ile doğrulayın" },
  ]);
}

export function evidenceOgMissing(kind: "title" | "image" | "description"): string {
  const labels = { title: "og:title", image: "og:image", description: "og:description" };
  return pageElementEvidence(
    `${labels[kind]} eksik`,
    `<meta property="${labels[kind]}">`,
    "Bulunamadı",
    "Sosyal paylaşım için Open Graph meta ekleyin",
  );
}

export function evidenceBadLinks(kind: "javascript" | "hash", html: string, total: number): string {
  const filter = kind === "javascript"
    ? (h: string) => /^javascript:/i.test(h)
    : (h: string) => /^#\//.test(h);
  const links = extractAnchorLinks(html, filter);
  const headline = kind === "javascript"
    ? `${total} javascript: linki`
    : `${total} hash routing linki (#/)`;
  const items = capItems(
    links.map((l, i) => ({
      label: `Link #${i + 1}`,
      value: l.href,
      detail: `Anchor: "${l.text}"`,
    })),
    total,
  );
  return listEvidence(headline, items, total);
}

export function evidenceGeoFaqMissing(): string {
  return pageElementEvidence(
    "FAQ / SSS yapısı yok",
    "Sayfa içeriği veya JSON-LD",
    "FAQPage veya Question schema bulunamadı",
    "Sık sorulan sorular bölümü ve FAQPage schema ekleyin",
  );
}

export function evidenceGeoSummaryMissing(firstP: string, wordCount: number): string {
  return buildIssueDetail("Özet paragraf yetersiz", [
    { label: "Konum", value: "İlk <p> paragrafı" },
    { label: "Mevcut", value: firstP ? truncateText(firstP, 120) : "(boş veya çok kısa)" },
    { label: "Kelime sayısı", value: `${wordCount} kelime (sayfa geneli)` },
    { label: "Ne yapmalı", value: "Sayfa başında 60+ karakterlik net bir özet paragraf yazın" },
  ]);
}

export function evidenceGeoAiDisclosure(): string {
  return pageElementEvidence(
    "AI içerik bildirimi eksik",
    "Sayfa içeriği",
    "AI üretimi ifadesi var ama açıklama/bildirim yok",
    "Yapay zeka ile üretildiğini belirten görünür bir bildirim ekleyin",
  );
}

export function evidenceMetaCharsetMissing(): string {
  return pageElementEvidence(
    "Charset meta yok",
    "<head> → <meta charset>",
    "Bulunamadı",
    "<meta charset=\"utf-8\"> ekleyin",
  );
}

export function evidenceMetaKeywords(html: string): string {
  const kw = extractMetaKeywords(html);
  return buildIssueDetail("Meta keywords kullanılıyor (deprecated)", [
    { label: "Konum", value: "<meta name=\"keywords\">" },
    { label: "İçerik", value: kw ? truncateText(kw, 120) : "(okunamadı)" },
    { label: "Ne yapmalı", value: "Google bu etiketi dikkate almaz; kaldırabilirsiniz" },
  ]);
}

export function evidenceMixedContent(html: string): string {
  const urls = extractMixedContentUrls(html);
  const items = capItems(
    urls.map((u, i) => ({ label: `Kaynak #${i + 1}`, value: u, href: u })),
    urls.length,
  );
  return listEvidence(`${urls.length} HTTP (karışık) kaynak`, items, urls.length);
}

export function evidenceThinContent(wordCount: number, excerpt: string): string {
  return buildIssueDetail(`İnce içerik (${wordCount} kelime)`, [
    { label: "Konum", value: "<body> metin içeriği" },
    { label: "Kelime sayısı", value: String(wordCount) },
    { label: "Örnek", value: excerpt ? truncateText(excerpt, 150) : "(metin yok)" },
    { label: "Ne yapmalı", value: "Konuyu derinlemesine anlatan en az 300 kelimelik içerik ekleyin" },
  ]);
}

export function evidenceKeywordStuffing(word: string, count: number, title: string): string {
  return buildIssueDetail(`Title'da tekrarlayan kelime: "${word}" ×${count}`, [
    { label: "Konum", value: "<title>" },
    { label: "Title", value: truncateText(title, 120) },
    { label: "Tekrar", value: `"${word}" ${count} kez geçiyor` },
    { label: "Ne yapmalı", value: "Doğal bir başlık yazın; aynı kelimeyi 3+ kez tekrarlamayın" },
  ]);
}

export function evidenceHiddenText(html: string): string {
  const matches = extractHiddenTextMatches(html);
  const items = matches.map((m, i) => ({ label: `CSS #${i + 1}`, value: m }));
  return listEvidence("Gizli metin CSS şüphesi", items, matches.length);
}

export function evidenceInterstitial(html: string): string {
  const matches = extractInterstitialMatches(html);
  const items = matches.map((m, i) => ({ label: `Bileşen #${i + 1}`, value: m }));
  return listEvidence("Müdahaleci interstitial / popup", items, matches.length);
}

export function evidenceMaxImagePreviewMissing(): string {
  return pageElementEvidence(
    "max-image-preview:large yok",
    "<meta name=\"robots\"> veya HTTP X-Robots-Tag",
    "max-image-preview:large bulunamadı",
    "Büyük görsel önizleme için max-image-preview:large ekleyin",
  );
}

export function evidenceFaviconMissing(): string {
  return pageElementEvidence(
    "Favicon yok",
    "<head> → link rel=\"icon\" veya /favicon.ico",
    "Bulunamadı",
    "Site ikonu için favicon linki veya /favicon.ico ekleyin",
  );
}

export function evidenceTwitterCardMissing(): string {
  return pageElementEvidence(
    "Twitter card meta yok",
    "<meta name=\"twitter:card\">",
    "Bulunamadı",
    "twitter:card, twitter:title ve twitter:image ekleyin",
  );
}

export function evidenceOgImageSmall(ogImage: string): string {
  return buildIssueDetail("OG görseli küçük olabilir", [
    { label: "Konum", value: "og:image" },
    { label: "URL", value: ogImage, href: /^https?:\/\//i.test(ogImage) ? ogImage : undefined },
    { label: "Ne yapmalı", value: "En az 1200px genişlikte görsel kullanın (önerilen: 1200×630)" },
  ]);
}

export function evidenceUrlTooDeep(url: string, depth: number): string {
  const segments = pathSegments(url);
  return buildIssueDetail(`URL çok derin (${depth} seviye)`, [
    { label: "Sayfa", value: url },
    { label: "Yol parçaları", value: segments.join(" → ") || "/" },
    { label: "Ne yapmalı", value: "URL yapısını sadeleştirin; 3–4 seviyeden fazla kaçının" },
  ]);
}

export function evidenceVideoSchemaMissing(html: string): string {
  const items = extractVideoEmbeds(html);
  if (items.length === 0) {
    return pageElementEvidence(
      "Video var, schema yok",
      "<video> veya YouTube/Vimeo embed",
      "VideoObject JSON-LD bulunamadı",
      "Her video için VideoObject schema ekleyin",
    );
  }
  return listEvidence("Video embed — VideoObject schema eksik", items, items.length);
}

export function evidenceImageNoSrcset(html: string, total: number): string {
  const imgs = extractImagesWithoutSrcset(html);
  const items = capItems(
    imgs.map((img, i) => ({
      label: `Görsel #${i + 1}`,
      value: img.src,
      detail: "srcset / sizes ekleyin",
      href: /^https?:\/\//i.test(img.src) ? img.src : undefined,
    })),
    total,
  );
  return listEvidence(`${total} görselde srcset yok`, items, total);
}

export function evidenceProductSchemaMissing(url: string): string {
  return buildIssueDetail("Product schema eksik", [
    { label: "Sayfa", value: url },
    { label: "İpucu", value: "URL veya içerik ürün sayfası gibi görünüyor" },
    { label: "Ne yapmalı", value: "Product JSON-LD (name, image, offers) ekleyin" },
  ]);
}

export function evidenceBreadcrumbMissing(): string {
  return pageElementEvidence(
    "Breadcrumb yok",
    "HTML nav veya BreadcrumbList schema",
    "Bulunamadı",
    "Görsel breadcrumb ve BreadcrumbList JSON-LD ekleyin",
  );
}

export function evidenceSiteUrlList(headline: string, urls: string[], action: string): string {
  const items = capItems(
    urls.map((u, i) => ({ label: `Sayfa #${i + 1}`, value: u, href: u.startsWith("http") ? u : undefined })),
    urls.length,
  );
  if (action) items.push({ label: "Ne yapmalı", value: action });
  return listEvidence(headline, items, urls.length);
}

export function evidenceRedirectChain(hops: number, statuses: number[]): string {
  return buildIssueDetail(`${hops} yönlendirme zinciri`, [
    { label: "Konum", value: "HTTP yönlendirme zinciri" },
    { label: "Durum kodları", value: statuses.filter((s) => s >= 300 && s < 400).join(" → ") || statuses.join(" → ") },
    { label: "Ne yapmalı", value: "Tek 301/308 ile hedef URL'ye yönlendirin" },
  ]);
}

export function evidenceRedirectTemporary(statuses: number[]): string {
  return buildIssueDetail("Geçici yönlendirme (302/307)", [
    { label: "Zincir", value: statuses.filter((s) => s >= 300 && s < 400).join(" → ") },
    { label: "Ne yapmalı", value: "Kalıcı taşınma için 301/308 kullanın" },
  ]);
}

export function evidenceCanonicalTargetError(canonical: string, status: number): string {
  return buildIssueDetail(`Canonical hedefi hatalı (HTTP ${status || "hata"})`, [
    { label: "Canonical URL", value: canonical, href: /^https?:\/\//i.test(canonical) ? canonical : undefined },
    { label: "HTTP durumu", value: String(status || "erişilemedi") },
    { label: "Ne yapmalı", value: "200 dönen, doğru canonical URL kullanın" },
  ]);
}

export function evidenceGooglebotBlocked(status: number | null | undefined): string {
  return buildIssueDetail("Googlebot engellenmiş olabilir", [
    { label: "Googlebot testi", value: `HTTP ${status ?? "?"}` },
    { label: "Ne yapmalı", value: "robots.txt, firewall ve sunucu kurallarını kontrol edin" },
  ]);
}

export function evidencePlain(headline: string, items: EvidenceItem[]): string {
  return buildIssueDetail(headline, items);
}

export function evidenceSchemaIssue(schemaType: string, missingFields: string[], action: string): string {
  return buildIssueDetail(`${schemaType} schema eksik veya hatalı`, [
    { label: "Konum", value: `JSON-LD → @type: ${schemaType}` },
    { label: "Eksik / hatalı", value: missingFields.length ? missingFields.join(", ") : "Zorunlu alanlar" },
    { label: "Ne yapmalı", value: action },
  ]);
}

export function evidenceRobotsConflict(meta: string, header: string): string {
  return buildIssueDetail("Meta robots ile HTTP başlığı çelişiyor", [
    { label: "Meta robots", value: meta },
    { label: "X-Robots-Tag", value: header },
    { label: "Ne yapmalı", value: "Meta ve sunucu başlığını aynı index/noindex yönünde hizalayın" },
  ]);
}

export function evidenceRobotsDirective(label: string, value: string, action: string): string {
  return buildIssueDetail(label, [
    { label: "Konum", value: "Meta robots veya X-Robots-Tag" },
    { label: "Değer", value: value },
    { label: "Ne yapmalı", value: action },
  ]);
}

export function evidenceDataNosnippetInvalid(): string {
  return pageElementEvidence(
    "Geçersiz data-nosnippet kullanımı",
    "HTML elementleri",
    "data-nosnippet yalnızca span, div, section üzerinde olmalı",
    "Diğer etiketlerden kaldırın veya uygun etikete taşıyın",
  );
}

export function evidenceEcommerceFacet(search: string, url: string): string {
  return buildIssueDetail("Filtre/facet URL indekslenebilir", [
    { label: "Sayfa", value: url },
    { label: "Query", value: search || "(parametreler)" },
    { label: "Ne yapmalı", value: "Facet sayfalarına noindex ekleyin veya canonical kullanın" },
  ]);
}

export function evidenceEcommercePagination(url: string): string {
  return buildIssueDetail("Sayfalandırma rel=next/prev yok", [
    { label: "Sayfa", value: url },
    { label: "Konum", value: "<head> → link rel=\"next/prev\"" },
    { label: "Ne yapmalı", value: "Sayfalandırılmış serilerde next/prev linkleri ekleyin" },
  ]);
}

export function evidenceSpamSuspiciousHost(hostname: string, src: string): string {
  return buildIssueDetail(`Şüpheli harici kaynak: ${hostname}`, [
    { label: "Konum", value: "<script> veya <iframe>" },
    { label: "Kaynak", value: src, href: src.startsWith("http") ? src : undefined },
    { label: "Ne yapmalı", value: "Güvenilir olmayan reklam/tracker kaynaklarını kaldırın" },
  ]);
}

export function evidenceSpamUgcLink(href: string): string {
  return buildIssueDetail("UGC dış link nofollow değil", [
    { label: "Konum", value: "Yorum/forum/UGC bloğu" },
    { label: "Link", value: href, href: href.startsWith("http") ? href : undefined },
    { label: "Ne yapmalı", value: "rel=\"nofollow ugc\" ekleyin" },
  ]);
}

export function evidenceSpamKeyword(word: string, count: number, totalWords: number): string {
  return buildIssueDetail(`Aşırı kelime tekrarı: "${word}"`, [
    { label: "Konum", value: "Sayfa metin içeriği" },
    { label: "Tekrar", value: `"${word}" ${count} kez (${totalWords} kelime içinde)` },
    { label: "Ne yapmalı", value: "Doğal dil kullanın; aynı kelimeyi spam yapmayın" },
  ]);
}

export function evidenceDoorwayTitles(pairs: number, examples: { url: string; title: string }[]): string {
  const items = examples.slice(0, 5).map((e, i) => ({
    label: `Örnek #${i + 1}`,
    value: truncateText(e.title, 80),
    detail: e.url,
    href: e.url.startsWith("http") ? e.url : undefined,
  }));
  return listEvidence(`${pairs} benzer başlık çifti`, items, pairs);
}

export function evidenceHreflangGraph(missingReturn: number, examples: string[]): string {
  const items = examples.map((ex, i) => ({ label: `Örnek #${i + 1}`, value: ex }));
  return listEvidence(`${missingReturn} karşılıksız hreflang`, items, missingReturn);
}

export function evidenceCctldHint(host: string): string {
  return buildIssueDetail("ccTLD + hreflang ipucu", [
    { label: "Alan adı", value: host },
    { label: "Ne yapmalı", value: "Uluslararası hedefleme için hreflang ve Search Console ayarlarını doğrulayın" },
  ]);
}

export function evidenceAmpError(ampUrl: string, status?: number): string {
  return buildIssueDetail(`AMP sayfası erişilemiyor${status ? ` (HTTP ${status})` : ""}`, [
    { label: "Konum", value: "<link rel=\"amphtml\">" },
    { label: "AMP URL", value: ampUrl, href: ampUrl.startsWith("http") ? ampUrl : undefined },
    { label: "Ne yapmalı", value: "AMP URL'nin 200 döndüğünden emin olun veya amphtml linkini kaldırın" },
  ]);
}

export function evidenceDuplicateTitle(title: string, urls: string[]): string {
  const items = urls.map((u, i) => ({ label: `Sayfa #${i + 1}`, value: u, href: u.startsWith("http") ? u : undefined }));
  return listEvidence(`Aynı title: "${truncateText(title, 60)}"`, items, urls.length);
}

export function evidenceDuplicateDescription(desc: string, urls: string[]): string {
  const items = urls.map((u, i) => ({ label: `Sayfa #${i + 1}`, value: u, href: u.startsWith("http") ? u : undefined }));
  return listEvidence(`Aynı description: "${truncateText(desc, 50)}"`, items, urls.length);
}

export function evidenceOrphanPage(url: string): string {
  return buildIssueDetail("Yetim sayfa (iç link yok)", [
    { label: "Sayfa", value: url, href: url.startsWith("http") ? url : undefined },
    { label: "Ne yapmalı", value: "Site içi navigasyon veya ilgili sayfalardan link verin" },
  ]);
}

export function evidenceSitemapGap(missing: number, total: number, sampleUrls: string[]): string {
  const items = sampleUrls.map((u, i) => ({ label: `Eksik #${i + 1}`, value: u }));
  return listEvidence(`${missing}/${total} sitemap URL taranmadı`, items, missing);
}

export function evidenceRobotsBlocksAll(): string {
  return pageElementEvidence(
    "robots.txt tüm siteyi engelliyor",
    "/robots.txt → Disallow: /",
    "Tüm site crawl dışı",
    "Disallow: / kuralını kaldırın veya daraltın",
  );
}

export function evidenceRobotsTxtMissing(origin: string): string {
  return buildIssueDetail("robots.txt bulunamadı", [
    { label: "URL", value: `${origin}/robots.txt` },
    { label: "Ne yapmalı", value: "Geçerli bir robots.txt oluşturun" },
  ]);
}

export function evidenceSitemapMissing(origin: string): string {
  return buildIssueDetail("Sitemap bulunamadı", [
    { label: "Konum", value: "robots.txt Sitemap: veya /sitemap.xml" },
    { label: "Site", value: origin },
    { label: "Ne yapmalı", value: "XML sitemap oluşturup robots.txt'e ekleyin" },
  ]);
}

export function evidenceRobotsBlocksCssJs(rules: string[]): string {
  const items = rules.map((r, i) => ({ label: `Kural #${i + 1}`, value: r }));
  return listEvidence("CSS/JS robots.txt ile engellenmiş", items, rules.length);
}

export function evidenceRobotsSyntax(warnings: string[]): string {
  const items = warnings.map((w, i) => ({ label: `Uyarı #${i + 1}`, value: w }));
  return listEvidence("robots.txt sözdizimi uyarısı", items, warnings.length);
}

export function evidenceSitemapInvalid(detail: string): string {
  return buildIssueDetail("Sitemap XML geçersiz", [
    { label: "Konum", value: "sitemap.xml" },
    { label: "Detay", value: detail },
    { label: "Ne yapmalı", value: "XML yapısını düzeltin; URL'lerin erişilebilir olduğundan emin olun" },
  ]);
}

export function evidenceSitemapLastmod(count: number): string {
  return buildIssueDetail(`${count} geçersiz lastmod`, [
    { label: "Konum", value: "sitemap.xml → <lastmod>" },
    { label: "Adet", value: String(count) },
    { label: "Ne yapmalı", value: "ISO 8601 tarih formatı kullanın (YYYY-MM-DD)" },
  ]);
}

export function evidenceSitemapUnreachable(unreachable: number, sampled: number): string {
  return buildIssueDetail(`${unreachable}/${sampled} sitemap URL erişilemedi`, [
    { label: "Konum", value: "sitemap.xml → <loc>" },
    { label: "Oran", value: `${unreachable} / ${sampled} örnek URL` },
    { label: "Ne yapmalı", value: "404/5xx dönen URL'leri sitemap'ten çıkarın veya düzeltin" },
  ]);
}

export function evidenceJsTitleMissingStatic(renderedTitle: string): string {
  return buildIssueDetail("Statik HTML'de title yok (JS ile geliyor)", [
    { label: "Statik", value: "(title yok)" },
    { label: "Render sonrası", value: renderedTitle },
    { label: "Ne yapmalı", value: "SSR veya pre-render ile title'ı ilk HTML'de sunun" },
  ]);
}

export function evidenceJsTitleChanged(staticTitle: string, renderedTitle: string): string {
  return buildIssueDetail("Title JS ile değişiyor", [
    { label: "Statik title", value: staticTitle },
    { label: "Render sonrası", value: renderedTitle },
    { label: "Ne yapmalı", value: "Googlebot için tutarlı title sağlayın (SSR)" },
  ]);
}

export function evidenceJsH1MissingStatic(renderedH1: number): string {
  return buildIssueDetail(`Statik HTML'de H1 yok (${renderedH1} H1 render sonrası)`, [
    { label: "Konum", value: "<body> → <h1>" },
    { label: "Ne yapmalı", value: "Ana başlığı SSR ile ilk HTML'de gönderin" },
  ]);
}

export function evidenceJsSoft404(staticLabel: string, renderedLabel?: string): string {
  return buildIssueDetail("Soft 404 şüphesi (JS)", [
    { label: "Statik içerik", value: staticLabel },
    ...(renderedLabel ? [{ label: "Render sonrası", value: renderedLabel }] : []),
    { label: "Ne yapmalı", value: "404 sayfalarında HTTP 404 döndürün; gerçek içerikle karıştırmayın" },
  ]);
}

export function evidenceJsLazyContent(staticWords: number, renderedWords: number): string {
  return buildIssueDetail("JS ile yüklenen içerik", [
    { label: "Statik kelime", value: String(staticWords) },
    { label: "Render sonrası", value: String(renderedWords) },
    { label: "Ne yapmalı", value: "Önemli içeriği ilk HTML'de sunun veya SSR kullanın" },
  ]);
}

export function evidenceJsCloakingScore(titleDiff: boolean, h1Diff: boolean, wordDeltaPct: number): string {
  return buildIssueDetail("Statik vs render farkı yüksek", [
    { label: "Title farkı", value: titleDiff ? "Evet" : "Hayır" },
    { label: "H1 farkı", value: h1Diff ? "Evet" : "Hayır" },
    { label: "Kelime farkı", value: `%${wordDeltaPct}` },
    { label: "Ne yapmalı", value: "Googlebot'a gösterilen içerik kullanıcı içeriğiyle aynı olmalı" },
  ]);
}
