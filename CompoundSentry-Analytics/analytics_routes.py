"""
FastAPI Routes for Compound Sentry Analytics
Example implementation with SQLAlchemy ORM
"""

from fastapi import APIRouter, HTTPException, Depends, Query
from fastapi.responses import JSONResponse
from datetime import datetime, date, timedelta
from typing import List, Optional
from uuid import UUID
from sqlalchemy.orm import Session
from sqlalchemy import func, and_, or_, desc
import logging

from analytics_models import (
    Device as DeviceSchema, DeviceCreate, DeviceUpdate, DeviceDetail,
    DeviceEvent as DeviceEventSchema, DeviceEventCreate,
    DailyActivity as DailyActivitySchema, HourlyActivity as HourlyActivitySchema,
    NetworkSummary as NetworkSummarySchema,
    DeviceSession as DeviceSessionSchema, Alert as AlertSchema, AlertCreate, AlertUpdate, AlertFilter,
    QuickStats, DeviceActivityFeed, AnalyticsReport,
    PaginatedResponse, PaginationParams, TimeRangeFilter,
    DeviceFilter, ConnectionTrend, PeakTimeAnalysis,
    AlertTypeEnum, SeverityEnum, DeviceStatusEnum
)

from database import (
    get_db_session,
    Device as DeviceORM, DeviceEvent as DeviceEventORM,
    DailyDeviceActivity as DailyDeviceActivityORM,
    HourlyDeviceActivity as HourlyDeviceActivityORM,
    NetworkSummary as NetworkSummaryORM,
    DeviceSession as DeviceSessionORM,
    Alert as AlertORM,
    DashboardActivity as DashboardActivityORM,
)

logger = logging.getLogger(__name__)

# Router for analytics endpoints
router = APIRouter(
    prefix="/api/analytics",
    tags=["analytics"],
    responses={404: {"description": "Not found"}}
)

# ============================================================================
# DEPENDENCY: Database Session (implement based on your setup)
# ============================================================================

def get_db():
    """
    Dependency to get database session.
    """
    yield from get_db_session()


# ============================================================================
# DEVICE ENDPOINTS
# ============================================================================

@router.get("/devices/overview", response_model=QuickStats)
async def get_quick_stats(db: Session = Depends(get_db)):
    """
    Get quick network statistics for dashboard.
    
    Returns:
    - Total devices registered
    - Currently online devices
    - Currently offline devices
    - Average signal strength
    """
    try:
        # This assumes you have SQLAlchemy models defined
        # from database_models import Device, DeviceEvent
        
        total_devices = db.query(func.count(DeviceORM.device_id)).scalar() or 0
        online_devices = db.query(func.count(DeviceORM.device_id)).filter(
            DeviceORM.is_active == True
        ).scalar() or 0
        offline_devices = total_devices - online_devices
        
        avg_signal = db.query(func.avg(DeviceORM.avg_signal_strength)).scalar()
        
        # Get today's events
        today = date.today()
        events_today = db.query(func.count(DeviceEventORM.event_id)).filter(
            DeviceEventORM.event_timestamp >= datetime.combine(today, datetime.min.time())
        ).scalar() or 0
        
        last_activity = db.query(func.max(DeviceORM.last_seen_at)).scalar()
        
        return QuickStats(
            total_devices=total_devices,
            online_devices=online_devices,
            offline_devices=offline_devices,
            avg_signal_strength=int(avg_signal) if avg_signal else None,
            last_activity=last_activity,
            events_today=events_today,
            new_devices_today=0  # Calculate if needed
        )
    except Exception as e:
        logger.error(f"Error fetching quick stats: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch statistics")


@router.get("/devices", response_model=PaginatedResponse)
async def list_devices(
    db: Session = Depends(get_db),
    skip: int = Query(0, ge=0),
    limit: int = Query(50, ge=1, le=500),
    device_type: Optional[str] = None,
    is_active: Optional[bool] = None,
    search: Optional[str] = None
):
    """
    List all devices with filtering and pagination.
    
    Query parameters:
    - skip: Number of items to skip
    - limit: Number of items to return
    - device_type: Filter by device type
    - is_active: Filter by active status
    - search: Search by MAC, name, or label
    """
    try:
        query = db.query(DeviceORM)
        
        # Apply filters
        if device_type:
            query = query.filter(DeviceORM.device_type == device_type)
        
        if is_active is not None:
            query = query.filter(DeviceORM.is_active == is_active)
        
        if search:
            search_pattern = f"%{search}%"
            query = query.filter(
                or_(
                    DeviceORM.mac_address.ilike(search_pattern),
                    DeviceORM.device_name.ilike(search_pattern),
                    DeviceORM.device_label.ilike(search_pattern)
                )
            )
        
        # Count total
        total_count = query.count()
        
        # Apply pagination
        devices = query.order_by(desc(DeviceORM.last_seen_at)).offset(skip).limit(limit).all()
        
        return PaginatedResponse(
            total_count=total_count,
            skip=skip,
            limit=limit,
            items=[DeviceSchema.from_orm(d) for d in devices]
        )
    except Exception as e:
        logger.error(f"Error listing devices: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch devices")


@router.get("/devices/{device_id}", response_model=DeviceDetail)
async def get_device_detail(
    device_id: UUID,
    db: Session = Depends(get_db)
):
    """
    Get detailed information about a specific device.
    
    Includes:
    - Basic device info
    - 30-day uptime
    - Today's activity
    - Current status
    """
    try:
        device = db.query(DeviceORM).filter(DeviceORM.device_id == device_id).first()
        
        if not device:
            raise HTTPException(status_code=404, detail="Device not found")
        
        # Calculate 30-day uptime
        thirty_days_ago = date.today() - timedelta(days=30)
        uptime_data = db.query(
            func.coalesce(func.sum(DailyDeviceActivityORM.total_presence_seconds), 0)
        ).filter(
            and_(
                DailyDeviceActivityORM.device_id == device_id,
                DailyDeviceActivityORM.date >= thirty_days_ago
            )
        ).scalar()
        
        total_seconds = 30 * 24 * 3600
        uptime_percentage = (uptime_data / total_seconds * 100) if uptime_data else 0
        
        # Get today's activity
        today = date.today()
        today_activity = db.query(DailyDeviceActivityORM).filter(
            and_(
                DailyDeviceActivityORM.device_id == device_id,
                DailyDeviceActivityORM.date == today
            )
        ).first()
        
        return DeviceDetail(
            **DeviceSchema.from_orm(device).dict(),
            uptime_percentage_30d=round(uptime_percentage, 2),
            connections_today=today_activity.connection_count if today_activity else 0,
            presence_today_seconds=today_activity.total_presence_seconds if today_activity else 0,
            avg_signal_today=today_activity.avg_signal_strength if today_activity else None
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error fetching device detail: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch device")


@router.get("/devices/{device_id}/timeline")
async def get_device_timeline(
    device_id: UUID,
    db: Session = Depends(get_db),
    start_date: date = Query(..., description="Start date (YYYY-MM-DD)"),
    end_date: date = Query(..., description="End date (YYYY-MM-DD)"),
    resolution: str = Query("hourly", regex="^(hourly|daily)$")
):
    """
    Get device activity timeline (hourly or daily aggregates).
    
    Returns activity data for the specified date range.
    """
    try:
        if resolution == "daily":
            activities = db.query(DailyDeviceActivityORM).filter(
                and_(
                    DailyDeviceActivityORM.device_id == device_id,
                    DailyDeviceActivityORM.date >= start_date,
                    DailyDeviceActivityORM.date <= end_date
                )
            ).order_by(DailyDeviceActivityORM.date).all()
            
            return [DailyActivitySchema.from_orm(a) for a in activities]
        
        else:  # hourly
            activities = db.query(HourlyDeviceActivityORM).filter(
                and_(
                    HourlyDeviceActivityORM.device_id == device_id,
                    HourlyDeviceActivityORM.hour_start >= datetime.combine(start_date, datetime.min.time()),
                    HourlyDeviceActivityORM.hour_start <= datetime.combine(end_date, datetime.max.time())
                )
            ).order_by(HourlyDeviceActivityORM.hour_start).all()
            
            return [HourlyActivitySchema.from_orm(a) for a in activities]
    except Exception as e:
        logger.error(f"Error fetching timeline: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch timeline")


@router.get("/devices/{device_id}/sessions")
async def get_device_sessions(
    device_id: UUID,
    db: Session = Depends(get_db),
    start_date: date = Query(...),
    end_date: date = Query(...),
    skip: int = Query(0, ge=0),
    limit: int = Query(50, ge=1, le=500)
):
    """
    Get device connection sessions for date range.
    
    A session is a continuous connection from login to logout.
    """
    try:
        query = db.query(DeviceSessionORM).filter(
            and_(
                DeviceSessionORM.device_id == device_id,
                DeviceSessionORM.date_started >= start_date,
                DeviceSessionORM.date_started <= end_date
            )
        ).order_by(desc(DeviceSessionORM.session_start))
        
        total_count = query.count()
        sessions = query.offset(skip).limit(limit).all()
        
        return PaginatedResponse(
            total_count=total_count,
            skip=skip,
            limit=limit,
            items=[DeviceSessionSchema.from_orm(s) for s in sessions]
        )
    except Exception as e:
        logger.error(f"Error fetching sessions: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch sessions")


# ============================================================================
# NETWORK ANALYTICS ENDPOINTS
# ============================================================================

@router.get("/network/summary")
async def get_network_summary(
    db: Session = Depends(get_db),
    date: date = Query(default_factory=date.today)
):
    """
    Get network-wide summary for a specific date.
    """
    try:
        summary = db.query(NetworkSummaryORM).filter(
            NetworkSummaryORM.date == date
        ).first()
        
        if not summary:
            raise HTTPException(status_code=404, detail="No summary data for this date")
        
        return NetworkSummarySchema.from_orm(summary)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error fetching network summary: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch network summary")


@router.get("/network/activity-feed")
async def get_network_activity_feed(
    db: Session = Depends(get_db),
    limit: int = Query(20, ge=1, le=100)
):
    """
    Get recent device activity across the network.
    """
    try:
        # Get recent events grouped by device
        recent_events = db.query(
            DeviceEventORM.device_id,
            DeviceORM.mac_address,
            DeviceORM.device_label,
            DeviceEventORM.event_type,
            DeviceEventORM.event_timestamp,
            DeviceEventORM.signal_strength
        ).join(DeviceORM).order_by(
            desc(DeviceEventORM.event_timestamp)
        ).limit(limit).all()
        
        return recent_events
    except Exception as e:
        logger.error(f"Error fetching activity feed: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch activity feed")


@router.get("/network/peak-times")
async def get_peak_times(
    db: Session = Depends(get_db),
    days: int = Query(30, ge=1, le=365)
):
    """
    Analyze peak activity times (busiest hours and days).
    
    Returns hourly and daily aggregates for pattern analysis.
    """
    try:
        start_date = date.today() - timedelta(days=days)
        
        # Query hourly peaks
        hourly_peaks = db.query(
            func.extract('hour', HourlyDeviceActivityORM.hour_start).label('hour'),
            func.avg(HourlyDeviceActivityORM.connection_count).label('avg_connections'),
            func.avg(HourlyDeviceActivityORM.disconnection_count).label('avg_disconnections')
        ).filter(
            HourlyDeviceActivityORM.hour_start >= datetime.combine(start_date, datetime.min.time())
        ).group_by('hour').order_by('hour').all()
        
        return {
            "peak_hours": [
                {
                    "hour": int(peak[0]),
                    "avg_connections": int(peak[1]),
                    "avg_disconnections": int(peak[2])
                }
                for peak in hourly_peaks
            ]
        }
    except Exception as e:
        logger.error(f"Error fetching peak times: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch peak times")


# ============================================================================
# ALERTS ENDPOINTS
# ============================================================================

@router.get("/alerts")
async def list_alerts(
    db: Session = Depends(get_db),
    alert_type: Optional[AlertTypeEnum] = None,
    severity: Optional[SeverityEnum] = None,
    is_acknowledged: Optional[bool] = None,
    skip: int = Query(0, ge=0),
    limit: int = Query(50, ge=1, le=500)
):
    """
    List alerts with filtering and pagination.
    """
    try:
        query = db.query(AlertORM)
        
        if alert_type:
            query = query.filter(AlertORM.alert_type == alert_type)
        
        if severity:
            query = query.filter(AlertORM.severity == severity)
        
        if is_acknowledged is not None:
            query = query.filter(AlertORM.is_acknowledged == is_acknowledged)
        
        total_count = query.count()
        alerts = query.order_by(desc(AlertORM.triggered_at)).offset(skip).limit(limit).all()
        
        return PaginatedResponse(
            total_count=total_count,
            skip=skip,
            limit=limit,
            items=[AlertSchema.from_orm(a) for a in alerts]
        )
    except Exception as e:
        logger.error(f"Error listing alerts: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to fetch alerts")


@router.post("/alerts/{alert_id}/acknowledge")
async def acknowledge_alert(
    alert_id: UUID,
    user_id: UUID = Query(...),
    db: Session = Depends(get_db)
):
    """
    Mark an alert as acknowledged.
    """
    try:
        alert = db.query(AlertORM).filter(AlertORM.alert_id == alert_id).first()
        
        if not alert:
            raise HTTPException(status_code=404, detail="Alert not found")
        
        alert.is_acknowledged = True
        alert.acknowledged_by = user_id
        alert.acknowledged_at = datetime.utcnow()
        
        db.commit()
        
        return {"message": "Alert acknowledged", "alert_id": alert_id}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error acknowledging alert: {str(e)}")
        db.rollback()
        raise HTTPException(status_code=500, detail="Failed to acknowledge alert")


# ============================================================================
# REPORTS ENDPOINTS
# ============================================================================

@router.get("/reports/daily")
async def get_daily_report(
    db: Session = Depends(get_db),
    date_param: date = Query(default_factory=date.today)
):
    """
    Generate daily analytics report.
    """
    try:
        summary = db.query(NetworkSummaryORM).filter(
            NetworkSummaryORM.date == date_param
        ).first()
        
        if not summary:
            raise HTTPException(status_code=404, detail="No data for this date")
        
        report = AnalyticsReport(
            report_type="daily",
            period_start=date_param,
            period_end=date_param,
            total_devices=summary.total_unique_devices,
            active_devices=summary.active_devices_today,
            total_events=summary.total_events,
            peak_connections=summary.peak_connected_devices or 0
        )
        
        return report
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error generating daily report: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to generate report")


@router.get("/reports/weekly")
async def get_weekly_report(
    db: Session = Depends(get_db),
    start_date: date = Query(...),
    end_date: date = Query(...)
):
    """
    Generate weekly analytics report.
    """
    try:
        # Query summaries for the week
        summaries = db.query(NetworkSummaryORM).filter(
            and_(
                NetworkSummaryORM.date >= start_date,
                NetworkSummaryORM.date <= end_date
            )
        ).all()
        
        if not summaries:
            raise HTTPException(status_code=404, detail="No data for this period")
        
        total_events = sum(s.total_events for s in summaries)
        avg_devices = sum(s.active_devices_today for s in summaries) / len(summaries)
        peak_connections = max(s.peak_connected_devices or 0 for s in summaries)
        
        report = AnalyticsReport(
            report_type="weekly",
            period_start=start_date,
            period_end=end_date,
            total_devices=len(set(s.total_unique_devices for s in summaries)),
            active_devices=int(avg_devices),
            total_events=total_events,
            peak_connections=peak_connections
        )
        
        return report
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error generating weekly report: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to generate report")


# ============================================================================
# HEALTH CHECK
# ============================================================================

@router.get("/health")
async def health_check():
    """
    Health check endpoint.
    """
    return {"status": "healthy", "timestamp": datetime.utcnow()}
