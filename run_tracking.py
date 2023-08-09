#! /usr/bin/env python
from pypylon import pylon
import cv2
import numpy as np
import time
from pupil_apriltags import Detector
from pythonosc import udp_client
import logging

from cameras import PylonCamera, MacBookCamera

logger = logging.getLogger(__name__)

# lots of openCV API's want int32s
def to_i32(x):
    return np.rint(x).astype(np.int32)


at_detector = Detector(families="tagStandard41h12", quad_decimate=2)
cam = PylonCamera("cam1")
#cam = MacBookCamera(0)
TRACKING_TAG_SIZE = 0.05

prev_frame_end = None
fps_smooth = None
load_smooth = None
font = cv2.FONT_HERSHEY_SIMPLEX
frames_processed = 0
osc = udp_client.SimpleUDPClient("127.0.0.1", 53330)
while(True):
    # Capture the video frame
    while True:
        img = cam.get_frame()
        if img is not None:
            break

    detect_start = time.time()
    # tag size is the dimensions of the outer black square (not including the white padding space)
    markers = at_detector.detect(img, True, cam.apriltag_params, TRACKING_TAG_SIZE)
    detect_end = time.time()
    for m in markers:
        # print("==============")
        # print("sending rotation")
        # print(m.pose_R)
        if m.tag_id == 0:
            print("===========")
            print(m.pose_t)
        # the OSCCore C# library has a bug, where it skips the first argument if you have 3, so we
        # add a dummy argument here to work around it.
        osc.send_message(f'/{cam.name}/marker/{m.tag_id}/position', m.pose_t.ravel().tolist() + [0])
        osc.send_message(f'/{cam.name}/marker/{m.tag_id}/rotation', m.pose_R.ravel().tolist())
        corners = to_i32(m.corners)
        # put white background behind text so it's easier to read.
        id_str = f"{m.tag_id}"
        # get the text size so we can center it. apparently it doesn't take thickness into account,
        # so when you put text with multiple thicknesses on top of each other, things are aligned
        (xsize, ysize), _ = cv2.getTextSize(id_str, font, 2, 1)
        if m.hamming > 0:
            id_str += f' ({m.hamming} err)'
        for color, thickness in [((255, 255, 255), 10), ((0, 0, 0), 3)]:
            cv2.polylines(img,
                        # opencv wants a int32 array of shape (points, 1, 2)
                        [corners],
                        True,
                        color, thickness)
            # place the tag ID on the tag
            cv2.putText(img, id_str, to_i32(m.center + [-xsize/2, ysize/2]), font, 2, color, thickness)
    frame_end = time.time()
    # converge quickly and then smooth out so it's easier to read
    smooth_alpha = 0.8 if frames_processed < 50 else 0.99
    if prev_frame_end is not None:
        fps = 1 / (frame_end - prev_frame_end)
        fps_smooth = fps if fps_smooth is None else smooth_alpha * fps_smooth + (1-smooth_alpha) * fps
        cv2.putText(img, f"{fps_smooth:.1f}fps", (20, 50), font, 1, (255, 255, 255), 4)
        cv2.putText(img, f"{fps_smooth:.1f}fps", (20, 50), font, 1, (0, 0, 0), 1)

        load = (detect_end - detect_start) / (frame_end - prev_frame_end) * 100
        load_smooth = load if load_smooth is None else smooth_alpha * load_smooth + (1-smooth_alpha) * load
        cv2.putText(img, f"{load_smooth:.1f}% load", (20, 100), font, 1, (255, 255, 255), 4)
        cv2.putText(img, f"{load_smooth:.1f}% load", (20, 100), font, 1, (0, 0, 0), 1)
    prev_frame_end = frame_end
    frames_processed += 1
    cv2.imshow('img', img)

    # q to quit
    if cv2.pollKey() & 0xFF == ord('q'):
        break

cam.close()
# Destroy all the windows
cv2.destroyAllWindows()
