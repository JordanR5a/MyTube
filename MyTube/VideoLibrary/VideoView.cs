using System;
using System.Collections.Generic;
using System.Linq;
using MyTube.Model;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace MyTube.VideoLibrary
{
    class VideoView
    {
        public static double DEFAULT_VOLUME { get { return 0.7; } }

        private List<AttachedVideo> currentVideos;
        private MediaPlayerElement mediaPlayer;
        private StorageFile currentFile;
        private int currentVideoIndex;
        private bool randomized, looping;
        public bool LoopingEnabled { get { return looping; } }
        public int CurrentVideoIndex { get { return currentVideoIndex; } }
        public AttachedVideo CurrentVideo { get { return currentVideos[currentVideoIndex]; } }
        public TimeSpan[] StartStopPoints { get; set; }
        public TimeSpan Position { get { return mediaPlayer.MediaPlayer.PlaybackSession.Position; } }
        public double Volume { get { return mediaPlayer.MediaPlayer.Volume; } }
        public int VideoCount { get { return currentVideos.Count; } }
        public bool IsLastVideo { get { return (currentVideoIndex == VideoCount - 1); } }

        public VideoView(MediaPlayerElement mediaPlayer, List<AttachedVideo> currentVideos)
        {
            this.mediaPlayer = mediaPlayer;
            this.currentVideos = currentVideos;
            currentVideoIndex = 0;
        }

        public VideoView(MediaPlayerElement mediaPlayer, List<AttachedVideo> currentVideos, int currentVideoIndex) : this(mediaPlayer, currentVideos)
        {
            if (currentVideoIndex > currentVideos.Count - 1) currentVideoIndex = currentVideos.Count - 1;
            this.currentVideoIndex = currentVideoIndex;
        }

        public bool ToggleCurrentVideoMute()
        {
            if (mediaPlayer.MediaPlayer.IsMuted)
            {
                mediaPlayer.MediaPlayer.IsMuted = false;
                return false;
            }
            else
            {
                mediaPlayer.MediaPlayer.IsMuted = true;
                return true;
            }
        }

        public void RaiseCurrentVideoVolume(double num)
        {
            if (mediaPlayer.MediaPlayer.Volume + num <= 1) mediaPlayer.MediaPlayer.Volume += num;
            else mediaPlayer.MediaPlayer.Volume = 1;
        }

        public void LowerCurrentVideoVolume(double num)
        {
            if (mediaPlayer.MediaPlayer.Volume - num >= 0) mediaPlayer.MediaPlayer.Volume -= num;
            else mediaPlayer.MediaPlayer.Volume = 0;
        }

        public void CloseVideoPlayer()
        {
            mediaPlayer.MediaPlayer.Source = null;
        }

        public void RemoveVideo()
        {
            AttachedVideo videoToRemove = currentVideos[currentVideoIndex];
            if (videoToRemove.Id != null) currentVideos.RemoveAll(x => x.Id == videoToRemove.Id);
            else currentVideos.Remove(videoToRemove);
            if (VideoCount > 0 && currentVideoIndex >= VideoCount) currentVideoIndex = VideoCount - 1;
        }

        public void DeleteVideo()
        {
            CurrentVideo.File.DeleteAsync().AsTask().GetAwaiter().GetResult();
            RemoveVideo();
        }

        public void ResetCurrentVideoProperties()
        {
            CurrentVideo.Parts = new List<TimeSpan[]> { new TimeSpan[2] { TimeSpan.Zero, CurrentVideo.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult().Duration } };
            StartStopPoints = new TimeSpan[2] { CurrentVideo.StartTime, CurrentVideo.EndTime };
        }

        public void PartitionReset()
        {
            CurrentVideo.Parts = new List<TimeSpan[]> { new TimeSpan[2] { CurrentVideo.StartTime, CurrentVideo.EndTime } };
            StartStopPoints = new TimeSpan[2] { CurrentVideo.StartTime, CurrentVideo.EndTime };
        }

        public bool FocusCurrentVideo()
        {
            try
            {
                if (CurrentVideo.File != currentFile) mediaPlayer.MediaPlayer.Source = MediaSource.CreateFromStorageFile(CurrentVideo.File);
                else ReplayCurrentVideo();
                currentFile = CurrentVideo.File;

                mediaPlayer.MediaPlayer.Volume = DEFAULT_VOLUME;
                StartStopPoints = new TimeSpan[2] { CurrentVideo.StartTime, CurrentVideo.EndTime };
            }
            catch (Exception) { return false; }
            return true;

        }

        public void SkipToNextVideo()
        {
            if (looping)
            {
                ReplayCurrentVideo();
                return;
            }

            string id = CurrentVideo.Id;

            do
            {
                if (currentVideoIndex >= VideoCount - 1) currentVideoIndex = 0;
                else currentVideoIndex++;
            }
            while (id != null && id == CurrentVideo.Id && !currentVideos.Select(v => v.Id).All(x => x == id));

            FocusCurrentVideo();
        }

        public void PlayNextVideoAsync(MediaPlayer sender, object args)
        {
            if (looping)
            {
                ReplayCurrentVideo();
                return;
            }

            if (currentVideoIndex >= VideoCount - 1) currentVideoIndex = 0;
            else currentVideoIndex++;

            FocusCurrentVideo();
        }

        public void PlayPreviousVideoAsync(MediaPlayer sender, object args)
        {
            if (currentVideoIndex <= 0) currentVideoIndex = 0;
            else currentVideoIndex--;

            FocusCurrentVideo();
        }

        public void SetCurrentPosition(TimeSpan position)
        {
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            else if (mediaPlayer.MediaPlayer.PlaybackSession.NaturalDuration < position) position = mediaPlayer.MediaPlayer.PlaybackSession.NaturalDuration;

            try
            {
                if (mediaPlayer.MediaPlayer.PlaybackSession.CanSeek) mediaPlayer.MediaPlayer.PlaybackSession.Position = position;
            }
            catch (Exception)
            {
                FocusCurrentVideo();
                PlayCurrentVideo();
            }


        }

        public void ForwardOneFrame()
        {
            mediaPlayer.MediaPlayer.StepForwardOneFrame();
        }

        public void BackOneFrame()
        {
            mediaPlayer.MediaPlayer.StepBackwardOneFrame();
            mediaPlayer.MediaPlayer.StepBackwardOneFrame();
            mediaPlayer.MediaPlayer.StepForwardOneFrame();
        }

        public void FastForwardCurrentVideo(TimeSpan amount)
        {
            SetCurrentPosition(mediaPlayer.MediaPlayer.PlaybackSession.Position + amount);
        }

        public void RewindCurrentVideo(TimeSpan amount)
        {
            SetCurrentPosition(mediaPlayer.MediaPlayer.PlaybackSession.Position - amount);
        }

        public void ReplayCurrentVideo()
        {
            SetCurrentPosition(CurrentVideo.StartTime);
        }

        public void ToggleCurrentVideoAutoPlay()
        {
            if (looping) looping = false;
            else looping = true;
        }

        public bool ToggleRandomVideos()
        {
            AttachedVideo video = CurrentVideo;
            if (randomized && CurrentVideo.Id != null)
            {
                currentVideos = currentVideos.OrderBy(x => x.DocumentedDate).ToList();
                currentVideoIndex = currentVideos.FindIndex(x => x == video);
                randomized = false;
                return false;
            }
            else if (randomized && CurrentVideo.Id == null)
            {
                currentVideos = currentVideos.OrderBy(x => x.File.DisplayName).ToList();
                currentVideoIndex = currentVideos.FindIndex(x => x == video);
                randomized = false;
                return false;
            }
            else
            {
                currentVideos = currentVideos.OrderBy(x => new Random().Next()).GroupBy(x => x.Id).SelectMany(grp => grp.ToList()).ToList();
                currentVideoIndex = currentVideos.FindIndex(x => x == video);
                randomized = true;
                return true;
            }
        }

        public void FlickCurrentVideoFullScreen()
        {
            if (mediaPlayer.IsFullWindow) mediaPlayer.IsFullWindow = false;
            else mediaPlayer.IsFullWindow = true;
        }

        public void PauseCurrentVideo()
        {
            mediaPlayer.MediaPlayer.Pause();
        }

        public void PlayCurrentVideo()
        {
            mediaPlayer.MediaPlayer.Play();
        }

        public bool PauseOrPlayCurrentVideo()
        {
            if (mediaPlayer.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                PauseCurrentVideo();
                return false;
            }
            else if (mediaPlayer.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
            {
                PlayCurrentVideo();
                return true;
            }
            else throw new Exception();
        }

        public bool SetCurrentVideoStart()
        {
            if (Position < StartStopPoints[1])
            {
                StartStopPoints[0] = Position;
                return true;
            }
            return false;
        }

        public bool SetCurrentVideoEnd()
        {
            if (Position > StartStopPoints[0])
            {
                StartStopPoints[1] = Position;
                return true;
            }
            return false;
        }

    }
}