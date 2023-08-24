from pypylon import pylon
import cv2
import numpy as np
from os.path import isfile
import logging
logger = logging.getLogger(__name__)
# disabled logger and switched to print because it wasn't coming out for some reason


CAMERA_SERIALS = {
    "cam1": "24499505"
}

from dataclasses import dataclass
@dataclass
class PylonCamera:
    def __init__(self, name):
        if name not in CAMERA_SERIALS.keys():
            raise ValueError(f'{name} not recognized. must be one of: {", ".join((CAMERA_SERIALS.keys()))}')
        self.name = name
        # could get a more detailed model dynamically probably
        self.model = "Basler"
        self.serial = CAMERA_SERIALS[self.name]
        self.param_file = f'{self.model}_{self.serial}_params.npz'
        self.apriltag_params = self._load_apriltag_params()
        self.camera = pylon.InstantCamera(pylon.TlFactory.GetInstance().CreateFirstDevice())
        self.camera.Open()
        self.camera.StartGrabbing()

    # only call after self.model and self.name have been set
    def _load_apriltag_params(self):
        if isfile(self.param_file):
            print(f'Loading camera parameters from {self.param_file}')
            params = np.load(self.param_file)
            return params['mtx'][[0,1,0,1], [0,1,2,2]]
        else:
            print("No camera calibration found, using estimated parameters")
            # hard-coded estimate based on lens focal length and sensor size
            return estimate_camera_intrinsics([6.9, 5.5], [1280, 1024], 3.5)

    def get_frame(self):
        res = self.camera.RetrieveResult(5000, pylon.TimeoutHandling_ThrowException)
        try:
            if res.GrabSucceeded():
                return res.Array.copy()
            print("Camera grab failed")
            return None
        finally:
            res.Release()

    def close(self):
        self.camera.StopGrabbing()
        self.camera.Close()

@dataclass
class MacBookCamera:
    def __init__(self, idx):
        self.camera = cv2.VideoCapture(idx)
        self.model = "MacBookCamera"
        self.serial = "XXXX"
        self.param_file = 'MacBookCamera_params.npz'

        # extract f_x and f_y (focal length) and c_x and c_y (optical center)
        self.apriltag_params = np.load(self.param_file)['mtx'][[0,1,0,1], [0,1,2,2]]

    def get_frame(self):
        return cv2.cvtColor(self.camera.read()[1], cv2.COLOR_BGR2GRAY)

    def close(self):
        self.camera.release()

# we should calibrate to get accurate intrinsics, but this should give us a ballpark.
# we compute the focal length based on the lens focal length and sensor parameters,
# and assume the optical center is the same as the image center.
def estimate_camera_intrinsics(sensor_dims_mm, sensor_dims_px, focal_length_mm):
    return (focal_length_mm * sensor_dims_px[0] / sensor_dims_mm[0],
            focal_length_mm * sensor_dims_px[1] / sensor_dims_mm[1],
            sensor_dims_px[0] / 2,
            sensor_dims_px[1] / 2)

def calibrate(cam, chessboard_squares):
    window_name = "Camera Calibration"
    calib_images = []
    while True:
        img = cam.get_frame()
        cv2.imshow(window_name, img)
        key_pressed = cv2.pollKey()
        if key_pressed & 0xff == ord(' '):
            found, corners = cv2.findChessboardCorners(img, chessboard_squares, None)
            if found:
                calib_images.append((img, corners))
                print(f'{len(calib_images)} frames captured')
            else:
                print("No chessboard found in image")
        elif key_pressed & 0xff == ord('q'):
            break


    # termination criteria
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 0.001)

    cb_x = chessboard_squares[0]
    cb_y = chessboard_squares[1]
    # prepare object points, like (0,0,0), (1,0,0), (2,0,0) ....,(6,5,0)
    objp = np.zeros((cb_x * cb_y, 3), np.float32)
    objp[:,:2] = np.mgrid[0:cb_x,0:cb_y].T.reshape(-1,2)

    # Arrays to store object points and image points from all the images.
    objpoints = [] # 3d point in real world space
    imgpoints = [] # 2d points in image plane.

    for (img, corners) in calib_images:
        # If found, add object points, image points (after refining them)
        objpoints.append(objp)

        corners2 = cv2.cornerSubPix(img, corners, (11,11), (-1,-1), criteria)
        imgpoints.append(corners2)

        # Draw and display the corners
        img = cv2.drawChessboardCorners(img, chessboard_squares, corners2, True)
        cv2.imshow(window_name, img)
        cv2.waitKey(1000)
    cv2.destroyAllWindows()
    cv2.pollKey()
    ret, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(objpoints, imgpoints, img.shape[::-1],None,None)
    if ret:
        np.savez(cam.param_file, mtx=mtx, dist=dist, rvecs=rvecs, tvecs=tvecs)
        print(f'wrote {cam.param_file}')

    else:
        print("Calibration failed")
