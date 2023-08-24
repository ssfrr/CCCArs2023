#! /usr/bin/env python

import cv2

from cameras import PylonCamera, MacBookCamera, calibrate
import logging
logger = logging.getLogger(__name__)

cam = PylonCamera('cam1')
calibrate(cam, (9, 6))
