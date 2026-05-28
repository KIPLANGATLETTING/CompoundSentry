"""
FastAPI Models and Schemas for Compound Sentry Analytics
Corresponds to the PostgreSQL database schema
"""

from datetime import datetime, date, time
from typing import Optional, List, Dict, Any
from uuid import UUID
from pydantic import BaseModel, Field
from enum import Enum


# ============================================================================
# ENUMS
# ============================================================================

class DeviceTypeEnum(str, Enum):
    """Device type classification"""
    PHONE = "phone"
    LAPTOP = "laptop"
    TABLET = "tablet"
    DESKTOP = "desktop"
    IOT = "iot"
    UNKNOWN = "unknown"


class EventTypeEnum(str, Enum):
    """Device event types"""
    CONNECTED = "CONNECTED"
    DISCONNECTED = "DISCONNECTED"
    SIGNAL_CHANGE = "SIGNAL_CHANGE"


class AlertTypeEnum(str, Enum):
    """Alert classification"""
    UNUSUAL_TIME = "unusual_time"
    POOR_SIGNAL = "poor_signal"
    FREQUENT_DISCONNECTS = "frequent_disconnects"
    NEW_DEVICE = "new_device"
    OFFLINE_ALERT = "offline_alert"


class SeverityEnum(str, Enum):
    """Alert severity levels"""
    INFO = "info"
    WARNING = "warning"
    CRITICAL = "critical"


class DeviceStatusEnum(str, Enum):
    """Device presence status"""
    ONLINE = "online"
    OFFLINE = "offline"
    MIXED = "mixed"  # mixed activity in time period


class PageViewEnum(str, Enum):
    """Dashboard pages"""
    DASHBOARD = "dashboard"
    DEVICES = "devices"
    ANALYTICS = "analytics"
    SETTINGS = "settings"
    REPORTS = "reports"


class ActionTypeEnum(str, Enum):
    """Dashboard actions"""
    VIEW = "view"
    EXPORT = "export"
    FILTER = "filter"
    REPORT_GENERATED = "report_generated"
    SEARCH = "search"


# ============================================================================
# DEVICE MODELS
# ============================================================================

class DeviceBase(BaseModel):
    """Base device information"""
    mac_address: str = Field(..., description="MAC address (17 chars)")
    device_name: Optional[str] = None
    device_type: Optional[DeviceTypeEnum] = None
    vendor_name: Optional[str] = None
    os_type: Optional[str] = None
    device_label: Optional[str] = Field(None, description="Custom label (e.g., 'John\\'s iPhone')")
    is_personal_device: bool = False


class DeviceCreate(DeviceBase):
    """Create device"""
    pass


class DeviceUpdate(BaseModel):
    """Update device"""
    device_name: Optional[str] = None
    device_label: Optional[str] = None
    device_type: Optional[DeviceTypeEnum] = None
    is_personal_device: Optional[bool] = None
    is_active: Optional[bool] = None


class Device(DeviceBase):
    """Device response model"""
    device_id: UUID
    first_seen_at: datetime
    last_seen_at: datetime
    last_updated_at: datetime
    is_active: bool
    total_connections: int
    avg_signal_strength: Optional[int]
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class DeviceDetail(Device):
    """Detailed device information with stats"""
    uptime_percentage_30d: Optional[float] = None
    connections_today: Optional[int] = None
    presence_today_seconds: Optional[int] = None
    avg_signal_today: Optional[int] = None


# ============================================================================
# DEVICE EVENT MODELS
# ============================================================================

class DeviceEventBase(BaseModel):
    """Base event information"""
    mac_address: str
    event_type: EventTypeEnum
    event_timestamp: datetime
    signal_strength: Optional[int] = Field(None, description="RSSI in dBm (-30 to -90)")
    duration_seconds: Optional[int] = None
    location_zone: Optional[str] = None
    ip_address: Optional[str] = None
    hostname: Optional[str] = None


class DeviceEventCreate(DeviceEventBase):
    """Create event"""
    device_id: Optional[UUID] = None


class DeviceEvent(DeviceEventBase):
    """Event response model"""
    event_id: int
    device_id: Optional[UUID]
    created_at: datetime

    class Config:
        from_attributes = True


# ============================================================================
# ACTIVITY AGGREGATES
# ============================================================================

class DailyActivityBase(BaseModel):
    """Daily activity aggregates"""
    device_id: UUID
    date: date
    connection_count: int = 0
    disconnection_count: int = 0
    total_presence_seconds: int = 0
    avg_signal_strength: Optional[int] = None
    min_signal_strength: Optional[int] = None
    max_signal_strength: Optional[int] = None
    signal_samples: int = 0
    first_connection_time: Optional[time] = None
    last_disconnection_time: Optional[time] = None
    presence_percentage: Optional[int] = Field(None, ge=0, le=100)


class DailyActivity(DailyActivityBase):
    """Daily activity response"""
    activity_id: UUID
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class HourlyActivityBase(BaseModel):
    """Hourly activity aggregates"""
    device_id: UUID
    hour_start: datetime
    connection_count: int = 0
    disconnection_count: int = 0
    total_presence_seconds: int = 0
    avg_signal_strength: Optional[int] = None
    min_signal_strength: Optional[int] = None
    max_signal_strength: Optional[int] = None
    device_status: Optional[DeviceStatusEnum] = None


class HourlyActivity(HourlyActivityBase):
    """Hourly activity response"""
    activity_id: UUID
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class NetworkSummaryBase(BaseModel):
    """Network-wide daily summary"""
    date: date
    total_unique_devices: int = 0
    active_devices_today: int = 0
    new_devices_count: int = 0
    offline_devices_count: int = 0
    total_events: int = 0
    connection_events: int = 0
    disconnection_events: int = 0
    avg_connected_devices: Optional[int] = None
    peak_connected_devices: Optional[int] = None
    peak_time: Optional[time] = None
    avg_signal_strength: Optional[int] = None
    busiest_hour: Optional[int] = Field(None, ge=0, le=23)
    quietest_hour: Optional[int] = Field(None, ge=0, le=23)


class NetworkSummary(NetworkSummaryBase):
    """Network summary response"""
    summary_id: UUID
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


# ============================================================================
# SESSION MODELS
# ============================================================================

class DeviceSessionBase(BaseModel):
    """Device session (connection window)"""
    device_id: UUID
    mac_address: str
    session_start: datetime
    session_end: Optional[datetime] = None
    duration_seconds: Optional[int] = None
    is_active: bool = True
    avg_signal_strength: Optional[int] = None
    min_signal_strength: Optional[int] = None
    max_signal_strength: Optional[int] = None
    disconnections_during_session: int = 0
    date_started: date


class DeviceSession(DeviceSessionBase):
    """Session response"""
    session_id: UUID
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


# ============================================================================
# ALERT MODELS
# ============================================================================

class AlertBase(BaseModel):
    """Alert information"""
    device_id: Optional[UUID] = None
    mac_address: Optional[str] = None
    alert_type: AlertTypeEnum
    severity: SeverityEnum
    title: str
    description: Optional[str] = None


class AlertCreate(AlertBase):
    """Create alert"""
    pass


class AlertUpdate(BaseModel):
    """Update alert"""
    is_acknowledged: Optional[bool] = None
    acknowledged_by: Optional[UUID] = None


class Alert(AlertBase):
    """Alert response"""
    alert_id: UUID
    is_acknowledged: bool
    acknowledged_by: Optional[UUID] = None
    acknowledged_at: Optional[datetime] = None
    triggered_at: datetime
    created_at: datetime

    class Config:
        from_attributes = True


# ============================================================================
# USER & DASHBOARD MODELS
# ============================================================================

class AdminUserBase(BaseModel):
    """Admin user information"""
    username: str
    email: str
    full_name: Optional[str] = None


class AdminUserCreate(AdminUserBase):
    """Create admin user"""
    pass


class AdminUser(AdminUserBase):
    """Admin user response"""
    user_id: UUID
    is_active: bool
    created_at: datetime
    last_login_at: Optional[datetime] = None
    updated_at: datetime

    class Config:
        from_attributes = True


class DashboardActivityBase(BaseModel):
    """Dashboard user activity"""
    user_id: UUID
    page_viewed: PageViewEnum
    action_type: ActionTypeEnum
    viewed_at: datetime
    duration_seconds: Optional[int] = None
    session_id: Optional[str] = None
    ip_address: Optional[str] = None
    user_agent: Optional[str] = None
    query_filters: Optional[Dict[str, Any]] = None


class DashboardActivityCreate(BaseModel):
    """Create dashboard activity (simplified)"""
    page_viewed: PageViewEnum
    action_type: ActionTypeEnum
    duration_seconds: Optional[int] = None
    query_filters: Optional[Dict[str, Any]] = None


class DashboardActivity(DashboardActivityBase):
    """Dashboard activity response"""
    activity_id: UUID
    created_at: datetime

    class Config:
        from_attributes = True


# ============================================================================
# ANALYTICS & REPORTING MODELS
# ============================================================================

class QuickStats(BaseModel):
    """Quick dashboard statistics"""
    total_devices: int
    online_devices: int
    offline_devices: int
    avg_signal_strength: Optional[int]
    last_activity: Optional[datetime]
    events_today: int
    new_devices_today: int


class DeviceUptimeSummary(BaseModel):
    """Device uptime metrics"""
    device_id: UUID
    mac_address: str
    device_label: Optional[str]
    device_type: Optional[str]
    uptime_percentage: float = Field(..., ge=0, le=100)
    active_days: int


class ConnectionTrend(BaseModel):
    """Connection trend data"""
    timestamp: datetime
    connection_count: int
    disconnection_count: int
    active_devices: int
    avg_signal: Optional[int]


class PeakTimeAnalysis(BaseModel):
    """Peak time statistics"""
    hour: int = Field(..., ge=0, le=23)
    day_of_week: int = Field(..., ge=0, le=6, description="0=Monday, 6=Sunday")
    avg_connections: int
    avg_active_devices: int
    event_frequency: int


class DeviceActivityFeed(BaseModel):
    """Recent device activity"""
    device_id: UUID
    mac_address: str
    device_label: Optional[str]
    last_event_type: EventTypeEnum
    last_event_time: datetime
    current_status: DeviceStatusEnum
    signal_strength: Optional[int]


class AnalyticsReport(BaseModel):
    """Complete analytics report"""
    report_id: UUID = Field(default_factory=lambda: UUID('00000000-0000-0000-0000-000000000000'))
    report_type: str  # 'daily', 'weekly', 'monthly'
    period_start: date
    period_end: date
    total_devices: int
    active_devices: int
    total_events: int
    avg_uptime_percentage: float
    peak_connections: int
    generated_at: datetime = Field(default_factory=datetime.now)
    generated_by: Optional[UUID] = None


class DeviceWeeklyStat(BaseModel):
    """Weekly device statistics"""
    device_id: UUID
    week_start: date
    total_connections: int
    total_disconnections: int
    total_presence_hours: int
    avg_daily_presence_percentage: int
    avg_signal_strength: Optional[int]


# ============================================================================
# FILTER & QUERY MODELS
# ============================================================================

class TimeRangeFilter(BaseModel):
    """Time range filter"""
    start_date: date
    end_date: date


class DeviceFilter(BaseModel):
    """Device filtering options"""
    device_type: Optional[DeviceTypeEnum] = None
    is_active: Optional[bool] = None
    is_personal_device: Optional[bool] = None
    vendor_name: Optional[str] = None
    search: Optional[str] = Field(None, description="Search by MAC, name, or label")


class AlertFilter(BaseModel):
    """Alert filtering options"""
    alert_type: Optional[AlertTypeEnum] = None
    severity: Optional[SeverityEnum] = None
    is_acknowledged: Optional[bool] = None
    device_id: Optional[UUID] = None


# ============================================================================
# PAGINATION MODELS
# ============================================================================

class PaginationParams(BaseModel):
    """Pagination parameters"""
    skip: int = Field(0, ge=0)
    limit: int = Field(50, ge=1, le=500)
    sort_by: Optional[str] = None
    sort_order: str = Field("desc", pattern="^(asc|desc)$")


class PaginatedResponse(BaseModel):
    """Paginated response wrapper"""
    total_count: int
    skip: int
    limit: int
    items: List[Any]


# ============================================================================
# ERROR MODELS
# ============================================================================

class ErrorResponse(BaseModel):
    """Error response"""
    error_code: str
    message: str
    details: Optional[Dict[str, Any]] = None
    timestamp: datetime = Field(default_factory=datetime.now)
