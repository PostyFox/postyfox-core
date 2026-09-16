FROM python:3.12-alpine
COPY app/server.py /app/server.py
EXPOSE 8090
USER 65534:65534
CMD ["python3", "/app/server.py"]
