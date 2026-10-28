from flask import Flask, jsonify
import os
import socket
import psycopg2
from psycopg2.extras import RealDictCursor
import time

app = Flask(__name__)

DB_CONFIG = {
    "host": os.getenv("POSTGRES_HOST", "db"),
    "port": os.getenv("POSTGRES_PORT", "5432"),
    "user": os.getenv("POSTGRES_USER", "appuser"),
    "password": os.getenv("POSTGRES_PASSWORD", "supersecret"),
    "dbname": os.getenv("POSTGRES_DB", "appdb"),
}

def get_connection():
    for attempt in range(10):
        try:
            return psycopg2.connect(**DB_CONFIG)
        except psycopg2.OperationalError:
            time.sleep(2)
    raise RuntimeError("Не удалось подключиться к PostgreSQL")

@app.route("/")
def hello():
    conn = get_connection()
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("INSERT INTO visits (path) VALUES (%s) RETURNING id;", ("/",))
            visit_id = cur.fetchone()["id"]
            cur.execute("SELECT COUNT(*) AS total FROM visits;")
            total = cur.fetchone()["total"]
        conn.commit()
    finally:
        conn.close()

    return jsonify({
        "message": "Hello from Docker + Postgres!",
        "hostname": socket.gethostname(),
        "env": os.getenv("APP_ENV", "development"),
        "visit_id": visit_id,
        "total_visits": total,
    })

@app.route("/visits")
def visits():
    conn = get_connection()
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("SELECT id, path, created_at FROM visits ORDER BY id DESC LIMIT 20;")
            rows = cur.fetchall()
    finally:
        conn.close()
    return jsonify(rows)

@app.route("/health")
def health():
    try:
        conn = get_connection()
        conn.close()
        return jsonify({"status": "ok", "db": "connected"}), 200
    except Exception as e:
        return jsonify({"status": "error", "detail": str(e)}), 500

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
