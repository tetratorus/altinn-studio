import pytest
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

from api.main import app
from api.routes import websocket as ws_module
from agents.services.events import sink

SECRET = "test-shared-secret"
DEVELOPER = "alice"


def _headers(secret: str = SECRET, developer: str = DEVELOPER) -> dict[str, str]:
    return {
        ws_module.SHARED_SECRET_HEADER: secret,
        ws_module.DEVELOPER_HEADER: developer,
    }


@pytest.fixture(autouse=True)
def shared_secret(monkeypatch):
    monkeypatch.setattr(ws_module.config, "ALTINITY_AGENT_SHARED_SECRET", SECRET)


def _register(ws, developer=None, session_id="session-1"):
    frame = {"type": "session", "session_id": session_id}
    if developer is not None:
        frame["developer"] = developer
    ws.send_json(frame)


class TestWebSocketHandshakeAuthentication:
    def test_rejects_connection_without_credentials(self):
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect("/ws"):
                pass

    def test_rejects_wrong_shared_secret(self):
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect("/ws", headers=_headers(secret="nope")):
                pass

    def test_rejects_missing_developer_header(self):
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect(
                "/ws", headers={ws_module.SHARED_SECRET_HEADER: SECRET}
            ):
                pass

    def test_rejects_browser_origin(self):
        headers = {**_headers(), "Origin": "http://studio.localhost"}
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect("/ws", headers=headers):
                pass

    def test_rejects_everything_when_secret_not_configured(self, monkeypatch):
        monkeypatch.setattr(ws_module.config, "ALTINITY_AGENT_SHARED_SECRET", None)
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect("/ws", headers=_headers()):
                pass

    def test_accepts_valid_credentials_and_binds_developer(self):
        with TestClient(app).websocket_connect("/ws", headers=_headers()) as ws:
            assert ws.receive_json()["type"] == "connection"
            _register(ws, developer=DEVELOPER)
            registered = ws.receive_json()
            assert registered == {
                "type": "session",
                "status": "registered",
                "session_id": "session-1",
                "developer": DEVELOPER,
            }
            assert sink.get_session_developer("session-1") == DEVELOPER


class TestDeveloperBinding:
    def test_initial_registration_for_other_developer_closes_connection(self):
        with pytest.raises(WebSocketDisconnect):
            with TestClient(app).websocket_connect("/ws", headers=_headers()) as ws:
                ws.receive_json()
                _register(ws, developer="victim", session_id="victim-session")
                ws.receive_json()
        assert sink.get_session_developer("victim-session") != DEVELOPER

    def test_later_registration_for_other_developer_is_ignored(self):
        with TestClient(app).websocket_connect("/ws", headers=_headers()) as ws:
            ws.receive_json()
            _register(ws, session_id="own-session")
            assert ws.receive_json()["status"] == "registered"

            _register(ws, developer="victim", session_id="spoofed-session")
            ws.send_json({"type": "ping", "timestamp": 1})
            assert ws.receive_json()["type"] == "pong"
        assert sink.get_session_developer("spoofed-session") is None
