@echo off
echo -----------------------------------------------
echo Starting Grafana on http://localhost:3000
echo Default login: admin / admin
echo Running Grafana container...
docker run -d --name grafana -p 3000:3000 grafana/grafana
echo Grafana is now running. Visit your browser to access it.
echo -----------------------------------------------
pause

