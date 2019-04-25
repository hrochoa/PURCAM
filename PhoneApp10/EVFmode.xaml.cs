using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Devices;
using System.IO;
using System.IO.IsolatedStorage;
using Microsoft.Xna.Framework.Media;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace PhoneApp10
{
    public partial class EVFmode : PhoneApplicationPage
    {

        //Variables
        private int savedCounter = 0;
        PhotoCamera cam;
        MediaLibrary library = new MediaLibrary();

        //Holds current flash mode
        private string currentFlashMode;

        //Holds current resolution index
        int currentResIndex = 0;


        public EVFmode()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {

            //Check to see if camera is available on device
            if ((PhotoCamera.IsCameraTypeSupported(CameraType.Primary) == true))
            {
                // Initialize the camera, when available

                //Use standard camera on back of device
                cam = new Microsoft.Devices.PhotoCamera(CameraType.Primary);

                // Event is fired when the PhotoCamera object has been initialized.
                cam.Initialized += new EventHandler<Microsoft.Devices.CameraOperationCompletedEventArgs>(cam_Initialized);

                // Event is fired when the capture sequence is complete.
                cam.CaptureCompleted += new EventHandler<CameraOperationCompletedEventArgs>(cam_CaptureCompleted);

                // Event is fired when the capture sequence is complete and an image is available.
                cam.CaptureImageAvailable += new EventHandler<Microsoft.Devices.ContentReadyEventArgs>(cam_CaptureImageAvailable);

                // Event is fired when the capture sequence is complete and a thumbnail image is available.
                cam.CaptureThumbnailAvailable += new EventHandler<ContentReadyEventArgs>(cam_CaptureThumbnailAvailable);

                //Event fired when shutter button receives a full press
                CameraButtons.ShutterKeyPressed += OnButtonFullPress;

                //Set VideoBrush source to camera
                viewfinderBrush.SetSource(cam);
            }
            else
            {
                //Camera not supported
                this.Dispatcher.BeginInvoke(delegate()
                {
                    //Write message to UI
                    txtDebug.Text = "A Camera is not available on this device.";
                });

                //Disable UI
                ShutterButton.IsEnabled = false;
                FlashButton.IsEnabled = false;
                ResButton.IsEnabled = false;
            }
        }

        protected override void OnNavigatingFrom(System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (cam != null)
            {
                //Close camera
                cam.Dispose();

                //Release memory
                cam.Initialized -= cam_Initialized;
                cam.CaptureCompleted -= cam_CaptureCompleted;
                cam.CaptureImageAvailable -= cam_CaptureImageAvailable;
                cam.CaptureThumbnailAvailable -= cam_CaptureThumbnailAvailable;
                CameraButtons.ShutterKeyPressed -= OnButtonFullPress;

            }
        }

        //Update UI if initialization succeeds
        void cam_Initialized(object sender, Microsoft.Devices.CameraOperationCompletedEventArgs e)
        {
            if (e.Succeeded)
            {
                this.Dispatcher.BeginInvoke(delegate()
                {
                    //Write message
                    txtDebug.Text = "Camera initialized.";

                    //Set flash button text
                    FlashButton.Content = "Fl:" + cam.FlashMode.ToString();
                });
            }
        }

        //Ensure that viewfinder is upright in LandscapeRight
        protected override void OnOrientationChanged(OrientationChangedEventArgs e)
        {
            if (cam != null)
            {
                //LandscapeRight rotation when camera is on back of device
                int landscapeRightRotation = 180;

                //Rotate video brush from camera
                if (e.Orientation == PageOrientation.LandscapeRight)
                {
                    //Rotate for LandscapeRight orientation
                    viewfinderBrush.RelativeTransform =
                        new CompositeTransform() { CenterX = 0.5, CenterY = 0.5, Rotation = landscapeRightRotation };
                }
                else
                {
                    //Rotate for standard landscape orientation
                    viewfinderBrush.RelativeTransform =
                        new CompositeTransform() { CenterX = 0.5, CenterY = 0.5, Rotation = 0 };
                }
            }

            base.OnOrientationChanged(e);
        }

        private void ShutterButton_Click(object sender, RoutedEventArgs e)
        {
            if (cam != null)
            {
                try
                {
                    //Start image capture
                    cam.CaptureImage();
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(delegate()
                    {
                        //Can't capture until the previous capture completed
                        txtDebug.Text = ex.Message;
                    });
                }
            }
        }

        void cam_CaptureCompleted(object sender, CameraOperationCompletedEventArgs e)
        {
            //Increments savedCounter variable used for generating JPEG file names
            savedCounter++;
        }

        //Informs when full resolution picture has been taken and stores it
        void cam_CaptureImageAvailable(object sender, Microsoft.Devices.ContentReadyEventArgs e)
        {
            string fileName = savedCounter + ".jpg";

            try
            {   //Write message to the UI
                Deployment.Current.Dispatcher.BeginInvoke(delegate()
                {
                    txtDebug.Text = "Captured image available, saving picture...";
                });

                //Save picture camera roll
                library.SavePictureToCameraRoll(fileName, e.ImageStream);

                //Write message UI
                Deployment.Current.Dispatcher.BeginInvoke(delegate()
                {
                    txtDebug.Text = "Picture has been saved to camera roll...";
                });

                //Set position of stream back to start
                e.ImageStream.Seek(0, SeekOrigin.Begin);

                //Save picture as JPEG
                using (IsolatedStorageFile isStore = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (IsolatedStorageFileStream targetStream = isStore.OpenFile(fileName, FileMode.Create, FileAccess.Write))
                    {
                        //Initialize buffer for 4KB disk pages
                        byte[] readBuffer = new byte[4096];
                        int bytesRead = -1;

                        //Store image 
                        while ((bytesRead = e.ImageStream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                        {
                            targetStream.Write(readBuffer, 0, bytesRead);
                        }
                    }
                }

                //Write message to UI 
                Deployment.Current.Dispatcher.BeginInvoke(delegate()
                {
                    txtDebug.Text = "Picture taken";
                });
            }
            finally
            {
                //Close image stream
                e.ImageStream.Close();
            }

        }

        //Informs when thumbnail picture has been taken, then store it
        //User selects image in photos for full res 
        public void cam_CaptureThumbnailAvailable(object sender, ContentReadyEventArgs e)
        {
            string fileName = savedCounter + "_th.jpg";

            try
            {
                //Write message UI
                Deployment.Current.Dispatcher.BeginInvoke(delegate()
                {
                    txtDebug.Text = "Captured image available, saving thumbnail.";
                });

                //Save thumbnail as JPEG storage
                using (IsolatedStorageFile isStore = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (IsolatedStorageFileStream targetStream = isStore.OpenFile(fileName, FileMode.Create, FileAccess.Write))
                    {
                        //Initialize the buffer for 4KB disk pages
                        byte[] readBuffer = new byte[4096];
                        int bytesRead = -1;

                        //Copy the thumbnail to storage 
                        while ((bytesRead = e.ImageStream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                        {
                            targetStream.Write(readBuffer, 0, bytesRead);
                        }
                    }
                }

                //Write message to UI
                Deployment.Current.Dispatcher.BeginInvoke(delegate()
                {
                    txtDebug.Text = "Thumbnail has been saved to isolated storage.";
                });
            }
            finally
            {
                //Close image stream
                e.ImageStream.Close();
            }
        }

        //Activate flash
        //Cycle through flash mode options when flash button pressed
        private void changeFlash_Clicked(object sender, RoutedEventArgs e)
        {

            switch (cam.FlashMode)
            {
                case FlashMode.Off:
                    if (cam.IsFlashModeSupported(FlashMode.On))
                    {
                        //Specify that flash should be used
                        cam.FlashMode = FlashMode.On;
                        FlashButton.Content = "Fl:On";
                        currentFlashMode = "Flash mode: On";
                    }
                    break;
                case FlashMode.On:
                    if (cam.IsFlashModeSupported(FlashMode.RedEyeReduction))
                    {
                        //Specify that red-eye reduction flash to be used
                        cam.FlashMode = FlashMode.RedEyeReduction;
                        FlashButton.Content = "Fl:RER";
                        currentFlashMode = "Flash mode: RedEyeReduction";
                    }
                    else if (cam.IsFlashModeSupported(FlashMode.Auto))
                    {
                        //If red-eye reduction isn't supported, specify automatic mode
                        cam.FlashMode = FlashMode.Auto;
                        FlashButton.Content = "Fl:Auto";
                        currentFlashMode = "Flash mode: Auto";
                    }
                    else
                    {
                        //If automatic isn't supported, specify that no flash used
                        cam.FlashMode = FlashMode.Off;
                        FlashButton.Content = "Fl:Off";
                        currentFlashMode = "Flash mode: Off";
                    }
                    break;
                case FlashMode.RedEyeReduction:
                    if (cam.IsFlashModeSupported(FlashMode.Auto))
                    {
                        //Specify that flash used in automatic mode
                        cam.FlashMode = FlashMode.Auto;
                        FlashButton.Content = "Fl:Auto";
                        currentFlashMode = "Flash mode: Auto";
                    }
                    else
                    {
                        //If automatic is not supported, specify that no flash used
                        cam.FlashMode = FlashMode.Off;
                        FlashButton.Content = "Fl:Off";
                        currentFlashMode = "Flash mode: Off";
                    }
                    break;
                case FlashMode.Auto:
                    if (cam.IsFlashModeSupported(FlashMode.Off))
                    {
                        //Specify that no flash used
                        cam.FlashMode = FlashMode.Off;
                        FlashButton.Content = "Fl:Off";
                        currentFlashMode = "Flash mode: Off";
                    }
                    break;
            }

            //Display current flash mode
            this.Dispatcher.BeginInvoke(delegate()
            {
                txtDebug.Text = currentFlashMode;
            });
        }


        private void changeRes_Clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            //Variables
            IEnumerable<Size> resList = cam.AvailableResolutions;
            int resCount = resList.Count<Size>();
            Size res;

            //Poll for available camera resolutions
            for (int i = 0; i < resCount; i++)
            {
                res = resList.ElementAt<Size>(i);
            }

            //Set camera resolution
            res = resList.ElementAt<Size>((currentResIndex + 1) % resCount);
            cam.Resolution = res;
            currentResIndex = (currentResIndex + 1) % resCount;

            //Update UI
            txtDebug.Text = String.Format("Setting capture resolution: {0}x{1}", res.Width, res.Height);
            ResButton.Content = "R" + res.Width;
        }

        //Capture image with a full button press using the hardware shutter button
        private void OnButtonFullPress(object sender, EventArgs e)
        {
            if (cam != null)
            {
                cam.CaptureImage();
            }
        }

    }
}