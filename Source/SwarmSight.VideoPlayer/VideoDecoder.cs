﻿using System.Linq;
using Classes;
using NReco.VideoConverter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SwarmSight.Filters;

namespace SwarmSight.VideoPlayer
{
    public class VideoDecoder : IDisposable
    {
        public VideoInfo VideoInfo { get; private set; }
        public int CurrentFrame { get; private set; }
        public TimeSpan CurrentTime { get; private set; }
        public double CurrentPercentage { get; private set; }

        public bool IsPlaying { get; private set; }
        public bool IsBufferReady { get; private set; }

        public string VideoPath;
        public int PlayerOutputWidth = 256*4;
        public int PlayerOutputHeight = 192*4;
        public double? PlayEndTimeInSec;
        public double PlayStartTimeInSec;

        private ConvertSettings _settings;
        private FFMpegConverter _filereader;
        public FrameDecoder FrameDecoder;
        public VideoProcessorBase Processor;
        private ConvertLiveMediaTask _readingTask;
        private Thread _readingThread;

        public bool FramesInBuffer
        {
            get
            {
                return FrameDecoder != null &&
                       FrameDecoder.FrameBuffer != null &&
                       FrameDecoder.FrameBuffer.Count > 0;
            }
        }

        public FrameBuffer<Frame> FrameBuffer
        {
            get { return FrameDecoder.FrameBuffer; }
        }

        public bool AtEndOfVideo
        {
            get { return CurrentPercentage >= 1.0; }
        }

        public void Open(string videoPath)
        {
            Stop();

            VideoPath = videoPath;

            if (!File.Exists(VideoPath))
                throw new FileNotFoundException(string.Format("File not found at specified path '{0}'", VideoPath));

            //Parse video stats
            VideoInfo = new VideoInfo(VideoPath);
        }

        public void Start(bool open = true)
        {
            if(open)
                Open(VideoPath);

            _filereader = new FFMpegConverter();

            //Set the format of the output bitmap
            FrameDecoder = new FrameDecoder
            (
                PlayerOutputWidth,
                PlayerOutputHeight,
                PixelFormat.Format24bppRgb
            );

            FrameDecoder.Processor = Processor;
            FrameDecoder.FrameReady += OnFrameReady;
            _filereader.LogReceived += OnLogReceived;

            //Set conversion settings
            _settings = new ConvertSettings
                {
                    VideoFrameSize = PlayerOutputWidth + "x" + PlayerOutputHeight,
                    CustomOutputArgs = " -pix_fmt bgr24 "
                };

            //Set start time
            if (PlayStartTimeInSec > 0)
            {
                //Adjust frame index for seeking
                CurrentFrame = (int) Math.Round(PlayStartTimeInSec*VideoInfo.FPS, 0);

                _settings.Seek = (float?) PlayStartTimeInSec;
            }

            //Set end time (if valid)
            if (PlayEndTimeInSec != null && PlayEndTimeInSec > PlayStartTimeInSec)
            {
                _settings.MaxDuration = (float?) (PlayEndTimeInSec - PlayStartTimeInSec);
            }

            //Setup to convert from the file into the bitmap intercepting stream
            _readingTask = _filereader.ConvertLiveMedia
                (
                    VideoPath,
                    null, // autodetect stream format
                    FrameDecoder,
                    "rawvideo",
                    _settings
                );

            _readingThread = new Thread(StartDecoding) {IsBackground = true};
            _readingThread.Start();
        }

        private void StartDecoding()
        {
            try
            {
                IsPlaying = true;

                try
                {
                    _readingTask.Start();
                }
                catch (ThreadStartException)
                {
                }

                try
                {
                    _readingTask.Wait();
                }
                catch (NullReferenceException)
                {
                }
                catch (FFMpegException)
                {
                }
            }
            finally
            {
                IsPlaying = false;
            }
        }


        private void OnFrameReady(object o, OnFrameReady e)
        {
            CurrentTime = new TimeSpan(0, 0, 0, 0, (int) (0.0 + CurrentFrame/VideoInfo.FPS*1000.0));
            CurrentPercentage = CurrentFrame*1.0/(VideoInfo.TotalFrames - 1);

            //Attach frame metadata to each frame
            var mostRecentFrame = e.Frame;

            mostRecentFrame.FramePercentage = CurrentPercentage;
            mostRecentFrame.FrameIndex = CurrentFrame;
            mostRecentFrame.FrameTime = CurrentTime;
            mostRecentFrame.IsDecoded = true;

            if (FrameDecoder.FramesInBufferMoreThanMinimum)
                IsBufferReady = true;

            if (CurrentFrame < VideoInfo.TotalFrames)
                CurrentFrame++;
        }

        /// <summary>
        /// If available, returns the next frame from the video. If not, returns null
        /// </summary>
        /// <returns></returns>
        public Frame PlayNextFrame()
        {
            if (FramesInBuffer)
            {
                var result = FrameBuffer.First;

                FrameBuffer.Remove(result);

                return result.Value;
            }

            return null;
        }

        /// <summary>
        /// Sets the decoder to start at specified time in video. Start() can
        /// be called after this method.
        /// </summary>
        public void SeekTo(double seconds)
        {
            PlayStartTimeInSec = seconds;
        }

        /// <summary>
        /// Sets the decoder to start at specified time in video. Start() can
        /// be called after this method.
        /// </summary>
        public void SeekTo(int frame)
        {
            if (VideoInfo == null)
                throw new Exception("Video needs to be Open()'ed before seeking to a frame.");

            PlayStartTimeInSec = frame / VideoInfo.FPS;
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                //Kill the decoding thread
                try
                {
                    _readingTask.Abort();
                }
                catch
                {
                }

                //Kill the supervising thread
                try
                {
                    _readingThread.Abort();
                }
                catch
                {
                }

                IsPlaying = false;
            }
        }

        public void ClearBuffer()
        {
            if (FrameDecoder != null)
                FrameDecoder.ClearBuffer();
        }

        public void Dispose()
        {
            if (_readingTask != null)
            {
                try
                {
                    _readingTask.Abort();
                }
                catch
                {
                }
                _readingTask = null;
            }

            if (_filereader != null)
            {
                try
                {
                    _filereader.Abort();
                }
                catch
                {
                }
                _filereader = null;
            }

            if (FrameDecoder != null)
            {
                FrameDecoder.ClearBuffer();

                try
                {
                    FrameDecoder.Dispose();
                }
                catch
                {
                }
                FrameDecoder = null;
            }
        }

        private void OnLogReceived(object o, FFMpegLogEventArgs e)
        {
            //Use for debugging, if needed
        }
    }
}