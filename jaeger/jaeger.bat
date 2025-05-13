@echo off
echo -----------------------------------------------
echo Starting Jaeger on http://localhost:65209
echo This will collect traces (OpenTelemetry / Zipkin)
echo Running Jaeger container...
docker run --rm -d --name jaeger -e COLLECTOR_ZIPKIN_HOST_PORT=:9411 -p 65209:16686 -p 4317:4317 -p 4318:4318 -p 9411:9411 jaegertracing/all-in-one:latest
echo Jaeger is now running. Open your browser to see traces.
echo -----------------------------------------------
pause
