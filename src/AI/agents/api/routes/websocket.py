"""WebSocket routes for real-time communication.

Architecture
------------
The .NET Designer backend (AltinityProxyHub) opens a raw WebSocket to ``/ws``,
sends a ``{"type": "session", "session_id": "...", "developer": "..."}`` message,
and then listens for JSON frames that it forwards to the frontend via SignalR.

Authentication happens on the handshake, before ``accept()``:

* ``X-Altinity-Shared-Secret`` must match ``ALTINITY_AGENT_SHARED_SECRET``.
  The Designer backend is the only trusted client; it authenticates the end
  user itself and vouches for the identity it forwards.
* ``X-Developer`` names the developer whose events this connection may
  receive. The ``developer`` field in client frames is only accepted when it
  equals this handshake identity — it is never trusted on its own.
* A browser ``Origin`` header is rejected: this is a server-to-server socket.

This module:
1. Authenticates the handshake, accepts the WebSocket and waits for the
   ``session`` registration message.
2. Starts an **event-streaming loop** that reads from the per-**developer** event
   buffer in ``EventSink`` and sends each event as a JSON frame.
3. Concurrently listens for incoming messages (ping, more session registrations, etc.).

Key design: events are streamed by *developer*, not by *session*. This means
that after a page refresh (which creates a new connection-level session ID),
the WS still delivers all events for any workflow session belonging to that
developer. The session_id on each event lets the frontend route it correctly.

No callbacks are used — the WebSocket handler *pulls* from the buffer.
Reconnection after a page reload simply replays all buffered events.
"""
import asyncio
import hmac
import logging
from typing import Optional

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from starlette.websockets import WebSocketState
from agents.services.events import sink
from shared.config import get_config

logger = logging.getLogger(__name__)
config = get_config()

SHARED_SECRET_HEADER = "X-Altinity-Shared-Secret"
DEVELOPER_HEADER = "X-Developer"
ORIGIN_HEADER = "Origin"
WS_CLOSE_POLICY_VIOLATION = 1008


def authenticate_websocket_handshake(ws: WebSocket) -> Optional[str]:
    """Return the verified developer for *ws*, or ``None`` if the handshake is not trusted.

    Fails closed: without a configured shared secret every connection is refused.
    """
    expected_secret = config.ALTINITY_AGENT_SHARED_SECRET
    if not expected_secret:
        logger.error(
            "ALTINITY_AGENT_SHARED_SECRET is not configured; refusing WebSocket connection"
        )
        return None

    if ws.headers.get(ORIGIN_HEADER):
        logger.warning("Refusing WebSocket with browser Origin header (server-to-server only)")
        return None

    presented_secret = ws.headers.get(SHARED_SECRET_HEADER, "")
    if not hmac.compare_digest(presented_secret.encode(), expected_secret.encode()):
        logger.warning("Refusing WebSocket with missing or invalid shared secret")
        return None

    developer = (ws.headers.get(DEVELOPER_HEADER) or "").strip()
    if not developer:
        logger.warning(f"Refusing WebSocket without {DEVELOPER_HEADER} header")
        return None

    return developer


def is_registration_for_developer(data: dict, developer: str) -> bool:
    """A ``session`` frame may omit ``developer`` or repeat the authenticated one — nothing else."""
    requested = data.get("developer")
    return requested is None or requested == developer


async def _safe_send_json(ws: WebSocket, data: dict) -> bool:
    """Send JSON over *ws*. Returns False if the socket is gone."""
    try:
        if ws.client_state != WebSocketState.CONNECTED:
            return False
        await ws.send_json(data)
        return True
    except Exception:
        return False


async def _stream_developer_events(ws: WebSocket, developer: str):
    """Read all events for *developer* and push them over *ws*.

    Streams indefinitely — new workflow sessions for the same developer
    are automatically included because events fan out to the developer buffer.
    Only stops when the WebSocket disconnects.
    """
    cursor = sink.developer_event_count(developer)  # start from current tail (skip already-sent history)
    logger.info(f"🎬 _stream_developer_events started for developer {developer} (cursor={cursor})")

    while True:
        new_events = sink.get_developer_events_since(developer, cursor)
        if new_events:
            logger.info(
                f"📦 Found {len(new_events)} new events (cursor={cursor}) for developer {developer}"
            )

        for event in new_events:
            ok = await _safe_send_json(ws, event.model_dump())
            if not ok:
                logger.info(
                    f"🔌 WS closed while streaming event {event.type} "
                    f"session={event.session_id} developer={developer}"
                )
                return
            logger.info(
                f"✅ WS sent: type={event.type}, session={event.session_id}, developer={developer}"
            )
            cursor += 1

        try:
            logger.debug(f"⏳ Waiting for developer events (cursor={cursor}) developer={developer}")
            got_new = await sink.wait_for_developer_events(developer, known_count=cursor, timeout=30.0)
            if not got_new:
                logger.debug(f"⏰ Wait timed out (cursor={cursor}) developer={developer}, looping")
        except Exception as e:
            logger.warning(f"Wait error for developer {developer}: {e}")


async def _receive_initial_registration(ws: WebSocket, developer: str) -> Optional[str]:
    """Wait for the first ``session`` registration message for *developer*.

    Returns the registered ``session_id`` (possibly empty), or ``None`` on
    disconnect or when the frame names a different developer.
    """
    try:
        while True:
            data = await ws.receive_json()
            msg_type = data.get("type")

            if msg_type == "ping":
                await _safe_send_json(ws, {
                    "type": "pong",
                    "timestamp": data.get("timestamp"),
                })
            elif msg_type == "session":
                if not is_registration_for_developer(data, developer):
                    logger.warning(
                        f"Registration developer mismatch for authenticated developer {developer}"
                    )
                    return None
                return data.get("session_id") or ""
    except (WebSocketDisconnect, Exception):
        return None


def register_websocket_routes(app: FastAPI):
    """Register WebSocket routes on the FastAPI app."""

    @app.websocket("/ws")
    async def websocket_endpoint(websocket: WebSocket):
        developer = authenticate_websocket_handshake(websocket)
        if developer is None:
            await websocket.close(code=WS_CLOSE_POLICY_VIOLATION)
            return

        stream_task = None

        try:
            await websocket.accept()
            logger.info(f"🔗 WebSocket connected for developer {developer}")

            await _safe_send_json(websocket, {
                "type": "connection",
                "status": "connected",
                "message": "WebSocket connection established",
            })

            # --- Phase 1: wait for initial registration -----------------------
            session_id = await _receive_initial_registration(websocket, developer)
            if session_id is None:
                logger.info(f"🔌 WebSocket closed before session registration (developer={developer})")
                await websocket.close(code=WS_CLOSE_POLICY_VIOLATION)
                return

            sink.register_developer_session(developer, session_id)
            logger.info(f"📋 Developer registered: {developer}, initial session: {session_id}")
            await _safe_send_json(websocket, {
                "type": "session",
                "status": "registered",
                "session_id": session_id,
                "developer": developer,
            })

            # --- Phase 2: stream ALL developer events + keep reading ----------
            # The stream never restarts on new session registrations — it delivers
            # events for any session belonging to this developer.
            stream_task = asyncio.create_task(_stream_developer_events(websocket, developer))

            try:
                while True:
                    data = await websocket.receive_json()
                    msg_type = data.get("type")

                    if msg_type == "ping":
                        await _safe_send_json(websocket, {
                            "type": "pong",
                            "timestamp": data.get("timestamp"),
                        })
                    elif msg_type == "session":
                        if not is_registration_for_developer(data, developer):
                            logger.warning(
                                f"Ignoring session registration for another developer "
                                f"(authenticated developer={developer})"
                            )
                            continue
                        new_session_id = data.get("session_id")
                        if new_session_id:
                            sink.register_developer_session(developer, new_session_id)
                            logger.info(
                                f"📋 Additional session registered: {new_session_id} "
                                f"-> developer {developer}"
                            )
                            await _safe_send_json(websocket, {
                                "type": "session",
                                "status": "registered",
                                "session_id": new_session_id,
                                "developer": developer,
                            })
            except (WebSocketDisconnect, Exception):
                pass

        except Exception as e:
            logger.error(f"WebSocket error: {e}")

        finally:
            if stream_task and not stream_task.done():
                stream_task.cancel()
                try:
                    await stream_task
                except (asyncio.CancelledError, Exception):
                    pass
            logger.info(f"🔌 WebSocket disconnected (developer={developer})")

    @app.get("/api/ws/status")
    async def get_websocket_status():
        """Get WebSocket connection status."""
        return {"status": "ok"}
