﻿//------------------------------------------------------------------------------
// <copyright file="KinectBodyView.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Globalization;
    using System.Diagnostics;

    /// <summary>
    /// Visualizes the Kinect Body stream for display in the UI
    /// </summary>
    public sealed class KinectBodyView
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// to keep track of when Kinect is recording
        /// </summary>
        private bool isRecording;

        /// <summary>
        /// To check if it was calibrated for 3 seconds
        /// </summary>
        private Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Save calibration status
        /// </summary>
        private bool isCalibrated;

        /// <summary>
        /// Optimal Depth for Calibration
        /// </summary>
        float optimalDepth = (float)1.4;

        /// <summary>
        /// Optimal Height for Calibration
        /// </summary>
        float optimalHeight = (float)-0.44;

        /// <summary>
        /// Initializes a new instance of the KinectBodyView class
        /// </summary>
        /// <param name="kinectSensor">Active instance of the KinectSensor</param>
        public KinectBodyView(KinectSensor kinectSensor)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }

            // get the coordinate mapper
            this.coordinateMapper = kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);
        }

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Updates the body array with new information from the sensor
        /// Should be called whenever a new BodyFrameArrivedEvent occurs
        /// </summary>
        /// <param name="bodies">Array of bodies to update</param>
        public void UpdateBodyFrame(Body[] bodies, bool isRecording)
        {
            this.isRecording = isRecording;
            if (bodies != null)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    int penIndex = 0;
                    foreach (Body body in bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];
                        if (body.IsTracked)
                        {

                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                            float spineBaseHeight = joints[JointType.SpineBase].Position.Y;
                            
                            // CALIBRATION 
                            if (!this.isRecording && !this.isCalibrated)
                            {
                                dc.PushOpacity(0.5); 
                                this.Calibrate(bodies, dc);
                                dc.Pop();
                            }
                            else if(!this.isRecording && this.isCalibrated)
                            {
                                // check if calibration is off after the flag is set
                                this.CalibrationCheck(bodies);
                            }
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// <summary>
        /// Checks if the calibration is done for the body 
        /// Called when not recording
        /// </summary>
        /// <param name="bodies">Array of bodies to update</param>
        public void Calibrate(Body[] bodies, DrawingContext dc)
        {
            if (bodies != null)
            {
                    foreach (Body body in bodies)
                    {
                        if (body.IsTracked)
                        {

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // CALIBRATION 
                            float spineBaseDepth = joints[JointType.SpineBase].Position.Z;
                            float spineBaseHeight = joints[JointType.SpineBase].Position.Y;

                            // accepts depths within 10% of the optimal value
                            float leftMargin = (float) 0.9 * optimalDepth;
                            float rightMargin = (float) 1.1 * optimalDepth;

                            // accepts heights within 10% of the optimal value
                            //negative optimal height so opposite
                            float leftMarginHeight = (float)1.1 * optimalHeight;
                            float rightMarginHeight = (float)0.9 * optimalHeight;

                            Console.WriteLine("{0},{1},{2}", spineBaseHeight, leftMarginHeight, rightMarginHeight);

                            if (spineBaseDepth != 0 && (spineBaseDepth < leftMargin || spineBaseDepth > rightMargin ))
                            {
                                // if it was almost calibrated in last frame, now reset the clock
                                if (stopwatch.IsRunning)
                                {
                                    stopwatch.Stop();
                                }

                                if (spineBaseDepth > rightMargin)
                                {
                                    //Console.WriteLine("NOT ALIGNED - TOO FAR");
                                    // Display the formatted text string.

                                    FormattedText formattedText = new FormattedText(
                                        "Not aligned. Move closer to the Kinect!",
                                        CultureInfo.GetCultureInfo("en-us"),
                                        FlowDirection.LeftToRight,
                                        new Typeface("Verdana"),
                                        24,
                                        Brushes.Aquamarine);
                                    dc.DrawText(formattedText, new Point(20, 20));
                                }
                                else
                                {
                                    //Console.WriteLine("NOT ALIGNED - TOO CLOSE");
                                    // Display the formatted text string.

                                    FormattedText formattedText = new FormattedText(
                                        "Not aligned. Move away from the Kinect!",
                                        CultureInfo.GetCultureInfo("en-us"),
                                        FlowDirection.LeftToRight,
                                        new Typeface("Verdana"),
                                        24,
                                        Brushes.Aquamarine);
                                    dc.DrawText(formattedText, new Point(20, 20));
                                }

                                Pen drawPenOverlay = new Pen(Brushes.BurlyWood, 8);
                                Dictionary<JointType, Point> jointPointsOverlay = new Dictionary<JointType, Point>();

                                Dictionary<JointType, Joint> jointsOverlay = new Dictionary<JointType, Joint>();
                                
                                float scalingRatio = optimalDepth / spineBaseDepth;
                                //Console.WriteLine(scalingRatio);

                                foreach (JointType jointType in joints.Keys)
                                {
                                    Joint newJoint = joints[jointType];
                                    
                                    var newPosition = new CameraSpacePoint
                                    {
                                        X = joints[jointType].Position.X * scalingRatio,
                                        Y = joints[jointType].Position.Y * scalingRatio,
                                        Z = spineBaseDepth
                                    };
                                    /*
                                    if(jointType == JointType.HandLeft || jointType == JointType.HandRight)
                                    {
                                        Console.WriteLine("HAND POSITIONS: <{0},{1},{2}>",joints[jointType].Position.X, joints[jointType].Position.Y, joints[jointType].Position.Z);
                                    }
                                    if (jointType == JointType.ElbowLeft || jointType == JointType.ElbowRight)
                                    {
                                        Console.WriteLine("ELBOW POSITIONS: <{0},{1},{2}>", joints[jointType].Position.X, joints[jointType].Position.Y, joints[jointType].Position.Z);
                                    }
                                    if(jointType == JointType.SpineShoulder)
                                    {
                                        Console.WriteLine("Spine shoulder POSITIONS: <{0},{1},{2}>", joints[jointType].Position.X, joints[jointType].Position.Y, joints[jointType].Position.Z);
                                    }
                                    if (jointType == JointType.SpineMid)
                                    {
                                        Console.WriteLine("Spine mid POSITIONS: <{0},{1},{2}>", joints[jointType].Position.X, joints[jointType].Position.Y, joints[jointType].Position.Z);
                                    } */
                                    newJoint.Position = newPosition;
                                    DepthSpacePoint depthSpacePointOverlay = this.coordinateMapper.MapCameraPointToDepthSpace(newPosition);
                                    jointPointsOverlay[jointType] = new Point(depthSpacePointOverlay.X, depthSpacePointOverlay.Y);
                                    jointsOverlay.Add(jointType, newJoint);
                                }


                                this.DrawBody(jointsOverlay, jointPointsOverlay, dc, drawPenOverlay);
                            }
                        else if (spineBaseHeight != 0 && (spineBaseHeight < leftMarginHeight || spineBaseHeight > rightMarginHeight))
                        {
                            // if it was almost calibrated in last frame, now reset the clock
                            if (stopwatch.IsRunning)
                            {
                                stopwatch.Stop();
                            }

                            if (spineBaseHeight > rightMarginHeight)
                            {
                                //Console.WriteLine("NOT ALIGNED - TOO FAR");
                                // Display the formatted text string.

                                FormattedText formattedText = new FormattedText(
                                    "Not aligned. Move the Kinect down!",
                                    CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight,
                                    new Typeface("Verdana"),
                                    24,
                                    Brushes.Aquamarine);
                                dc.DrawText(formattedText, new Point(20, 20));
                            }
                            else
                            {
                                //Console.WriteLine("NOT ALIGNED - TOO CLOSE");
                                // Display the formatted text string.

                                FormattedText formattedText = new FormattedText(
                                    "Not aligned. Move the Kinect up!",
                                    CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight,
                                    new Typeface("Verdana"),
                                    24,
                                    Brushes.Aquamarine);
                                dc.DrawText(formattedText, new Point(20, 20));
                            }

                            Pen drawPenOverlay = new Pen(Brushes.BurlyWood, 8);
                            Dictionary<JointType, Point> jointPointsOverlay = new Dictionary<JointType, Point>();

                            Dictionary<JointType, Joint> jointsOverlay = new Dictionary<JointType, Joint>();

                            float scalingRatio = optimalHeight / spineBaseHeight;
                            //Console.WriteLine(scalingRatio);

                            foreach (JointType jointType in joints.Keys)
                            {
                                Joint newJoint = joints[jointType];

                                var newPosition = new CameraSpacePoint
                                {
                                    X = joints[jointType].Position.X ,
                                    Y = joints[jointType].Position.Y * scalingRatio,
                                    Z = joints[jointType].Position.Z 
                                };
                               
                                newJoint.Position = newPosition;
                                DepthSpacePoint depthSpacePointOverlay = this.coordinateMapper.MapCameraPointToDepthSpace(newPosition);
                                jointPointsOverlay[jointType] = new Point(depthSpacePointOverlay.X, depthSpacePointOverlay.Y);
                                jointsOverlay.Add(jointType, newJoint);
                            }


                            this.DrawBody(jointsOverlay, jointPointsOverlay, dc, drawPenOverlay);
                        }
                        else
                            {
                                if (stopwatch.IsRunning)
                                {
                                    // check if position was maintained for 3 seconds, then stop
                                    var elapsedTime = stopwatch.ElapsedMilliseconds;
                                    if(elapsedTime >= 3000)
                                    {
                                        if (elapsedTime <= 7000)
                                        {
                                            Console.WriteLine("CALIBRATED! You may begin");
                                            FormattedText formattedText = new FormattedText(
                                            "Calibrated! You may begin",
                                            CultureInfo.GetCultureInfo("en-us"),
                                            FlowDirection.LeftToRight,
                                            new Typeface("Verdana"),
                                            24,
                                            Brushes.Aquamarine);
                                            dc.DrawText(formattedText, new Point(20, 20));
                                        }
                                        else
                                        {
                                            stopwatch.Stop();
                                            this.isCalibrated = true;
                                        }
                                        
                                    }
                                }
                                else
                                {
                                Console.WriteLine("Starting Calibration...");
                                FormattedText formattedText = new FormattedText(
                                "Hold still for 3 seconds.",
                                CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight,
                                new Typeface("Verdana"),
                                24,
                                Brushes.Aquamarine);
                                dc.DrawText(formattedText, new Point(20, 20));

                                stopwatch.Start();
                                }

                            }
                        }
                                            
                }
            }
        }

        /// <summary>
        /// Check if calibration is off at any point after flag is set
        /// Called when not recording
        /// </summary>
        /// <param name="bodies">Array of bodies to update</param>
        public void CalibrationCheck(Body[] bodies)
        {
            if (bodies != null)
            {
                foreach (Body body in bodies)
                {
                    if (body.IsTracked)
                    {

                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                        // CALIBRATION 
                        float spineBaseDepth = joints[JointType.SpineBase].Position.Z;

                      
                        // accepts depths within 10% of the optimal value
                        float leftMargin = (float)0.9 * optimalDepth;
                        float rightMargin = (float)1.1 * optimalDepth;

                        if (spineBaseDepth != 0 && (spineBaseDepth < leftMargin || spineBaseDepth > rightMargin))
                        {
                            this.isCalibrated = false;  
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }
    }
}
