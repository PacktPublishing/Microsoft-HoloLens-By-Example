
import threading
from .singleton import Singleton
import bpy
import time
import sys
import zmq
import queue
import json

class ObjectSyncService(metaclass=Singleton):

    def __init__(self):
        self._running = False

        self._send_loop_running = False
        self._receive_loop_running = False

        self._send_thread = None
        self._receive_thread = None

        self.observed_objects = []
        self._context = None
        self._sender = None
        self._receiver = None

        self.packet_queue = queue.Queue()

    @property
    def is_running(self):
        return self._running

    def start(self):
        if self.is_running:
            return

        self._running = True

        self._open_connections()

        self._send_thread = threading.Thread(target=self._send_loop)
        self._send_thread.start()

        self._receive_thread = threading.Thread(target=self._receive_loop)
        self._receive_thread.start()

    def stop(self):
        self._running = False

    def _open_connections(self):
        print("_open_connections")

        try:
            self._context = zmq.Context()

            self._sender = self._context.socket(zmq.PUSH)
            self._sender.bind("tcp://*:5557")

            self._receiver = self._context.socket(zmq.PULL)
            self._receiver.bind("tcp://*:5558")
        except:
            raise Exception('Unable to start Thread')

    def _close_connections(self):
        print("_close_connections")

        if not self.is_running:
            return

        if self._sender:
            self._sender.close()
            self._sender = None

        if self._receiver:
            self._receiver.close()
            self._receiver = None

    def enqueue_packet(self, packet):
        self.packet_queue.put(packet)

    def _send_loop(self):
        print("entering - _send_loop")

        self._send_loop_running = True

        while self.is_running:
            while not self.packet_queue.empty():
                packet = self.packet_queue.get()
                #self._sender.send_string(packet)
                self._sender.send(str.encode(packet))

            time.sleep(0.2)

        self._send_loop_running = False

        print("exiting - _send_loop")

    def _receive_loop(self):
        print("entering - _receive_loop")

        self._receive_loop_running = True

        while self.is_running:
            try:
                packet = self._receiver.recv()
                print(packet)
                packet = json.loads(packet)

                self.process_packet(packet)

                # TODO: handle received message

            except Exception as e:
                print("error while trying to receive message {}".format(e))

        self._receive_loop_running = False

        print("exiting - _receive_loop")

    def _on_received_packet(self, packet):
        print("_on_received_packet {}".format(packet))

