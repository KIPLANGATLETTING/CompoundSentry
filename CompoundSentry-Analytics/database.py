"""
Database Configuration and Models for Analytics
SQLAlchemy ORM setup
"""

from sqlalchemy import create_engine, Column, Integer, String, DateTime, Date, Time, Boolean, ForeignKey, BIGINT, Numeric, JSON, INET, UniqueConstraint
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker, relationship
from sqlalchemy.pool import QueuePool
import uuid
from datetime import datetime, date, time
import os
from dotenv import load_dotenv

load_dotenv()

# ============================================================================
# DATABASE CONFIGURATION
# ============================================================================

DATABASE_URL = os.getenv(
    "DATABASE_URL",
    "postgresql://user:password@localhost:5432/compound_sentry_analytics"
)

# Create engine with connection pooling
engine = create_engine(
    DATABASE_URL,
    echo=os.getenv("SQL_ECHO", "false").lower() == "true",
    poolclass=QueuePool,
    pool_size=20,
    max_overflow=40,
    pool_pre_ping=True,  # Test connections before using
    pool_recycle=3600,   # Recycle connections after 1 hour
)

SessionLocal = sessionmaker(
    autocommit=False,
    autoflush=False,
    bind=engine,
    expire_on_commit=False
)

Base = declarative_base()


# ============================================================================
# ORM MODELS
# ============================================================================

class Device(Base):
    """Master device registry"""
    __tablename__ = "devices"
    
    device_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    mac_address = Column(String(17), unique=True, nullable=False, index=True)
    first_seen_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    last_seen_at = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    last_updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    device_name = Column(String(255))
    device_type = Column(String(50), index=True)
    vendor_name = Column(String(255))
    os_type = Column(String(100))
    is_active = Column(Boolean, default=True, index=True)
    total_connections = Column(Integer, default=0)
    avg_signal_strength = Column(Integer)
    is_personal_device = Column(Boolean, default=False)
    device_label = Column(String(255))
    created_at = Column(DateTime, default=datetime.utcnow, index=True)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # Relationships
    events = relationship("DeviceEvent", back_populates="device", cascade="all, delete-orphan")
    daily_activities = relationship("DailyDeviceActivity", back_populates="device", cascade="all, delete-orphan")
    hourly_activities = relationship("HourlyDeviceActivity", back_populates="device", cascade="all, delete-orphan")
    sessions = relationship("DeviceSession", back_populates="device", cascade="all, delete-orphan")
    presence_timeline = relationship("DevicePresenceTimeline", back_populates="device", cascade="all, delete-orphan")


class DeviceEvent(Base):
    """Raw event stream"""
    __tablename__ = "device_events"
    
    event_id = Column(BIGINT, primary_key=True, autoincrement=True)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), index=True)
    mac_address = Column(String(17), nullable=False, index=True)
    event_type = Column(String(20), nullable=False, index=True)
    event_timestamp = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    signal_strength = Column(Integer)
    duration_seconds = Column(Integer)
    location_zone = Column(String(100))
    ip_address = Column(INET)
    hostname = Column(String(255))
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # Relationships
    device = relationship("Device", back_populates="events")


class DailyDeviceActivity(Base):
    """Daily aggregates"""
    __tablename__ = "daily_device_activity"
    
    activity_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), nullable=False, index=True)
    date = Column(Date, nullable=False)
    connection_count = Column(Integer, default=0)
    disconnection_count = Column(Integer, default=0)
    total_presence_seconds = Column(Integer, default=0)
    avg_signal_strength = Column(Integer)
    min_signal_strength = Column(Integer)
    max_signal_strength = Column(Integer)
    signal_samples = Column(Integer, default=0)
    first_connection_time = Column(Time)
    last_disconnection_time = Column(Time)
    presence_percentage = Column(Integer)
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # Composite unique constraint
    __table_args__ = (
        UniqueConstraint('device_id', 'date', name='unique_device_date'),
    )
    
    # Relationships
    device = relationship("Device", back_populates="daily_activities")


class HourlyDeviceActivity(Base):
    """Hourly aggregates"""
    __tablename__ = "hourly_device_activity"
    
    activity_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), nullable=False, index=True)
    hour_start = Column(DateTime, nullable=False, index=True)
    connection_count = Column(Integer, default=0)
    disconnection_count = Column(Integer, default=0)
    total_presence_seconds = Column(Integer, default=0)
    avg_signal_strength = Column(Integer)
    min_signal_strength = Column(Integer)
    max_signal_strength = Column(Integer)
    device_status = Column(String(20))
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    __table_args__ = (
        UniqueConstraint('device_id', 'hour_start', name='unique_device_hour'),
    )
    
    # Relationships
    device = relationship("Device", back_populates="hourly_activities")


class NetworkSummary(Base):
    """Network-wide daily summary"""
    __tablename__ = "network_summary"
    
    summary_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    date = Column(Date, unique=True, nullable=False, index=True)
    total_unique_devices = Column(Integer, default=0)
    active_devices_today = Column(Integer, default=0)
    new_devices_count = Column(Integer, default=0)
    offline_devices_count = Column(Integer, default=0)
    total_events = Column(Integer, default=0)
    connection_events = Column(Integer, default=0)
    disconnection_events = Column(Integer, default=0)
    avg_connected_devices = Column(Integer)
    peak_connected_devices = Column(Integer)
    peak_time = Column(Time)
    avg_signal_strength = Column(Integer)
    busiest_hour = Column(Integer)
    quietest_hour = Column(Integer)
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)


class DeviceSession(Base):
    """Device connection sessions"""
    __tablename__ = "device_sessions"
    
    session_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), nullable=False, index=True)
    mac_address = Column(String(17), nullable=False)
    session_start = Column(DateTime, nullable=False, index=True)
    session_end = Column(DateTime)
    duration_seconds = Column(Integer)
    is_active = Column(Boolean, default=True, index=True)
    avg_signal_strength = Column(Integer)
    min_signal_strength = Column(Integer)
    max_signal_strength = Column(Integer)
    disconnections_during_session = Column(Integer, default=0)
    date_started = Column(Date, nullable=False, index=True)
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # Relationships
    device = relationship("Device", back_populates="sessions")


class DevicePresenceTimeline(Base):
    """Presence state changes"""
    __tablename__ = "device_presence_timeline"
    
    timeline_id = Column(BIGINT, primary_key=True, autoincrement=True)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), nullable=False, index=True)
    mac_address = Column(String(17), nullable=False)
    status = Column(String(20), nullable=False, index=True)
    status_changed_at = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    reason = Column(String(100))
    location_zone = Column(String(100))
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # Relationships
    device = relationship("Device", back_populates="presence_timeline")


class AdminUser(Base):
    """Admin users"""
    __tablename__ = "admin_users"
    
    user_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    username = Column(String(255), unique=True, nullable=False)
    email = Column(String(255), unique=True, nullable=False, index=True)
    full_name = Column(String(100))
    is_active = Column(Boolean, default=True, index=True)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    last_login_at = Column(DateTime)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # Relationships
    dashboard_activities = relationship("DashboardActivity", back_populates="user", cascade="all, delete-orphan")
    acknowledged_alerts = relationship("Alert", back_populates="acknowledged_by_user", foreign_keys="Alert.acknowledged_by")


class DashboardActivity(Base):
    """Dashboard user activity tracking"""
    __tablename__ = "dashboard_activity"
    
    activity_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id = Column(UUID(as_uuid=True), ForeignKey("admin_users.user_id", ondelete="CASCADE"), nullable=False, index=True)
    page_viewed = Column(String(100), index=True)
    action_type = Column(String(100))
    viewed_at = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    duration_seconds = Column(Integer)
    session_id = Column(String(255))
    ip_address = Column(INET)
    user_agent = Column(String(500))
    query_filters = Column(JSON)
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # Relationships
    user = relationship("AdminUser", back_populates="dashboard_activities")


class Alert(Base):
    """System alerts and anomalies"""
    __tablename__ = "alerts"
    
    alert_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="SET NULL"), index=True)
    mac_address = Column(String(17))
    alert_type = Column(String(100), index=True)
    severity = Column(String(20), index=True)
    title = Column(String(255), nullable=False)
    description = Column(String)
    is_acknowledged = Column(Boolean, default=False, index=True)
    acknowledged_by = Column(UUID(as_uuid=True), ForeignKey("admin_users.user_id", ondelete="SET NULL"))
    acknowledged_at = Column(DateTime)
    triggered_at = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # Relationships
    acknowledged_by_user = relationship("AdminUser", back_populates="acknowledged_alerts", foreign_keys=[acknowledged_by])


class DeviceStatsWeekly(Base):
    """Weekly rollup statistics"""
    __tablename__ = "device_stats_weekly"
    
    stats_id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id = Column(UUID(as_uuid=True), ForeignKey("devices.device_id", ondelete="CASCADE"), nullable=False, index=True)
    week_start = Column(Date, nullable=False)
    total_connections = Column(Integer)
    total_disconnections = Column(Integer)
    total_presence_hours = Column(Integer)
    avg_daily_presence_percentage = Column(Integer)
    avg_signal_strength = Column(Integer)
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    __table_args__ = (
        UniqueConstraint('device_id', 'week_start', name='unique_device_week'),
    )


# ============================================================================
# DATABASE INITIALIZATION
# ============================================================================

def init_db():
    """Initialize database and create all tables."""
    Base.metadata.create_all(bind=engine)
    print("Database tables created successfully!")


def get_db_session():
    """Get database session for dependency injection."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


if __name__ == "__main__":
    init_db()
