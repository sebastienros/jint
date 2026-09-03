"""A WebSocket client just big enough for the Chrome DevTools Protocol.

The Chrome DevTools Protocol is JSON over one ``ws://`` connection, and every published Python client for
it carries a dependency chain (``websockets``, ``websocket-client``, ``aiohttp``) that ``wpt run`` would
have to install into the virtualenv it builds for itself.  This is the whole of RFC 6455 that a loopback
CDP connection uses: the opening handshake, text frames, continuations, ping/pong and close.  There is no
TLS, no permessage-deflate and no server role, because none of the three would ever run.

The one rule that is easy to get wrong and impossible to see when it is: **a client masks every frame it
sends**.  A server is required to fail the connection on an unmasked frame, so an unmasked send does not
look like a protocol mistake, it looks like the browser hung up.
"""

from __future__ import annotations

import base64
import hashlib
import os
import socket
import struct
from urllib.parse import urlsplit

__all__ = ["WebSocket", "WebSocketError"]

#: RFC 6455 §4.2.2's constant, appended to the client's key before hashing.
_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

_OP_CONTINUATION = 0x0
_OP_TEXT = 0x1
_OP_BINARY = 0x2
_OP_CLOSE = 0x8
_OP_PING = 0x9
_OP_PONG = 0xA


class WebSocketError(Exception):
    """Raised when the connection cannot be made, or ends before the caller is done with it."""


class WebSocket:
    """One client connection.  Not thread safe for sending; :meth:`recv` is meant for a single reader."""

    def __init__(self, url: str, connect_timeout: float = 30.0) -> None:
        parts = urlsplit(url)
        if parts.scheme != "ws":
            raise WebSocketError(f"only ws:// is supported, got {url!r}")

        host = parts.hostname or "127.0.0.1"
        port = parts.port or 80
        path = parts.path or "/"
        if parts.query:
            path += "?" + parts.query

        self.url = url
        self._closed = False
        self._buffer = b""

        try:
            self._socket = socket.create_connection((host, port), timeout=connect_timeout)
        except OSError as error:
            raise WebSocketError(f"cannot connect to {host}:{port}: {error}") from error

        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self._handshake(host, port, path, connect_timeout)
        # The reader blocks indefinitely from here on; callers bound a wait with their own deadline and
        # close the socket to unblock it, which is what makes a hung page a timeout rather than a hang.
        self._socket.settimeout(None)

    # -- the opening handshake ------------------------------------------------------------------------

    def _handshake(self, host: str, port: int, path: str, timeout: float) -> None:
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        request = (
            f"GET {path} HTTP/1.1\r\n"
            f"Host: {host}:{port}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n"
            "\r\n"
        )
        self._socket.settimeout(timeout)
        self._socket.sendall(request.encode("ascii"))

        head = self._read_until(b"\r\n\r\n")
        status_line, _, header_block = head.partition(b"\r\n")
        if b" 101 " not in status_line:
            raise WebSocketError(f"upgrade refused: {status_line.decode('latin-1', 'replace')}")

        headers = {}
        for line in header_block.split(b"\r\n"):
            name, _, value = line.partition(b":")
            if name:
                headers[name.strip().lower().decode("latin-1")] = value.strip().decode("latin-1")

        expected = base64.b64encode(hashlib.sha1((key + _GUID).encode("ascii")).digest()).decode("ascii")
        if headers.get("sec-websocket-accept") != expected:
            raise WebSocketError("server did not echo the key; this is not a WebSocket endpoint")

    def _read_until(self, terminator: bytes) -> bytes:
        while terminator not in self._buffer:
            chunk = self._socket.recv(4096)
            if not chunk:
                raise WebSocketError("connection closed during the opening handshake")
            self._buffer += chunk

        head, _, rest = self._buffer.partition(terminator)
        self._buffer = rest
        return head

    # -- frames ---------------------------------------------------------------------------------------

    def _read_exactly(self, count: int) -> bytes:
        while len(self._buffer) < count:
            chunk = self._socket.recv(max(4096, count - len(self._buffer)))
            if not chunk:
                raise WebSocketError("connection closed by the browser")
            self._buffer += chunk

        head, self._buffer = self._buffer[:count], self._buffer[count:]
        return head

    def _read_frame(self):
        header = self._read_exactly(2)
        final = bool(header[0] & 0x80)
        opcode = header[0] & 0x0F
        masked = bool(header[1] & 0x80)
        length = header[1] & 0x7F

        if length == 126:
            (length,) = struct.unpack("!H", self._read_exactly(2))
        elif length == 127:
            (length,) = struct.unpack("!Q", self._read_exactly(8))

        mask = self._read_exactly(4) if masked else b""
        payload = self._read_exactly(length) if length else b""

        if masked:
            payload = bytes(byte ^ mask[index % 4] for index, byte in enumerate(payload))

        return final, opcode, payload

    def _send_frame(self, opcode: int, payload: bytes) -> None:
        if self._closed:
            raise WebSocketError("send on a closed connection")

        length = len(payload)
        header = bytearray()
        header.append(0x80 | opcode)

        if length < 126:
            header.append(0x80 | length)
        elif length < 0x10000:
            header.append(0x80 | 126)
            header += struct.pack("!H", length)
        else:
            header.append(0x80 | 127)
            header += struct.pack("!Q", length)

        mask = os.urandom(4)
        header += mask
        masked = bytes(byte ^ mask[index % 4] for index, byte in enumerate(payload))

        try:
            self._socket.sendall(bytes(header) + masked)
        except OSError as error:
            raise WebSocketError(f"send failed: {error}") from error

    # -- the caller's view ----------------------------------------------------------------------------

    def send_text(self, text: str) -> None:
        """Sends one text message."""
        self._send_frame(_OP_TEXT, text.encode("utf-8"))

    def recv(self) -> str:
        """Blocks until one whole text message arrives, answering pings and refusing binary ones."""
        while True:
            final, opcode, payload = self._read_frame()

            if opcode == _OP_PING:
                self._send_frame(_OP_PONG, payload)
                continue
            if opcode == _OP_PONG:
                continue
            if opcode == _OP_CLOSE:
                self._closed = True
                raise WebSocketError("the browser closed the connection")
            if opcode == _OP_BINARY:
                raise WebSocketError("binary frame on a JSON protocol")
            if opcode not in (_OP_TEXT, _OP_CONTINUATION):
                raise WebSocketError(f"unknown opcode {opcode}")

            message = payload
            while not final:
                final, opcode, payload = self._read_frame()
                if opcode not in (_OP_CONTINUATION, _OP_PING, _OP_PONG):
                    raise WebSocketError(f"opcode {opcode} interleaved with a fragmented message")
                if opcode == _OP_PING:
                    self._send_frame(_OP_PONG, payload)
                    final = False
                    continue
                if opcode == _OP_PONG:
                    final = False
                    continue
                message += payload

            return message.decode("utf-8")

    def close(self) -> None:
        """Sends a close frame if it still can, then drops the socket either way."""
        if self._closed:
            return

        try:
            self._send_frame(_OP_CLOSE, struct.pack("!H", 1000))
        except (WebSocketError, OSError):
            pass
        finally:
            self._closed = True
            try:
                self._socket.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            self._socket.close()
