/*
 * Copyright (c) 2019 Tanvas, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 *  * Redistributions of source code must retain the above copyright notice, this
 * list of conditions and the following disclaimer.
 *  * Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation and/or
 * other materials provided with the distribution.
 *  * Neither the name of Tanvas, Inc. nor the names of its contributors may be
 * used to endorse or promote products derived from this software without specific
 * prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
 * ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Tanvas.TanvasTouch.Resources;
using Tanvas.TanvasTouch.WpfUtilities;

namespace TanvasTouchHapticKnob
{
    /// <summary>
    /// The Haptic Knob takes care of all the business logic of the knob itself. It must be passed the appropriate controls and data
    /// upon creation, and it lives the life of the entire app.
    /// 
    /// The functions in here operate using angles in radian. Since the Y value on the Windows desktop grows DOWN from the top of the display, the
    /// circle is orientated such that (with 12:00 being the visual top of the knob):
    ///     * 0rad = 3:00 position.
    ///     * PI/2 = 6:00 position.
    ///     * PI = 9:00 position.
    ///     * 3PI/2 = 12:00 position.
    /// In summary: the radian increases in a clockwise direction.
    /// 
    /// Some terms:
    ///     * "Knob" is the name for the entire control.
    ///     * "Nub" is the part of the knob which the user "grabs" to rotate the knob.
    /// </summary>
    class HapticKnob
    {
        // Since we use this everywhere.
        private const double TWO_PI = (Math.PI * 2.0);

        // This sets the radius, in pixels, from the center of the knob to the center of the section markers
        // around the edge of the knob.
        private const int SECTION_RADIUS_PIXELS = 126;

        // The total number of sections on the knob; upon release, the knob will center itself in the current section.
        private const uint TOTAL_SECTIONS = 10;

        // The size of the arc of a single section, in radian.
        private const double SECTION_ARC_RADIAN = (TWO_PI / (double)TOTAL_SECTIONS);

        // Distance (in pixels) from the center of the nub for a touch-down to acquire the knob. This value is slightly larger
        // than the haptics, which means if someone finds the nub by feel, then raises and drops their finger to acquire it, even
        // if their finger drifts a bit from where they lifted it (which will always happen), they'll still be within the radius.
        private const int KNUB_ACQUIRE_RADIUS_PIXELS = 55;

        // The window which contains the knob.
        private readonly Window KnobWindow;

        // The canvas contains the entire knob, and the center of the canvas is the center of the knob (everything rotates
        // about the center of the Canvas). A canvas is used so that the various elements can be positioned at arbitrary X,Y
        // co-ordinates.
        private readonly Canvas KnobCanvas;

        // This is the element that is actually rotated as the knob is turned by the user.
        private readonly FrameworkElement KnobRotate;

        // The nub element - which is what the user actually interacts with.
        private readonly FrameworkElement NubElement;

        // This flag indicates when the knob has been "acquired". It will only respond to the user's movement once it
        // is acquired.
        private bool IsAcquired = false;

        // This moves the knob into its resting position, centered on the nearest stop.
        private AutoLerp RestReturn = null;

        // The center point of the knob, in the canvas. This is used to calculate the angle of the knob when
        // the user is interacting with it.
        private Point KnobCenterPoint;

        // This is the current angle of the knob.
        private double CurrentAngle = 0.0;

        // When the "acquire" state changes, this delegate is called.
        public delegate void AcquireStateChange(bool IsNowAcquired);

        private readonly AcquireStateChange AcquireStateChangeDelegate;

        // Set target section index for audio cue
        private uint TargetSectionIndex = 3;
        private bool HasPlayedTargetCue = false;
        private readonly System.Media.SoundPlayer TargetReachedCue = new System.Media.SoundPlayer("assets/TargetSound.wav");

        // Each section marker needs a few things.
        class SectionMarker
        {
            // The image which forms the visual of the section marker when the marker is NOT active..
            public Image InactiveImage { get; private set; }

            // The image which forms the visual of the section marker when the marker is active and the
            // knob is acquired
            public Image ActiveImageAcquired { get; private set; }

            // The image which forms the visual of the section marker when the marker is active and the
            // knob is idle (not acquired)
            public Image ActiveImageIdle { get; private set; }

            // The storyboard for animating the marker (it fades off).
            public Storyboard Storyboard { get; private set; }

            // These values are set when the marker is created.
            public SectionMarker(Image InactiveImage, Image ActiveImageAcquired, Image ActiveImageIdle, Storyboard storyboard)
            {
                this.InactiveImage = InactiveImage;
                this.ActiveImageAcquired = ActiveImageAcquired;
                this.ActiveImageIdle = ActiveImageIdle;
                this.Storyboard = storyboard;
            }
        }

        // Images for each of the active sections markers.
        private readonly SectionMarker[] SectionMarkers = new SectionMarker[TOTAL_SECTIONS];

        // The index for the current section the nub is in.
        private uint CurrentSectionIndex = 0;

        // The tracked view for the knob.
        private TanvasTouchViewTracker TrackedTView;

        // The sprite for the haptics while rotating the knob.
        private TSprite RotationHaptics;

        // The sprite for the haptics for the nub.
        private TSprite NubHaptics;


        /// <summary>
        /// The main constructor for the knob. Once created, the user will be able to interact with this knob, spinning it to
        /// any position. This is designed with explicit sections, with the knob "settling" into the center of each section.
        /// </summary>
        /// <param name="KnobWindow">The window which contains the knob.</param>
        /// <param name="KnobCanvas">The Canvas (centered on the above element) in which the other elements are positioned</param>
        /// <param name="KnobRotate">The element which is rotated to make the knob "spin".</param>
        /// <param name="NubElement">The element of the nub itself, which is what the user actually manipulates, and what the code rotates/positions.</param>
        public HapticKnob(Window KnobWindow, Canvas KnobCanvas, Canvas KnobRotate, FrameworkElement NubElement, AcquireStateChange AcquireStateChangeDelegate)
        {
            // Init the members.
            this.KnobWindow = KnobWindow;
            this.KnobCanvas = KnobCanvas;
            this.KnobRotate = KnobRotate;
            this.NubElement = NubElement;

            // Save the delegate for when acquire changes.
            this.AcquireStateChangeDelegate = AcquireStateChangeDelegate;

            // The user acquires the knob by touching down near the nub - we detect this by tracking touches anywhere in the window.
            KnobWindow.TouchDown += WindowTouchDown;

            // However touch tracking must continue anywhere in the window.
            KnobWindow.TouchMove += WindowTouchMove;
            KnobWindow.TouchUp += WindowTouchUp;
            KnobWindow.TouchLeave += WindowTouchLeave;

            // Since some displays support multi-touch, it's possible for the window to move
            // or be resized while the knob is acquired. In that case it's best to just force
            // the release.
            KnobWindow.SizeChanged += WindowSizeChanged;
            KnobWindow.LocationChanged += WindowLocationChanged;

            KnobWindow.Loaded += KnobWindow_Loaded;
            KnobWindow.Unloaded += KnobWindow_Unloaded;
            KnobWindow.ContentRendered += KnobWindow_ContentRendered;

            // When the window DPI changes, we need to resize sprites and load appropriate textures.
            KnobWindow.DpiChanged += KnobWindow_DpiChanged;
        }

        /// <summary>
        /// This calculates a point at the given angle and distance from the center of the knob. Note that
        /// distances should be less than the size of the KnobCanvas.
        /// </summary>
        /// <param name="AngleRad">The angle, in radian, for which the point should be calculated</param>
        /// <param name="DistancePixels">The distance, in pixels, from the center of the knob</param>
        /// <returns></returns>
        private Point CalculatePointAtAngleAndDistance(double AngleRad, double DistancePixels)
        {
            // Calculate the position of the point, at the appropriate distance.
            Point Point = new Point();
            Point.Y = KnobCenterPoint.Y + (int)Math.Round(DistancePixels * Math.Sin(AngleRad));
            Point.X = KnobCenterPoint.X + (int)Math.Round(DistancePixels * Math.Cos(AngleRad));

            return Point;
        }

        /// <summary>
        /// This will compute the angle around the circle, given the point that's passed
        /// in. The point may be that of the nub itself, or of the touch location, it
        /// doesn't matter. What does matter is that the point must be relative to the
        /// KnobCanvas itself.
        /// </summary>
        /// <param name="point">The point of interest, relative to the canvas</param>
        /// <returns>The angle, in radians.</returns>
        private double ComputeAngleFromPoint(Point PointInCanvas)
        {
            // We need to translate the point so that it's relative to the center of the knob, which
            // is the center of the canvas.
            Point PointRelativeToKnobCenter = new Point(PointInCanvas.X, PointInCanvas.Y);
            PointRelativeToKnobCenter.X -= KnobCenterPoint.X;
            PointRelativeToKnobCenter.Y -= KnobCenterPoint.Y;

            // Get the result (in Radian)
            double angle = Math.Atan2(PointRelativeToKnobCenter.Y, PointRelativeToKnobCenter.X);
            if (angle < 0)
            {
                angle += Math.PI * 2;
            }
            return angle;
        }

        private SectionMarker[] GetSectionMarkers()
        {
            return SectionMarkers;
        }

        /// <summary>
        /// This will set the knob at a particular angle of rotation.
        /// </summary>
        /// <param name="angle">The angle (in radian) at which to set the knob</param>
        private void SetKnobAtAngle(double angle, SectionMarker[] sectionMarkers)
        {
            // Set the angle of the knob to match where the user has touched.
            KnobRotate.RenderTransform = new RotateTransform(angle * (180.0 / Math.PI), KnobRotate.ActualWidth / 2.0, KnobRotate.ActualHeight / 2.0);

            // Save the angle.
            CurrentAngle = angle;

            // See which section we're in.
            uint SectionIndex = (uint)(angle / SECTION_ARC_RADIAN);
            if (SectionIndex != CurrentSectionIndex)
            {
                // We've moved into a new section, run the fade animation on the section we're leaving.
                SectionMarkers[CurrentSectionIndex].Storyboard.Begin();
                SectionMarkers[CurrentSectionIndex].ActiveImageIdle.Visibility = Visibility.Hidden;

                if (IsAcquired)
                {
                    sectionMarkers[SectionIndex].Storyboard.Stop();
                    SectionMarkers[SectionIndex].ActiveImageAcquired.Visibility = Visibility.Visible;
                }
                else
                {
                    // The knob isn't acquired, so illuminate that piece.
                    SectionMarkers[SectionIndex].ActiveImageIdle.Visibility = Visibility.Visible;
                }
            }

            // Play audio cue if target section is reached
            if (SectionIndex == TargetSectionIndex && !HasPlayedTargetCue)
            {
                TargetReachedCue.Play();
                HasPlayedTargetCue = true;
            }
            else if (SectionIndex != TargetSectionIndex)
            {
                HasPlayedTargetCue = false;
            }

            // Save the current section index.
            CurrentSectionIndex = SectionIndex;

            if (IsAcquired == false)
            {
                // When not acquired it's necessary to keep the "nub" haptics aligned with the visuals.
                AlignNubHaptics();
            }
        }

        /// <summary>
        /// Called when it is determined that the knob has been acquired. Once acquired, it'll be tracked
        /// along with the user's touch.
        /// 
        /// (If it's already acquired, then this will do nothing).
        /// 
        /// </summary>
        private void KnobAcquired()
        {
            if (IsAcquired == false)
            {
                IsAcquired = true;

                // Make sure to cancel any lerp.
                if (RestReturn != null)
                {
                    RestReturn.Cancel();
                    RestReturn = null;
                }

                // Enable the rotation haptics.
                if (RotationHaptics != null)
                {
                    RotationHaptics.Enabled = true;
                }

                // Disable the nub haptics.
                if (NubHaptics != null)
                {
                    NubHaptics.Enabled = false;
                }

                // Activate the "knob is acquired" section marker.
                SectionMarkers[CurrentSectionIndex].ActiveImageAcquired.Visibility = Visibility.Visible;
                SectionMarkers[CurrentSectionIndex].ActiveImageIdle.Visibility = Visibility.Hidden;

                // Call the delegate.
                AcquireStateChangeDelegate(IsAcquired);
            }
        }

        /// <summary>
        /// Called once it's determined that the knob has been released. 
        /// 
        /// (If the knob isn't currently acquired, then this will do nothing)
        /// 
        /// </summary>
        private void KnobReleased()
        {
            if (IsAcquired)
            {
                IsAcquired = false;

                // Call the delegate.
                AcquireStateChangeDelegate(IsAcquired);

                // Activate the "knob is idle" section marker.
                SectionMarkers[CurrentSectionIndex].ActiveImageAcquired.Visibility = Visibility.Hidden;
                SectionMarkers[CurrentSectionIndex].ActiveImageIdle.Visibility = Visibility.Visible;

                // Disable the rotation haptics.
                if (RotationHaptics != null)
                {
                    RotationHaptics.Enabled = false;
                }

                // Enable the nub haptics.
                if (NubHaptics != null)
                {
                    AlignNubHaptics();
                    NubHaptics.Enabled = true;
                }

                // In order to have the knob "snap" into position in the middle of this section, it's necessary to
                // find the center of the current section.
                double SectionCenterAngle = (SECTION_ARC_RADIAN * CurrentSectionIndex) + (SECTION_ARC_RADIAN / 2.0);

                // Now lerp from where the knob is.
                RestReturn = new AutoLerp((float)CurrentAngle, (float)SectionCenterAngle, 300, LerpUpdateCallback);
            }
        }

        /// <summary>
        /// This will create a single section mark - these are the marks (the "lights") around the circumference of the
        /// knob.
        /// </summary>
        /// <param name="SectionCenterAngle"></param>
        /// <param name="PositionCenter"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        private Image CreateSectionMark(double SectionCenterAngle, Point PositionCenter, Uri uri)
        {
            Image MarkImage = new Image();
            BitmapImage MarkBitmap = new BitmapImage(uri);
            MarkImage.Source = MarkBitmap;

            // Rotate the active marker image to match the angle. (Which takes degrees..)
            MarkImage.RenderTransform = new RotateTransform((SectionCenterAngle * (180.0 / Math.PI)) + 90.0, MarkBitmap.PixelWidth / 2.0, MarkBitmap.PixelHeight / 2.0);

            // Now add it to the Canvas, at the correct position.
            Point MarkPosition = new Point(PositionCenter.X - (MarkBitmap.PixelWidth / 2.0), PositionCenter.Y - (MarkBitmap.PixelHeight / 2.0));
            KnobCanvas.Children.Add(MarkImage);
            Canvas.SetLeft(MarkImage, MarkPosition.X);
            Canvas.SetTop(MarkImage, MarkPosition.Y);
            Panel.SetZIndex(MarkImage, 1);

            return MarkImage;
        }

        /// <summary>
        /// This will create the active section markers around the knob. The idea is that only one marker
        /// is "lit" at a time, and this changes as the user rotates the knob. This method creates the
        /// images and animations, and places them in position (hidden). As the knob is rotated, the
        /// appropriate marker will be illuminated (ie: made visible)
        /// </summary>
        private void CreateActiveSectionMarks()
        {
            for (uint i = 0; i < TOTAL_SECTIONS; i++)
            {
                // This is the angle (in radian) of the center of the section.
                double SectionCenterAngle = (SECTION_ARC_RADIAN * (double)i) + (SECTION_ARC_RADIAN / 2.0);
                
                // This is the center point for the marker.
                Point PositionCenter = CalculatePointAtAngleAndDistance(SectionCenterAngle, SECTION_RADIUS_PIXELS);

                // Create the Image for the inactive marker.
                Image MarkImageInactive = CreateSectionMark(SectionCenterAngle, PositionCenter, new Uri("pack://application:,,,/assets/background/section_inactive_single.png"));

                // Create the Image for the active marker for while the knob is acquired.
                Image MarkImageActiveAcquired = CreateSectionMark(SectionCenterAngle, PositionCenter, new Uri("pack://application:,,,/assets/background/section_active_acquired_single.png"));
                MarkImageActiveAcquired.Visibility = Visibility.Hidden;

                // Create the Image for the active marker for while the knob is not acquired.
                Image MarkImageActiveIdle = CreateSectionMark(SectionCenterAngle, PositionCenter, new Uri("pack://application:,,,/assets/background/section_active_idle_single.png"));

                // Now make the storyboard.
                var FadeAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    FillBehavior = FillBehavior.Stop,
                    Duration = new Duration(TimeSpan.FromMilliseconds(120))
                };
                var FadeStoryboard = new Storyboard();

                FadeStoryboard.Children.Add(FadeAnimation);
                Storyboard.SetTarget(FadeAnimation, MarkImageActiveAcquired);
                Storyboard.SetTargetProperty(FadeAnimation, new PropertyPath(Image.OpacityProperty));
                FadeStoryboard.Completed += delegate {
                    // Make sure to hide the image when the animation completes.
                    MarkImageActiveAcquired.Visibility = Visibility.Hidden;
                };

                SectionMarkers[i] = new SectionMarker(MarkImageInactive, MarkImageActiveAcquired, MarkImageActiveIdle, FadeStoryboard);

                if (i != CurrentSectionIndex)
                {
                    MarkImageActiveIdle.Visibility = Visibility.Hidden;
                }
            }
        }

        /// <summary>
        /// Called to align the Nub haptics with the Nub element.
        /// </summary>
        private void AlignNubHaptics()
        {
            if (NubHaptics == null)
            {
                return;
            }

            // Get the center point of the nub in haptic view space.
            var nubCenterOffset = new Point(NubElement.ActualWidth / 2.0, NubElement.ActualHeight / 2.0);
            var nubCenterPoint = NubElement.PointToHapticView(nubCenterOffset, TrackedTView.View);

            // Now position the sprite so that its center is coincident with the nub's center.
            NubHaptics.X = (float) (nubCenterPoint.X - (NubHaptics.Width / 2.0));
            NubHaptics.Y = (float) (nubCenterPoint.Y - (NubHaptics.Height / 2.0));
        }

        /// <summary>
        /// Called to align the "While Rotating" TanvasTouch TSprite with the knob. It's important
        /// that the center of the sprite is aligned with the center of the knob.
        /// </summary>
        private void AlignRotationHaptics()
        {
            if (RotationHaptics == null)
            {
                return;
            }

            // Get the center point of the knob in haptic view space.
            var knobCenterInHapticView = KnobCanvas.PointToHapticView(KnobCenterPoint, TrackedTView.View);

            // Now position the rotation sprite so that its center is coincident with the knob's center.
            RotationHaptics.X = (float) (knobCenterInHapticView.X - (RotationHaptics.Width / 2.0));
            RotationHaptics.Y = (float) (knobCenterInHapticView.Y - (RotationHaptics.Height / 2.0));
        }

        /// <summary>
        /// Called whenever the knob's view geometry changes (either size or location). It is
        /// necessary to make sure that the "while rotating" sprite maintains alignment with the
        /// center of the knob.
        /// </summary>
        /// <param name="View"></param>
        private void OnViewGeometryChanged(TView View)
        {
            AlignRotationHaptics();
            AlignNubHaptics();
        }

        /// <summary>
        /// Called to setup the haptics. This will create a view for the knob, along with
        /// the two sprites for the haptics.
        /// This must be called once the window has been loaded.
        /// </summary>
        private void SetupHaptics(DpiScale scale)
        {
            // Create a view that tracks the window.
            TrackedTView = new TanvasTouchViewTracker(KnobWindow);

            // Create haptic sprites and add them to view.
            CreateHapticSprites(scale);

            // Now that everything is setup, we want to be alerted to the view changing.
            TrackedTView.OnGeometryChanged += OnViewGeometryChanged;
        }

        private void TeardownHaptics()
        {
            // Clean everything up.
            NubHaptics.Dispose();
            NubHaptics = null;
            RotationHaptics.Dispose();
            RotationHaptics = null;
            TrackedTView.Dispose();
            TrackedTView = null;
        }

        private void CreateHapticSprites(DpiScale scale)
        {
            const string rotationHapticsUriTemplate =
                "pack://application:,,,/assets/background/rotation_haptics@{0}.png";
            const string nubHapticsUriTemplate = "pack://application:,,,/assets/nub/haptics@{0}.png";

            // Determine which texture size to use.  In this application, we only use DpiScaleX because
            // on the Mimo Vue HD with TanvasTouch (and many other 2020-era screens) it's typically
            // the same as DpiScaleY.
            //
            // Windows suggests only 100% and 125% zoom for the Mimo Vue HD with TanvasTouch, so this
            // application provides textures for those two zoom levels.
            var rotationHapticsUri = scale.DpiScaleX switch
            {
                1.0 => string.Format(rotationHapticsUriTemplate, "1"),
                1.25 => string.Format(rotationHapticsUriTemplate, "1.25"),
                _ => string.Format(rotationHapticsUriTemplate, "1.25")
            };

            var nubHapticsUri = scale.DpiScaleX switch
            {
                1.0 => string.Format(nubHapticsUriTemplate, "1"),
                1.25 => string.Format(nubHapticsUriTemplate, "1.25"),
                _ => string.Format(nubHapticsUriTemplate, "1.25")
            };

            // Create the sprite for rotation haptics.
            RotationHaptics = PNGToTanvasTouch.CreateSpriteFromPNG(new Uri(rotationHapticsUri));
            if (RotationHaptics != null)
            {
                TrackedTView.View.AddSprite(RotationHaptics);
                RotationHaptics.Enabled = false;
            }

            // Create the nub sprite.
            NubHaptics = PNGToTanvasTouch.CreateSpriteFromPNG(new Uri(nubHapticsUri));
            if (NubHaptics != null)
            {
                TrackedTView.View.AddSprite(NubHaptics);
                NubHaptics.Enabled = true;
            }
        }

        /// <summary>
        /// This must be called when the window containing the knob has been loaded, so that it can
        /// be initialized with all of the UIElements existing.
        /// </summary>
        private void KnobWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // This is the center of the knob, in the canvas
            KnobCenterPoint = new Point(KnobCanvas.ActualWidth / 2.0, KnobCanvas.ActualHeight / 2.0);

            // Setup the Haptics.
            SetupHaptics(VisualTreeHelper.GetDpi(KnobWindow));

            // Create the section markers.
            CreateActiveSectionMarks();

            // Home the knob to the 12:00 position.
            SetKnobAtAngle((3.0 * Math.PI) / 2.0, GetSectionMarkers());
        }

        /// <summary>
        /// Called when the window is unloaded, to dispose of all the TanvasTouch resources.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KnobWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            TeardownHaptics();
        }

        /// <summary>
        /// Called when the window's content has been rendered to make sure the haptics align with the
        /// visuals.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KnobWindow_ContentRendered(object sender, EventArgs e)
        {
            AlignRotationHaptics();
            AlignNubHaptics();
        }

        private void KnobWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (e.Source == KnobWindow)
            {
                TeardownHaptics();
                SetupHaptics(e.NewDpi);
            }
        }

        /// <summary>
        /// Called periodically while returning the knob to the center of the section..
        /// </summary>
        /// <param name="pct"></param>
        private void LerpUpdateCallback(float angle)
        {
            SetKnobAtAngle(angle, GetSectionMarkers());
        }

        /// <summary>
        /// Called when a touch down happens anywhere in the window. If the touch is close to the nub (KNUB_ACQUIRE_RADIUS_PIXELS from the
        /// center of the nub) then the knob will be acquired.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowTouchDown(object sender, TouchEventArgs e)
        {
            // Find the center of the nub (relative to the window).
            Point NubCenterOffset = new Point(NubElement.ActualWidth / 2.0, NubElement.ActualHeight / 2.0);
            Point NubCenterInWindow = NubElement.TranslatePoint(NubCenterOffset, KnobWindow);

            // Now where did the touch happen? (Again, relative to the window)
            Point TouchLocation = e.GetTouchPoint(KnobWindow).Position;

            // How close is the touch down?
            int TouchDistancePixels = (int)Point.Subtract(TouchLocation, NubCenterInWindow).Length;

            if (Math.Abs(TouchDistancePixels) < KNUB_ACQUIRE_RADIUS_PIXELS)
            {
                // Close enough to the nub!
                KnobAcquired();
            }
        }

        /// <summary>
        /// Called when the touch has moved anywhere in the window. If the knob had been acquired, then it will be
        /// rotated to match the touch.
        /// 
        /// If the knob hasn't been acquired, this will do nothing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowTouchMove(object sender, TouchEventArgs e)
        {
            if (IsAcquired)
            {
                // We allow the knob to be controlled anywhere in the window, which means
                // the touch point must be converted to a point in the canvas.
                Point TouchPointInCanvas = e.GetTouchPoint(KnobCanvas).Position;

                // Set the knob to the appropriate angle.
                SetKnobAtAngle(ComputeAngleFromPoint(TouchPointInCanvas), GetSectionMarkers());
            }
        }

        /// <summary>
        /// Called when on the touch-up even anywhere in the window, which means that the knob has been
        /// released.
        /// 
        /// (If the knob wasn't already acquired, this will do nothing).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowTouchUp(object sender, TouchEventArgs e)
        {
            KnobReleased();
        }

        /// <summary>
        /// Called when the touch leaves the window, to release the knob.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowTouchLeave(object sender, TouchEventArgs e)
        {
            KnobReleased();
        }

        /// <summary>
        /// Called when the size of the window has changed. Since some touch-screens
        /// support multi-touch it's not impossible for the window to be resized while the
        /// knob is acquired. To be safe, the knob will be released upon window resizing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            KnobReleased();
        }

        /// <summary>
        /// Called when the position of the window has changed. Since some touch-screens
        /// support multi-touch it's not impossible for the window to be moved while the
        /// knob is acquired. To be safe, the knob will be released upon moving the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowLocationChanged(object sender, EventArgs e)
        {
            KnobReleased();
        }
    }

    public static class FrameworkElementHapticsExtensions
    {
        /// <summary>
        /// Converts a Point in the coordinate system of a Visual into a Point in a TView.
        /// </summary>
        /// <param name="visual">The Visual containing the point.</param>
        /// <param name="point">The point to convert.</param>
        /// <param name="hapticView">The target TView.</param>
        /// <returns></returns>
        public static Point PointToHapticView(this Visual visual, Point point, TView hapticView)
        {
            // Haptic views exist in screen space.  To convert a point in a Visual to haptic view space,
            // we ask the Visual to convert that point to screen space and then present the converted point
            // relative to the selected haptic view.
            var screenSpace = visual.PointToScreen(point);

            return new Point(screenSpace.X - hapticView.X, screenSpace.Y - hapticView.Y);
        }
    }
}
