# WorkTimeTracker Docker Server

Lightweight self-hosted backend service for **WorkTimeTracker** running in Docker.

## Quick Start on Ubuntu

### 1. Clone & Configure
```bash
git clone https://github.com/Coeussss/TimeLogger.git
cd TimeLogger/server

# Copy sample environment configuration
cp .env.example .env

# Edit .env and set your secure API_KEY
nano .env
```

Example `.env`:
```ini
PORT=48291
API_KEY=my_custom_secret_passphrase_9876
```

### 2. Launch the Container
```bash
docker compose up -d --build
```

### 3. Verify Health
```bash
curl http://localhost:48291/api/health
# Output: {"status":"healthy","version":"1.0.0",...}
```

### 4. Router Port Forwarding
1. Log into your home router admin panel.
2. Find **Port Forwarding** / **Virtual Servers**.
3. Add a new rule:
   - **External Port**: `48291`
   - **Internal IP**: Your Ubuntu server's local IP (e.g. `192.168.1.150`)
   - **Internal Port**: `48291`
   - **Protocol**: `TCP`
4. Use your public IP or Dynamic DNS hostname (e.g. `http://myhome.ddns.net:48291`) in the Windows and Android apps.

## API Endpoints

| Method | Path | Auth Required? | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/health` | No | Health check |
| `GET` | `/api/logs` | `X-API-Key` | Fetch all time logs |
| `POST` | `/api/logs` | `X-API-Key` | Add or update logs |
| `POST` | `/api/logs/sync` | `X-API-Key` | Bidirectional client-server sync |
| `DELETE` | `/api/logs/{id}` | `X-API-Key` | Delete a log by GUID |
| `GET` | `/api/summary/today` | `X-API-Key` | Get today's 7.5h progress |

## Data Persistence
Logs are stored in `./data/timelogs.json` on the host, which is mounted into `/app/data/` inside the container.
