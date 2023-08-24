#! /usr/bin/env python
from pypylon import pylon
import cv2
import numpy as np
import time
from pupil_apriltags import Detector
from pythonosc import udp_client
import logging
from dataclasses import dataclass

from cameras import PylonCamera, MacBookCamera

logger = logging.getLogger(__name__)

# lots of openCV API's want int32s
def to_i32(x):
    return np.rint(x).astype(np.int32)

# marker locations relative to the tag origin, which is the center of the tag.
# These use the AprilTag coordinate system:
# X-Left, Y-Forward, Z-Down (Right-Handed)
# The first coordinates are for the front marker, then move clockwise (when
# looking from above)
# Note this is not the same as the Unity coordinate system, which is:
# X-Right, Y-Up, Z-Forward (Left-Handed)
TAG_GEOM = np.array([[0.0,   -61.75, -7.5],
                     [61.75,  61.75, -12.5],
                     [0.0,    61.75, -37.5],
                     [-61.75, 61.75, -17.5]])

# multiply apriltag coordinates by this matrix to convert to Unity coordinates
APRIL_TO_UNITY = np.array([[-1,  0,  0,  0],
                           [ 0,  0, -1,  0],
                           [ 0,  1,  0,  0],
                           [ 0,  0,  0,  1]])
UNITY_TO_APRIL = np.array([[-1,  0,  0,  0],
                           [ 0,  0,  1,  0],
                           [ 0, -1,  0,  0],
                           [ 0,  0,  0,  1]])

assert all((APRIL_TO_UNITY @ UNITY_TO_APRIL == np.identity(4)).ravel())

# tag IDs we're using, in sorted order
TAG_IDS = [
    1,
]

# @dataclass
# class TrackerState:
#     active: dict = dict()
#     detector: Detector = Detector()

# def run_tracker(camera):
#     state = TrackerState()
#     while True:
#         # get camera image, handle it
#         pass


"""
Tracking Overview

- grab a frame from the camera
- preprocess frame (not sure if this helps or hurts AprilTag)
- for each apriltag found:
    - if the ID is not one that we're using, discard
    - maybe skip the following steps if this tag is active and we have a recent position for it
    - compute the bounding box of the apriltag, and expand by a factor based on
      the headset geometry
    - perform contour detection in the expanded bounding box, get contour centroids
    - perform PnP on the apriltag corners (use SolvePnPGeneric /w SOLVEPNP_IPPE_SQUARE to
      get both answers if ambiguous)
    - Using the apriltag pose, project marker locations to image and find the corresponding
      points from the contour detection.  See below for algo. If there were
      multiple pose candidates, pick the one with the best fit. It seems like
      you should be able to do this purely in screen-space coordinates by
      treating the apriltag edges as basis vectors, but if that's true then I
      don't know what happens when the pose is ambiguous.
    - create a Headset object /w screen-space marker and blob coordinates
    - add the Headset to a dictionary of active Headsets, keyed by the tag ID. This will
      replace any earlier headsets with the same ID
- for each active headset:
    - if we don't already have fresh screen-space coordinates (i.e. this was not a new headset):
        - find the bounding box of the previous markers
        - expand the bounding box (by a ratio based on frame rate, physical marker layout size,
          and max expected velocity)
        - perform contour tracking within the expanded bounding box
        - match up the new points with the previous points (see below for algo)
        - alternatively we could try to track each blob individually by looking in a small
          region around just that blob, but then it gets more complicated to handle the case
          where multiple blobs are near each other
    - now that we have the screen-space coordinates for the blobs (and/or apriltag corners),
      we can run SolvePnPGeneric to get the 3D pose. Several algorithms require exactly 4 points,
      some require coplanar and/or square points. This video shows P3P freaking out while
      AP3P is much more stable: https://www.youtube.com/watch?v=X5h_4okDzyI. This is with
      a 4-point square tag. The docs for the iterative method say "Initial
      solution for non-planar "objectPoints" needs at least 6 points", which means that you
      need 6 points if you are not supplying an initial guess, but after that, 4 points
      is OK.
    - send the tag ID and pose to unity over OSC

Finding correspondences between sets of points with a greedy search:
1. compute the pairwise squared distance matrix.
2. find the argmin
3. record the match and error, and set that row and column to Inf
4. Repeat from step 2 until:
    - we have all the points
    - we've run out of points
    - the distance exceeds some threshold

There's probably a way to avoid searching for the minimum each time by
pre-sorting once, but this should be very fast with the small numbers of points
we have.
"""

# TAG_FAMILY="tag25h9"
# TAG_FAMILY="tagStandard41h12"
TAG_FAMILY="tag36h11"
at_detector = Detector(families=TAG_FAMILY, quad_decimate=2)
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
