# Backup & Restore Strategy

Production veri kaybı senaryoları ve bunlara karşı strateji.

## Risk Matrisi

| Senaryo | Önlem | RPO* | RTO** |
|---|---|---|---|
| App crash | Otomatik restart (k8s/systemd) | 0 | < 1dk |
| Veritabanı corruption | DB backup + WAL/transaction log | < 1dk | 1-2 saat |
| Yanlışlıkla DELETE | Point-in-time restore (PITR) | < 1dk | 1-2 saat |
| Bölgesel outage | Cross-region replica | < 5dk | < 30dk |
| Ransomware / kötü niyetli | Immutable backups (object-lock) | < 1 gün | 4+ saat |

\* RPO: Recovery Point Objective — ne kadar veri kaybı kabul edilebilir
\** RTO: Recovery Time Objective — ne kadar süre downtime kabul edilebilir

## Ne Yedeklenmeli?

### 1. Veritabanı (kritik)

Tüm uygulama state'i burada — kullanıcılar, oturumlar, audit log, outbox, inbox, themes.

**SQL Server için:**
- **Full backup**: günlük (gece, off-peak)
- **Differential**: 6 saatte bir
- **Transaction log**: 15 dakikada bir (PITR için)
- **Retention**: 30 gün (daha eski → archive storage)

```bash
# Full backup örneği
sqlcmd -S sql -U sa -P "$SA_PASSWORD" -Q "
BACKUP DATABASE [SearchConsoleApp]
TO DISK = '/backups/SearchConsoleApp-full-$(date +%Y%m%d).bak'
WITH COMPRESSION, INIT, CHECKSUM"
```

**PostgreSQL geçişi yapılırsa:**
- `pg_dump` günlük + WAL archiving (continuous)
- Managed: Aurora PostgreSQL, Cloud SQL PITR built-in

### 2. Blob storage (önemli)

Kullanıcı dosyaları, exported reports vb.

- **S3**: bucket versioning + lifecycle (90 gün eski versiyonları arşive)
- **Cross-region replication**: kritik bucket'lar için
- **MFA delete**: aksidental silinmeye karşı (object-lock)

**Local file storage** (`LocalFileBlobStorage`):
- Production'da KULLANMA. Pod restart'ta dosya kaybolabilir.
- Mecbursan: NFS/EFS mount + tar.gz günlük backup

### 3. Konfigürasyon (önemli)

- appsettings.Production.json (secret yok, ama config kritik)
- K8s manifest / Helm chart / Terraform — git'te (infra-as-code)
- Secret'lar Vault/Secrets Manager'da — backup oraya da uygulanır

### 4. Redis (önemli ama kayıp tolere edilir)

Cache + SignalR backplane + rate limit state — kaybı uygulamayı durdurmaz.

- AOF (Append-Only File) açık olsun (`redis-server --appendonly yes` — docker-compose'da var)
- Snapshot (RDB) saatte bir, 24 saat retention
- Cross-instance: Redis Sentinel veya Cluster

### 5. Audit log archive

`AuditLogArchive` tablosu hukuki kayıt — DB backup zaten kapsar ama:
- Ekstra önlem: aylık archive tablosunu Parquet'e export → S3 cold storage
- Compliance: GDPR/KVKK için 2+ yıl saklama tipik

## Backup Storage Stratejisi

**3-2-1 kuralı:**
- **3** kopya (production + 2 backup)
- **2** farklı medium (DB + S3 / local + cloud)
- **1** off-site (farklı bölge/datacenter)

```
Production DB ──┐
                ├──▶ Same-region backup (hızlı restore)
                └──▶ Cross-region backup (DR)
                     └──▶ Glacier / archive (90+ gün, ucuz)
```

## Restore Senaryoları

### Senaryo 1: Yanlışlıkla silinen veri (en yaygın)

```bash
# Point-in-time restore — bir saat öncesine git
sqlcmd -S sql-restore -U sa -P "$SA_PASSWORD" -Q "
RESTORE DATABASE [SearchConsoleApp_Restore]
FROM DISK = '/backups/SearchConsoleApp-full-20241201.bak'
WITH NORECOVERY, MOVE 'SearchConsoleApp_Data' TO '/var/opt/mssql/data/SearchConsoleApp_Restore.mdf';

RESTORE DATABASE [SearchConsoleApp_Restore]
FROM DISK = '/backups/SearchConsoleApp-diff-20241201-1800.bak'
WITH NORECOVERY;

RESTORE LOG [SearchConsoleApp_Restore]
FROM DISK = '/backups/SearchConsoleApp-log-20241201-1815.trn'
WITH STOPAT = '2024-12-01T18:13:00', RECOVERY;
"

# Silinen kaydı export et, original DB'ye yeniden ekle:
sqlcmd -Q "INSERT INTO SearchConsoleApp.dbo.Customer SELECT * FROM SearchConsoleApp_Restore.dbo.Customer WHERE ..."
```

### Senaryo 2: Tam DB kaybı (disk failure)

1. Yeni instance ayağa kaldır (k8s pod / yeni VM)
2. En son full + diff + log backup'ları sırayla restore
3. Application restart — DB connection string aynı kalırsa otomatik
4. **Önemli**: outbox mesajları yeniden işlenir, **inbox idempotency** dış event'lerin tekrarını engeller

### Senaryo 3: Bölgesel outage

DR plan: standby region'da read-replica zaten var.
- DNS swap (Route53/CloudFlare) → secondary region
- Read-replica'yı primary'e promote et
- Application primary connection string yeni endpoint'e

RTO < 30dk gerekiyorsa: active-active multi-region (daha pahalı, daha karmaşık — distributed transaction lazım).

## Backup Doğrulama (kritik!)

**"Backup'ın tek anlamı restore'dır."**

- Aylık DR drill: backup'tan restore et, app çalışıyor mu doğrula
- Otomatik integrity check: `RESTORE VERIFYONLY` her gün
- Sınama: random tabloda satır sayısı + checksum karşılaştır

Backup yapan ama restore test etmeyen ekipler felaket anında "backup corrupt" diyor — bu en kötü senaryo.

## Outbox/Inbox Restore Notları

**Outbox**:
- Restore sonrası `pending` mesajlar otomatik işlenir (dispatcher başlayınca)
- `in_progress` claim timeout (5dk) sonra `pending`'e dönüyor
- **Sorun**: bir mesaj zaten gönderilmiş olabilir, restore sonrası tekrar gönderilir
- **Çözüm**: receiver tarafının idempotency yapması (X-Webhook-Event-Id)

**Inbox**:
- Restore sonrası `received` ama `processed` olmamış mesajlar var
- Bunları manuel re-process et (admin endpoint eklenebilir)
- Veya: receiver durumdan haberdar değil — provider retry yapacak, idempotent kayıt zaten unique constraint ile korunur

## Otomatik Backup Job (örnek systemd timer)

```ini
# /etc/systemd/system/SearchConsoleApp-backup.service
[Unit]
Description=SearchConsoleApp DB Backup
[Service]
Type=oneshot
ExecStart=/usr/local/bin/SearchConsoleApp-backup.sh

# /etc/systemd/system/SearchConsoleApp-backup.timer
[Unit]
Description=Run SearchConsoleApp backup hourly
[Timer]
OnCalendar=hourly
Persistent=true
[Install]
WantedBy=timers.target
```

```bash
# /usr/local/bin/SearchConsoleApp-backup.sh
#!/bin/bash
set -euo pipefail
DATE=$(date +%Y%m%d-%H%M)
BACKUP_DIR="/backups"

# Differential daily, full weekly
DAY_OF_WEEK=$(date +%u)
if [[ $DAY_OF_WEEK == 7 ]]; then
  BACKUP_TYPE="full"
  CMD="BACKUP DATABASE [SearchConsoleApp] TO DISK = '$BACKUP_DIR/full-$DATE.bak' WITH COMPRESSION, CHECKSUM"
else
  BACKUP_TYPE="diff"
  CMD="BACKUP DATABASE [SearchConsoleApp] TO DISK = '$BACKUP_DIR/diff-$DATE.bak' WITH DIFFERENTIAL, COMPRESSION, CHECKSUM"
fi

sqlcmd -S sql -U sa -P "$SA_PASSWORD" -Q "$CMD"

# S3'e yükle
aws s3 cp "$BACKUP_DIR/$BACKUP_TYPE-$DATE.bak" "s3://SearchConsoleApp-backups/$BACKUP_TYPE/" \
  --storage-class STANDARD_IA

# 30 günden eski local backup sil
find "$BACKUP_DIR" -name "*.bak" -mtime +30 -delete

# Slack/PagerDuty notification (başarı/hata)
if [[ $? -eq 0 ]]; then
  curl -X POST "$SLACK_WEBHOOK" -d "{\"text\":\"✓ SearchConsoleApp $BACKUP_TYPE backup OK ($DATE)\"}"
else
  curl -X POST "$PAGERDUTY_WEBHOOK" -d "{\"event\":\"backup_failure\"}"
fi
```

## Managed Service Önerileri

DIY backup yönetmek istemiyorsan:

- **AWS RDS / Aurora**: automated backups + PITR built-in
- **Azure SQL Database**: Long-term retention + geo-replication
- **Google Cloud SQL**: automatic backups + on-demand snapshots

Managed servisler 7-35 gün PITR default, daha uzun retention için archive tier.
