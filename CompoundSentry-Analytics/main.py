"""
Compound Sentry Analytics FastAPI Application
Main entry point for the analytics service
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from contextlib import asynccontextmanager
import logging
from datetime import datetime

# Import routers
from analytics_routes import router as analytics_router

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


# ============================================================================
# LIFESPAN EVENTS
# ============================================================================

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    FastAPI lifespan context manager for startup/shutdown events.
    """
    # Startup
    logger.info("Starting Compound Sentry Analytics API...")
    logger.info("Initializing database connections...")
    # TODO: Initialize database, connection pools, background tasks, etc.
    
    yield
    
    # Shutdown
    logger.info("Shutting down Compound Sentry Analytics API...")
    # TODO: Cleanup resources, close connections, etc.


# ============================================================================
# APPLICATION FACTORY
# ============================================================================

def create_app() -> FastAPI:
    """
    Create and configure the FastAPI application.
    """
    app = FastAPI(
        title="Compound Sentry Analytics API",
        description="Analytics and reporting service for Compound Sentry network monitoring system",
        version="1.0.0",
        docs_url="/api/docs",
        openapi_url="/api/openapi.json",
        lifespan=lifespan
    )
    
    # ========================================================================
    # CORS MIDDLEWARE
    # ========================================================================
    
    app.add_middleware(
        CORSMiddleware,
        allow_origins=[
            "http://localhost:3000",
            "http://localhost:5173",  # Vite dev server
            "http://localhost",
            # Add your production frontend URLs here
        ],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    
    # ========================================================================
    # REQUEST/RESPONSE MIDDLEWARE
    # ========================================================================
    
    @app.middleware("http")
    async def add_process_time_header(request, call_next):
        """Add X-Process-Time header to responses."""
        import time
        start_time = time.time()
        response = await call_next(request)
        process_time = time.time() - start_time
        response.headers["X-Process-Time"] = str(process_time)
        return response
    
    # ========================================================================
    # ROUTES
    # ========================================================================
    
    # Include analytics router
    app.include_router(analytics_router)
    
    # ========================================================================
    # ROOT ENDPOINTS
    # ========================================================================
    
    @app.get("/", tags=["root"])
    async def root():
        """Root endpoint with API information."""
        return {
            "service": "Compound Sentry Analytics API",
            "version": "1.0.0",
            "docs": "/api/docs",
            "health": "/health",
            "timestamp": datetime.utcnow().isoformat()
        }
    
    @app.get("/health", tags=["health"])
    async def health_check():
        """Health check endpoint."""
        return {
            "status": "healthy",
            "service": "Compound Sentry Analytics",
            "timestamp": datetime.utcnow().isoformat()
        }
    
    @app.get("/api/info", tags=["info"])
    async def api_info():
        """API information endpoint."""
        return {
            "name": "Compound Sentry Analytics API",
            "version": "1.0.0",
            "description": "Analytics and reporting service for network device monitoring",
            "base_url": "/api/analytics",
            "docs": "/api/docs",
            "openapi": "/api/openapi.json"
        }
    
    # ========================================================================
    # ERROR HANDLERS
    # ========================================================================
    
    @app.exception_handler(Exception)
    async def general_exception_handler(request, exc):
        """Global exception handler."""
        logger.error(f"Unhandled exception: {str(exc)}")
        return JSONResponse(
            status_code=500,
            content={
                "error": "Internal server error",
                "message": str(exc),
                "timestamp": datetime.utcnow().isoformat()
            }
        )
    
    return app


# ============================================================================
# APPLICATION INSTANCE
# ============================================================================

app = create_app()


# ============================================================================
# MAIN ENTRY POINT
# ============================================================================

if __name__ == "__main__":
    import uvicorn
    
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,  # Enable for development
        log_level="info"
    )
