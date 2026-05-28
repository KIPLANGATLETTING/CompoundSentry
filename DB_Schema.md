# Compound Sentry Analytics Database Schema

## Overview
This schema is designed for a Python FastAPI analytics system that tracks and analyzes device presence patterns, network activity, and user engagement from the Compound Sentry monitoring system.

## Database Design Principles
- **Time-Series Optimized**: Heavy use of timestamps and indexes for fast time-range queries
- **Aggregated Data**: Pre-calculated daily/hourly summaries for fast reporting
- **Normalized Structure**: Separate tables for efficient querying and analytics
- **Partition-Ready**: Design supports future partitioning by date for massive datasets

---

## Core Tables

### 1. **devices** (Master Device Registry)
Maintains a complete registry of all devices seen on the network.

```sql
CREATE TABLE devices (
    device_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mac_address VARCHAR(17) NOT NULL UNIQUE,
    first_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    device_name VARCHAR(255),
    device_type VARCHAR(50),  -- 'phone', 'laptop', 'tablet', 'unknown'
    vendor_name VARCHAR(255),  -- OUI lookup: Apple, Samsung, etc.
    os_type VARCHAR(100),  -- iOS, Android, Windows, macOS, Linux
    is_active BOOLEAN DEFAULT TRUE,
    total_connections INT DEFAULT 0,  -- cumulative count
    avg_signal_strength INT,  -- average signal strength (-dBm)
    
    -- Tracking flags
    is_personal_device BOOLEAN DEFAULT FALSE,
    device_label VARCHAR(255),  -- custom label (e.g., "John's iPhone")
    
    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_mac_address (mac_address),
    INDEX idx_last_seen_at (last_seen_at),
    INDEX idx_device_type (device_type),
    INDEX idx_is_active (is_active)
);
```

### 2. **device_events** (Raw Event Stream)
Stores all device connection/disconnection events. Can be partitioned by date for large deployments.

```sql
CREATE TABLE device_events (
    event_id BIGSERIAL PRIMARY KEY,
    device_id UUID NOT NULL,
    mac_address VARCHAR(17) NOT NULL,
    event_type VARCHAR(20) NOT NULL,  -- 'CONNECTED', 'DISCONNECTED', 'SIGNAL_CHANGE'
    event_timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    signal_strength INT,  -- RSSI value in dBm (-30 to -90)
    duration_seconds INT,  -- only for DISCONNECTED events
    location_zone VARCHAR(100),  -- optional: zone/room identifier
    
    -- Metadata
    ip_address INET,
    hostname VARCHAR(255),
    
    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    INDEX idx_device_id_timestamp (device_id, event_timestamp DESC),
    INDEX idx_mac_address_timestamp (mac_address, event_timestamp DESC),
    INDEX idx_event_timestamp (event_timestamp DESC),
    INDEX idx_event_type (event_type),
    PARTITION BY RANGE (YEAR(event_timestamp), MONTH(event_timestamp))  -- ready for partitioning
);
```

### 3. **daily_device_activity** (Aggregated Daily Stats)
Pre-calculated daily aggregates for fast dashboard queries. Calculated via scheduled job.

```sql
CREATE TABLE daily_device_activity (
    activity_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL,
    date DATE NOT NULL,
    
    -- Presence metrics
    connection_count INT DEFAULT 0,  -- how many times device connected
    disconnection_count INT DEFAULT 0,
    total_presence_seconds INT DEFAULT 0,  -- total time on network
    
    -- Signal metrics
    avg_signal_strength INT,
    min_signal_strength INT,
    max_signal_strength INT,
    signal_samples INT,  -- count of samples for averaging
    
    -- Time metrics
    first_connection_time TIME,  -- first connection time of the day
    last_disconnection_time TIME,  -- last disconnection time of the day
    presence_percentage INT,  -- 0-100, percent of day device was present
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    UNIQUE KEY unique_device_date (device_id, date),
    INDEX idx_date (date),
    INDEX idx_device_id_date (device_id, date DESC)
);
```

### 4. **hourly_device_activity** (Hourly Aggregates)
Hourly aggregates for trend analysis and detailed time-series charts.

```sql
CREATE TABLE hourly_device_activity (
    activity_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL,
    hour_start TIMESTAMP NOT NULL,  -- start of hour (HH:00:00)
    
    -- Presence metrics
    connection_count INT DEFAULT 0,
    disconnection_count INT DEFAULT 0,
    total_presence_seconds INT DEFAULT 0,
    
    -- Signal metrics
    avg_signal_strength INT,
    min_signal_strength INT,
    max_signal_strength INT,
    
    -- Status
    device_status VARCHAR(20),  -- 'online', 'offline', 'mixed'
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    UNIQUE KEY unique_device_hour (device_id, hour_start),
    INDEX idx_hour_start (hour_start DESC),
    INDEX idx_device_id_hour (device_id, hour_start DESC)
);
```

### 5. **network_summary** (Network-Wide Statistics)
Aggregated network statistics across all devices.

```sql
CREATE TABLE network_summary (
    summary_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    date DATE NOT NULL UNIQUE,
    
    -- Device counts
    total_unique_devices INT DEFAULT 0,
    active_devices_today INT DEFAULT 0,
    new_devices_count INT DEFAULT 0,
    offline_devices_count INT DEFAULT 0,
    
    -- Activity metrics
    total_events INT DEFAULT 0,
    connection_events INT DEFAULT 0,
    disconnection_events INT DEFAULT 0,
    
    -- Presence metrics
    avg_connected_devices INT,  -- average number of devices online at any time
    peak_connected_devices INT,  -- maximum simultaneous devices
    peak_time TIME,  -- time when peak was reached
    
    -- Signal metrics
    avg_signal_strength INT,
    
    -- Time metrics
    busiest_hour INT,  -- hour with most activity (0-23)
    quietest_hour INT,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_date (date DESC)
);
```

### 6. **device_sessions** (Connection Sessions)
High-level view of device sessions (from connection to disconnection).

```sql
CREATE TABLE device_sessions (
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL,
    mac_address VARCHAR(17) NOT NULL,
    
    -- Session timing
    session_start TIMESTAMP NOT NULL,
    session_end TIMESTAMP,  -- NULL if device is currently connected
    duration_seconds INT,  -- calculated: session_end - session_start
    is_active BOOLEAN DEFAULT TRUE,
    
    -- Quality metrics
    avg_signal_strength INT,
    min_signal_strength INT,
    max_signal_strength INT,
    disconnections_during_session INT DEFAULT 0,  -- count of brief disconnects
    
    -- Context
    date_started DATE NOT NULL,  -- for partitioning/filtering
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    INDEX idx_device_id_start (device_id, session_start DESC),
    INDEX idx_session_start (session_start DESC),
    INDEX idx_is_active (is_active),
    INDEX idx_date_started (date_started DESC)
);
```

### 7. **device_presence_timeline** (Compact Presence History)
Stores only state changes to minimize storage while maintaining history.

```sql
CREATE TABLE device_presence_timeline (
    timeline_id BIGSERIAL PRIMARY KEY,
    device_id UUID NOT NULL,
    mac_address VARCHAR(17) NOT NULL,
    
    -- State tracking
    status VARCHAR(20) NOT NULL,  -- 'online' or 'offline'
    status_changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Context
    reason VARCHAR(100),  -- 'disconnection', 'timeout', 'signal_loss', etc.
    location_zone VARCHAR(100),
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    INDEX idx_device_id_timestamp (device_id, status_changed_at DESC),
    INDEX idx_status_changed_at (status_changed_at DESC)
);
```

### 8. **admin_users** (User Management)
Mirrors AdminUser from Compound_Sentry for reference and analytics.

```sql
CREATE TABLE admin_users (
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    full_name VARCHAR(100),
    
    -- Status tracking
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL,
    last_login_at TIMESTAMP,
    
    INDEX idx_email (email),
    INDEX idx_is_active (is_active)
);
```

### 9. **dashboard_activity** (User Engagement Analytics)
Tracks user interactions with the analytics dashboard.

```sql
CREATE TABLE dashboard_activity (
    activity_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    
    -- Navigation
    page_viewed VARCHAR(100),  -- 'dashboard', 'devices', 'analytics', 'settings'
    action_type VARCHAR(100),  -- 'view', 'export', 'filter', 'report_generated'
    
    -- Context
    viewed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    duration_seconds INT,  -- how long they stayed on page
    
    -- Session
    session_id VARCHAR(255),
    ip_address INET,
    user_agent VARCHAR(500),  -- browser info
    
    -- Metadata
    query_filters JSONB,  -- stored filters used
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (user_id) REFERENCES admin_users(user_id) ON DELETE CASCADE,
    INDEX idx_user_id_viewed_at (user_id, viewed_at DESC),
    INDEX idx_viewed_at (viewed_at DESC),
    INDEX idx_page_viewed (page_viewed),
    PARTITION BY RANGE (YEAR(viewed_at), MONTH(viewed_at))
);
```

### 10. **device_stats_weekly** (Weekly Rollup)
Lower resolution aggregates for long-term trending (90+ days).

```sql
CREATE TABLE device_stats_weekly (
    stats_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL,
    week_start DATE NOT NULL,  -- Monday of that week
    
    -- Aggregates
    total_connections INT,
    total_disconnections INT,
    total_presence_hours INT,
    avg_daily_presence_percentage INT,
    
    -- Signal
    avg_signal_strength INT,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE,
    UNIQUE KEY unique_device_week (device_id, week_start),
    INDEX idx_week_start (week_start DESC)
);
```

### 11. **alerts** (Anomaly & Event Alerts)
Stores system alerts and anomalies detected in the network.

```sql
CREATE TABLE alerts (
    alert_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID,
    mac_address VARCHAR(17),
    
    -- Alert details
    alert_type VARCHAR(100),  -- 'unusual_time', 'poor_signal', 'frequent_disconnects', 'new_device'
    severity VARCHAR(20),  -- 'info', 'warning', 'critical'
    title VARCHAR(255) NOT NULL,
    description TEXT,
    
    -- Status
    is_acknowledged BOOLEAN DEFAULT FALSE,
    acknowledged_by UUID,
    acknowledged_at TIMESTAMP,
    
    -- Timing
    triggered_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE SET NULL,
    FOREIGN KEY (acknowledged_by) REFERENCES admin_users(user_id) ON DELETE SET NULL,
    INDEX idx_triggered_at (triggered_at DESC),
    INDEX idx_device_id_triggered_at (device_id, triggered_at DESC),
    INDEX idx_severity (severity),
    INDEX idx_is_acknowledged (is_acknowledged)
);
```

---

## Indexes & Performance Optimization

### Critical Indexes for Fast Queries
```sql
-- Time-series queries (most common)
CREATE INDEX idx_device_id_timestamp ON device_events(device_id, event_timestamp DESC);
CREATE INDEX idx_mac_address_timestamp ON device_events(mac_address, event_timestamp DESC);
CREATE INDEX idx_event_timestamp ON device_events(event_timestamp DESC);

-- Aggregation queries
CREATE INDEX idx_device_id_date ON daily_device_activity(device_id, date DESC);
CREATE INDEX idx_date ON daily_device_activity(date DESC);

-- Dashboard queries
CREATE INDEX idx_last_seen_at ON devices(last_seen_at DESC);
CREATE INDEX idx_is_active ON devices(is_active);
```

---

## Data Retention Policy

```
device_events:
  - Retain: 90 days (hot storage)
  - Archive: After 90 days to cold storage
  - Partition by: MONTH

dashboard_activity:
  - Retain: 180 days
  - Purge: After 180 days

daily_device_activity:
  - Retain: 5 years (permanent)

hourly_device_activity:
  - Retain: 90 days
  - Archive: After 90 days

network_summary:
  - Retain: Permanent

alerts:
  - Retain: 1 year
  - Auto-purge acknowledged alerts after 90 days
```

---

## Key Metrics & KPIs

### Device-Level Analytics
- **Uptime %**: (total_presence_seconds / (24 * 3600)) * 100
- **Connection Frequency**: connections_per_day / active_days
- **Signal Quality**: avg_signal_strength (target: > -70 dBm)
- **Session Duration**: average length of continuous connection

### Network-Level Analytics
- **Device Coverage**: unique_devices_seen / expected_devices
- **Network Health**: (devices_online / total_devices) * 100
- **Peak Hours**: time with most simultaneous connections
- **Activity Patterns**: day-of-week and hour-of-day trends

### User Engagement
- **Dashboard Usage**: page_views per user per day
- **Data Export**: export frequency and volume
- **Average Session Duration**: time spent viewing analytics

---

## Recommended FastAPI Endpoints

```
# Device Analytics
GET /api/analytics/devices/{device_id}/overview
GET /api/analytics/devices/{device_id}/timeline?from=&to=
GET /api/analytics/devices/{device_id}/sessions
GET /api/analytics/devices/{device_id}/weekly-stats

# Network Analytics
GET /api/analytics/network/summary?date=
GET /api/analytics/network/heatmap?date_range=week|month
GET /api/analytics/network/peak-times
GET /api/analytics/network/device-locations

# Reports
GET /api/analytics/reports/daily
GET /api/analytics/reports/weekly
GET /api/analytics/reports/monthly
POST /api/analytics/reports/custom

# Alerts
GET /api/analytics/alerts?severity=&acknowledged=
POST /api/analytics/alerts/{alert_id}/acknowledge

# Dashboard
GET /api/dashboard/quick-stats
GET /api/dashboard/device-activity-feed
```

---

## Technology Recommendations

### Database
- **PostgreSQL 14+** (TimescaleDB extension for hyper-optimized time-series)
  - Alternative: ClickHouse for massive-scale analytics
- **Redis** for caching aggregates and hot data

### Python Stack
- **FastAPI** for REST API
- **SQLAlchemy** for ORM
- **Pydantic** for data validation
- **Celery** for scheduled aggregation jobs
- **APScheduler** for periodic tasks (hourly/daily rollups)

### Libraries
```
fastapi==0.104.1
sqlalchemy==2.0+
psycopg2-binary==2.9+  # PostgreSQL driver
pydantic==2.0+
python-dateutil==2.8+
pandas==2.0+  # For advanced analytics
```

---

## Migration Path from Current System

1. **Phase 1**: Create analytics DB with `devices` and `device_events` tables
2. **Phase 2**: ETL raw events from Compound_Sentry → device_events table
3. **Phase 3**: Implement aggregation jobs (daily, hourly, weekly)
4. **Phase 4**: Build FastAPI endpoints with caching
5. **Phase 5**: Add dashboard activity tracking
6. **Phase 6**: Implement alert rules and anomaly detection

