@echo off
echo -----------------------------------------------
echo Starting Prometheus on http://localhost:9090
echo Make sure your prometheus.yml is at C:\prometheus.yml
echo Running Prometheus container...
docker run -d --rm -v C:/prometheus.yml:/prometheus/prometheus.yml -p 9090:9090 prom/prometheus --enable-feature=otlp-write-receive
echo Prometheus is now running. Access it via your browser.
echo -----------------------------------------------
pause

