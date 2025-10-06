# Azure SQL Database Cost Monitoring System

This monitoring system alerts you when your Azure SQL Database Basic tier is approaching limits and needs to be upgraded.

## 🎯 What It Monitors

- **Storage Usage**: Alerts when approaching 2GB limit
- **DTU Usage**: Monitors when hitting 5 DTU capacity 
- **Cost Impact**: Shows upgrade costs when limits are reached

## 📊 Current Status

Based on your last check:
- **Tier**: Basic (5 DTU, 2GB storage)
- **Monthly Cost**: ~$5
- **Status**: ⚠️ **Both DTU and Storage at maximum limits**

## 🚨 Alert Triggers

The system will recommend upgrades when:
- Storage usage > 80% (1.6GB+)
- DTU consistently at maximum
- Connection limits being hit

## 💰 Upgrade Cost Summary

| Tier | DTU/vCore | Storage | Monthly Cost | Annual Increase |
|------|-----------|---------|--------------|-----------------|
| **Basic (Current)** | 5 DTU | 2GB | $5 | - |
| **Standard S0** | 10 DTU | 250GB | $15 | +$120/year |
| **Standard S1** | 20 DTU | 250GB | $30 | +$300/year |
| **General Purpose 2vCore** | 2 vCore | 32GB | $60 | +$660/year |

## 🔧 How to Use

### Check Current Status
```powershell
.\scripts\azure-sql-monitoring.ps1 -CheckLimits
```

### See Upgrade Recommendations  
```powershell
.\scripts\azure-sql-monitoring.ps1 -RecommendUpgrade
```

### Set Up Automated Monitoring
```powershell
# Run daily checks
.\scripts\azure-sql-scheduler.ps1 -SetupSchedule

# Manual check
.\scripts\azure-sql-scheduler.ps1 -RunOnce
```

## 📁 Files Created

- `sql-monitoring-results.json` - Latest usage data
- `sql-monitoring.log` - Historical monitoring logs  
- `sql-upgrade-alert.txt` - Last alert details

## 🎯 Recommended Action

**Immediate**: Your database is at both DTU and storage limits. Consider upgrading to **Standard S0** for:
- 10x DTU capacity (5 → 10 DTU)
- 125x storage capacity (2GB → 250GB) 
- Only **+$10/month** cost increase

## 📈 When to Upgrade

**Upgrade to Standard S0 when**:
- App response times slow down
- Getting storage warnings
- Planning to add more features
- Need better performance SLA

**Stay on Basic if**:
- Just development/testing
- Very light usage
- Cost is primary concern
- Current performance acceptable

## 🔔 Setting Up Email Alerts

To receive email notifications:
1. Run the monitoring setup
2. Configure Azure Action Groups (manual step)
3. Add your email to action group

```powershell
# After running -SetupAlerts, complete with:
az monitor action-group create \
  --resource-group WileyWidget-RG \
  --name sql-upgrade-alerts \
  --short-name SQLUpgrade \
  --action email your-email@domain.com youremail
```

---

**Last Updated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Next Recommended Check**: Daily during development phase