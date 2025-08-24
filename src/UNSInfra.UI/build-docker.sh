#!/bin/bash

# UNS Infrastructure UI - Docker Build Script
# Builds and optionally runs the UNS Infrastructure UI Docker container

set -e

# Configuration
IMAGE_NAME="unsinfra-ui"
IMAGE_TAG="${1:-latest}"
FULL_IMAGE_NAME="$IMAGE_NAME:$IMAGE_TAG"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [TAG] [OPTIONS]"
    echo ""
    echo "Arguments:"
    echo "  TAG                 Docker image tag (default: latest)"
    echo ""
    echo "Options:"
    echo "  --run              Build and run the container"
    echo "  --push             Push image to registry after build"
    echo "  --no-cache         Build without using cache"
    echo "  --help             Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                 # Build with 'latest' tag"
    echo "  $0 v1.5           # Build with 'v1.5' tag"
    echo "  $0 latest --run   # Build and run container"
    echo "  $0 dev --no-cache # Build dev image without cache"
}

# Parse command line arguments
BUILD_ARGS=""
RUN_CONTAINER=false
PUSH_IMAGE=false

for arg in "$@"; do
    case $arg in
        --run)
            RUN_CONTAINER=true
            shift
            ;;
        --push)
            PUSH_IMAGE=true
            shift
            ;;
        --no-cache)
            BUILD_ARGS="$BUILD_ARGS --no-cache"
            shift
            ;;
        --help)
            show_usage
            exit 0
            ;;
        --*)
            print_error "Unknown option: $arg"
            show_usage
            exit 1
            ;;
    esac
done

print_status "Building UNS Infrastructure UI Docker image..."
print_status "Image: $FULL_IMAGE_NAME"

# Check if Docker is running
if ! docker info >/dev/null 2>&1; then
    print_error "Docker is not running or not accessible"
    exit 1
fi

# Build the Docker image
print_status "Starting Docker build..."
if docker build $BUILD_ARGS -t $FULL_IMAGE_NAME .; then
    print_success "Docker image built successfully: $FULL_IMAGE_NAME"
else
    print_error "Docker build failed"
    exit 1
fi

# Show image size
IMAGE_SIZE=$(docker images $FULL_IMAGE_NAME --format "table {{.Size}}" | tail -n 1)
print_status "Image size: $IMAGE_SIZE"

# Push to registry if requested
if [ "$PUSH_IMAGE" = true ]; then
    print_status "Pushing image to registry..."
    if docker push $FULL_IMAGE_NAME; then
        print_success "Image pushed successfully"
    else
        print_error "Failed to push image"
        exit 1
    fi
fi

# Run container if requested
if [ "$RUN_CONTAINER" = true ]; then
    print_status "Starting container..."
    
    # Stop existing container if running
    if docker ps -q -f name=unsinfra-ui-standalone >/dev/null; then
        print_status "Stopping existing container..."
        docker stop unsinfra-ui-standalone
        docker rm unsinfra-ui-standalone
    fi
    
    # Run new container
    docker run -d \
        --name unsinfra-ui-standalone \
        -p 5000:8080 \
        -v unsinfra-ui-data:/app/data \
        -v unsinfra-ui-logs:/app/logs \
        -e ASPNETCORE_ENVIRONMENT=Development \
        $FULL_IMAGE_NAME
    
    print_success "Container started successfully"
    print_status "Access the application at: http://localhost:5000"
    print_status "Container name: unsinfra-ui-standalone"
    
    # Show container status
    sleep 3
    if docker ps -f name=unsinfra-ui-standalone --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep unsinfra-ui-standalone; then
        print_success "Container is running healthy"
    else
        print_error "Container may have failed to start"
        print_status "Check logs with: docker logs unsinfra-ui-standalone"
    fi
fi

print_success "Build process completed!"

# Show useful commands
echo ""
print_status "Useful commands:"
echo "  Run container:        docker run -d -p 5000:8080 --name unsinfra-ui $FULL_IMAGE_NAME"
echo "  View logs:            docker logs -f unsinfra-ui-standalone"
echo "  Stop container:       docker stop unsinfra-ui-standalone"
echo "  Remove container:     docker rm unsinfra-ui-standalone"
echo "  Shell into container: docker exec -it unsinfra-ui-standalone /bin/bash"
echo "  Use compose:          docker-compose up -d"