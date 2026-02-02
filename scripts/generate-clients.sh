#!/bin/bash
set -euo pipefail

# =============================================================================
# API Client Generation Script
# =============================================================================
# Generates strongly-typed API clients from the OpenAPI specification
#
# Usage:
#   ./scripts/generate-clients.sh [typescript|csharp|python|all]
#
# Prerequisites:
#   - Service must be running (http://localhost:8080)
#   - npx/npm installed for TypeScript generation
#   - NSwag CLI for C# generation: dotnet tool install -g NSwag.ConsoleCore
#   - OpenAPI Generator for Python: npm install -g @openapitools/openapi-generator-cli
# =============================================================================

API_URL="${API_URL:-http://localhost:8080}"
SWAGGER_JSON="$API_URL/swagger/v1/swagger.json"
OUTPUT_DIR="${OUTPUT_DIR:-./generated}"

CLIENT_TYPE="${1:-all}"

echo "🔍 Checking if API is running at $API_URL..."
if ! curl -f -s "$API_URL/health" > /dev/null; then
    echo "❌ API is not running at $API_URL"
    echo "   Start the service with: dotnet run --project src/Template.Api"
    exit 1
fi

echo "✅ API is running"
echo "📡 Fetching OpenAPI spec from $SWAGGER_JSON..."

mkdir -p "$OUTPUT_DIR"

# =============================================================================
# TypeScript Client Generation
# =============================================================================
generate_typescript() {
    echo ""
    echo "📦 Generating TypeScript client..."

    npx @openapitools/openapi-generator-cli generate \
        -i "$SWAGGER_JSON" \
        -g typescript-axios \
        -o "$OUTPUT_DIR/typescript" \
        --additional-properties=supportsES6=true,npmName=template-api-client,npmVersion=1.0.0

    echo "✅ TypeScript client generated at: $OUTPUT_DIR/typescript"
    echo "   Install with: cd $OUTPUT_DIR/typescript && npm install"
}

# =============================================================================
# C# Client Generation
# =============================================================================
generate_csharp() {
    echo ""
    echo "📦 Generating C# client..."

    if ! command -v nswag &> /dev/null; then
        echo "⚠️  NSwag not found. Installing..."
        dotnet tool install -g NSwag.ConsoleCore
    fi

    nswag openapi2csclient \
        /input:"$SWAGGER_JSON" \
        /output:"$OUTPUT_DIR/csharp/TemplateApiClient.cs" \
        /namespace:Template.Client \
        /generateClientInterfaces:true \
        /generateExceptionClasses:true \
        /exceptionClass:ApiException \
        /useBaseUrl:false

    echo "✅ C# client generated at: $OUTPUT_DIR/csharp/TemplateApiClient.cs"
}

# =============================================================================
# Python Client Generation
# =============================================================================
generate_python() {
    echo ""
    echo "📦 Generating Python client..."

    npx @openapitools/openapi-generator-cli generate \
        -i "$SWAGGER_JSON" \
        -g python \
        -o "$OUTPUT_DIR/python" \
        --additional-properties=packageName=template_api_client,projectName=template-api-client

    echo "✅ Python client generated at: $OUTPUT_DIR/python"
    echo "   Install with: cd $OUTPUT_DIR/python && pip install -e ."
}

# =============================================================================
# Main
# =============================================================================
case "$CLIENT_TYPE" in
    typescript|ts)
        generate_typescript
        ;;
    csharp|cs)
        generate_csharp
        ;;
    python|py)
        generate_python
        ;;
    all)
        generate_typescript
        generate_csharp
        generate_python
        ;;
    *)
        echo "❌ Unknown client type: $CLIENT_TYPE"
        echo "   Valid options: typescript, csharp, python, all"
        exit 1
        ;;
esac

echo ""
echo "🎉 Client generation complete!"
echo ""
echo "📚 Next steps:"
echo "   - Review generated clients in: $OUTPUT_DIR"
echo "   - Add to your project's dependencies"
echo "   - See docs/SWAGGER.md for usage examples"
