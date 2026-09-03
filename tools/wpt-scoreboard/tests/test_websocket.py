"""The frame codec, over a socket pair, with no browser and no server.

The three things that are silent when they are wrong: a client frame that is not masked (a conforming
server fails the connection, which looks like the browser hanging up), a length that took the wrong branch
of the 7/16/64-bit encoding, and a message split across continuation frames — which is exactly what a large
``Runtime.getProperties`` answer arrives as.
"""

from __future__ import annotations

import socket
import struct
import threading

import pytest

from wpt_jint_browser.websocket import WebSocket, WebSocketError


def _detached(sock: socket.socket) -> WebSocket:
    """A :class:`WebSocket` over an already-connected socket, skipping the HTTP upgrade."""
    connection = object.__new__(WebSocket)
    connection.url = "ws://socketpair/"
    connection._socket = sock
    connection._buffer = b""
    connection._closed = False
    return connection


def _server_frame(opcode: int, payload: bytes, final: bool = True) -> bytes:
    """One unmasked frame, which is what a server sends."""
    header = bytearray([(0x80 if final else 0x00) | opcode])
    if len(payload) < 126:
        header.append(len(payload))
    elif len(payload) < 0x10000:
        header.append(126)
        header += struct.pack("!H", len(payload))
    else:
        header.append(127)
        header += struct.pack("!Q", len(payload))
    return bytes(header) + payload


@pytest.fixture
def pair():
    left, right = socket.socketpair()
    try:
        yield left, right
    finally:
        left.close()
        right.close()


def test_reads_a_short_text_frame(pair):
    client, server = pair
    server.sendall(_server_frame(0x1, b'{"id":1}'))
    assert _detached(client).recv() == '{"id":1}'


@pytest.mark.parametrize("size", [125, 126, 200, 0x10000, 0x10001])
def test_reads_every_length_encoding(pair, size):
    client, server = pair
    payload = ("x" * size).encode("utf-8")

    def send() -> None:
        server.sendall(_server_frame(0x1, payload))

    # A payload past the socket buffer would block the sender, so it goes on a thread of its own.
    thread = threading.Thread(target=send, daemon=True)
    thread.start()
    assert _detached(client).recv() == payload.decode("utf-8")
    thread.join(timeout=10)


def test_reassembles_continuation_frames(pair):
    client, server = pair
    server.sendall(_server_frame(0x1, b'{"me', final=False))
    server.sendall(_server_frame(0x0, b'thod', final=False))
    server.sendall(_server_frame(0x0, b'":1}', final=True))
    assert _detached(client).recv() == '{"method":1}'


def test_answers_a_ping_before_the_message(pair):
    client, server = pair
    server.sendall(_server_frame(0x9, b"beat"))
    server.sendall(_server_frame(0x1, b"after"))

    connection = _detached(client)
    assert connection.recv() == "after"

    pong = server.recv(64)
    assert pong[0] == 0x8A, "the answer to a ping is a pong"
    assert pong[1] & 0x80, "a client masks every frame it sends, pongs included"


def test_a_close_frame_ends_the_connection(pair):
    client, server = pair
    server.sendall(_server_frame(0x8, struct.pack("!H", 1001)))

    with pytest.raises(WebSocketError, match="closed the connection"):
        _detached(client).recv()


def test_sent_frames_are_masked_and_decode_back(pair):
    client, server = pair
    _detached(client).send_text("hello")

    frame = server.recv(64)
    assert frame[0] == 0x81
    assert frame[1] & 0x80, "an unmasked client frame is a protocol violation the server must fail on"

    length = frame[1] & 0x7F
    assert length == 5
    mask, payload = frame[2:6], frame[6:6 + length]
    assert bytes(byte ^ mask[index % 4] for index, byte in enumerate(payload)) == b"hello"


def test_a_truncated_connection_is_an_error(pair):
    client, server = pair
    server.sendall(_server_frame(0x1, b"xxxxx")[:3])
    server.close()

    with pytest.raises(WebSocketError, match="closed by the browser"):
        _detached(client).recv()


def test_only_ws_urls_are_accepted():
    with pytest.raises(WebSocketError, match="only ws://"):
        WebSocket("wss://example.test/socket")
