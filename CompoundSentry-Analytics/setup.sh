#!/bin/bash

# Compound Sentry Analytics - Quick Setup Script
# This script automates initial setup of the analytics service

set -e

echo "═════════════════════════════════════════════════════════════════"
echo "  Compound Sentry Analytics - Quick Setup"
echo "═════════════════════════════════════════════════════════════════"
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[✓]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

check_command() {
    if ! command -v $1 &> /dev/null; then
        print_error "$1 is not installed"
        return 1
    fi
    print_success "$1 found"
    return 0
}

# Step 1: Check prerequisites
echo -e "${BLUE}Step 1: Checking Prerequisites...${NC}"
echo ""

check_command "python3" || { print_error "Python 3 required"; exit 1; }
check_command "psql" || { print_warning "PostgreSQL client not found (you may need to install it)"; }
check_command "redis-cli" || { print_warning "Redis CLI not found (optional)"; }

PYTHON_VERSION=$(python3 --version | cut -d' ' -f2)
print_info "Python version: $PYTHON_VERSION"

# Verify Python 3.11+
MAJOR=$(echo $PYTHON_VERSION | cut -d'.' -f1)
MINOR=$(echo $PYTHON_VERSION | cut -d'.' -f2)

if [ $MAJOR -lt 3 ] || ([ $MAJOR -eq 3 ] && [ $MINOR -lt 11 ]); then
    print_warning "Python 3.11+ recommended (found $PYTHON_VERSION)"
fi

echo ""

# Step 2: Create virtual environment
echo -e "${BLUE}Step 2: Setting Up Virtual Environment...${NC}"
echo ""

if [ ! -d "venv" ]; then
    print_info "Creating virtual environment..."
    python3 -m venv venv
    print_success "Virtual environment created"
else
    print_warning "Virtual environment already exists"
fi

source venv/bin/activate || { print_error "Failed to activate venv"; exit 1; }
print_success "Virtual environment activated"

echo ""

# Step 3: Install dependencies
echo -e "${BLUE}Step 3: Installing Dependencies...${NC}"
echo ""

print_info "Upgrading pip..."
pip install --upgrade pip > /dev/null 2>&1

print_info "Installing requirements.txt..."
pip install -r requirements.txt > /dev/null 2>&1

print_success "Dependencies installed"

echo ""

# Step 4: Environment configuration
echo -e "${BLUE}Step 4: Configuring Environment...${NC}"
echo ""

if [ ! -f ".env" ]; then
    print_info "Copying .env.example to .env..."
    cp .env.example .env
    print_success ".env created"
    print_warning "Please edit .env with your database credentials"
else
    print_warning ".env already exists"
fi

echo ""

# Step 5: Database setup (optional)
echo -e "${BLUE}Step 5: Database Setup (Optional)${NC}"
echo ""

read -p "Do you want to initialize the database now? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    
    print_info "Enter database connection details:"
    read -p "PostgreSQL User [analytics_user]: " DB_USER
    DB_USER=${DB_USER:-analytics_user}
    
    read -p "PostgreSQL Host [localhost]: " DB_HOST
    DB_HOST=${DB_HOST:-localhost}
    
    read -p "PostgreSQL Port [5432]: " DB_PORT
    DB_PORT=${DB_PORT:-5432}
    
    read -p "Database Name [compound_sentry_analytics]: " DB_NAME
    DB_NAME=${DB_NAME:-compound_sentry_analytics}
    
    read -sp "PostgreSQL Password: " DB_PASSWORD
    echo
    
    # Update .env
    sed -i "s|postgresql://.*|postgresql://$DB_USER:$DB_PASSWORD@$DB_HOST:$DB_PORT/$DB_NAME|" .env
    print_success "Database credentials updated in .env"
    
    # Test connection
    print_info "Testing database connection..."
    if PGPASSWORD=$DB_PASSWORD psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "SELECT 1" > /dev/null 2>&1; then
        print_success "Database connection successful"
        
        # Initialize schema
        print_info "Initializing database schema..."
        python database.py
        print_success "Database schema initialized"
    else
        print_error "Database connection failed"
        print_warning "Database setup skipped. You can run 'python database.py' later"
    fi
else
    print_info "Database setup skipped"
    print_warning "Run 'python database.py' to initialize the database later"
fi

echo ""

# Step 6: Verify installation
echo -e "${BLUE}Step 6: Verifying Installation...${NC}"
echo ""

print_info "Checking imports..."

python -c "import fastapi; print('✓ FastAPI')" 2>/dev/null || print_error "FastAPI import failed"
python -c "import sqlalchemy; print('✓ SQLAlchemy')" 2>/dev/null || print_error "SQLAlchemy import failed"
python -c "import pydantic; print('✓ Pydantic')" 2>/dev/null || print_error "Pydantic import failed"
python -c "import psycopg2; print('✓ psycopg2')" 2>/dev/null || print_error "psycopg2 import failed"

echo ""

# Step 7: Summary
echo -e "${BLUE}Step 7: Setup Complete!${NC}"
echo ""

print_success "Compound Sentry Analytics setup complete!"
echo ""

echo "Next steps:"
echo "1. Review and edit .env with your configuration"
echo "2. Ensure PostgreSQL is running"
echo "3. Run: python database.py (if not done above)"
echo "4. Start the API server:"
echo "   python -m uvicorn main:app --reload"
echo ""
echo "API Documentation will be available at:"
echo "   http://localhost:8000/api/docs"
echo ""

echo "For more information, see:"
echo "  - QUICK_REFERENCE.md"
echo "  - IMPLEMENTATION_GUIDE.md"
echo "  - ANALYTICS_DB_SCHEMA.md"
echo ""

print_success "Ready to start!"
