﻿using UnityEngine;
using System.Collections;
using RenderHeads.Media.AVProVideo;

public class txUGUIVideo : txUGUIRawImage
{
	protected MediaPlayer mMediaPlayer;
	protected VideoCallback mVideoEndCallback;
	protected VideoCallback mVideoReadyCallback;
	protected VideoErrorCallback mErrorCallback;
	protected PLAY_STATE mNextState;
	protected string mFileName;
	protected float mNextRate;
	protected float mNextSeekTime;
	protected bool mAutoShowOrHide;
	protected bool mIsRequires;
	protected bool mNextLoop;   // 刚设置视频文件,还未加载时,要设置播放状态就需要先保存状态,然后等到视频准备完毕后再设置
	protected bool mReady;
	public txUGUIVideo()
	{
		mAutoShowOrHide = true;
		mNextState = PLAY_STATE.PS_NONE;
		mNextRate = 1.0f;
		mFileName = null;
	}
	public override void init(GameObject go, txUIObject parent)
	{
		base.init(go, parent);
		mIsRequires = false;
		mMediaPlayer = getUnityComponent<MediaPlayer>();
		mMediaPlayer.Events.AddListener(onVideoEvent);
	}
	public override void update(float elapsedTime)
	{
		base.update(elapsedTime);
		if(mReady && mMediaPlayer.Control != null && mMediaPlayer.Control.IsPlaying())
		{
			if(mMediaPlayer != null)
			{
				if(mMediaPlayer.TextureProducer != null)
				{
					Texture texture = mMediaPlayer.TextureProducer.GetTexture();
					if(texture != null)
					{
						if(mMediaPlayer.TextureProducer.RequiresVerticalFlip() && !mIsRequires)
						{
							mIsRequires = true;
							Vector3 rot = getRotation();
							float rotX = rot.x + PI_DEGREE;
							adjustAngle180(ref rotX);
							rot.x = rotX;
							// 旋转180 
							setRotation(rot);
						}
						mRawImage.texture = texture;
						// 只有当真正开始渲染时才认为是准备完毕
						mVideoReadyCallback?.Invoke(mFileName, false);
						mVideoReadyCallback = null;
					}
				}
			}
			else
			{
				mRawImage.texture = null;
			}
		}
	}
	public override void destroy()
	{
		mMediaPlayer.Events.RemoveAllListeners();
		base.destroy();
	}
	public void setPlayState(PLAY_STATE state, bool autoShowOrHide = true)
	{
		if(isEmpty(mFileName))
		{
			return;
		}
		if(mReady)
		{
			if(state == PLAY_STATE.PS_PLAY)
			{
				play(autoShowOrHide);
			}
			else if(state == PLAY_STATE.PS_PAUSE)
			{
				pause();
			}
			else if(state == PLAY_STATE.PS_STOP)
			{
				stop(autoShowOrHide);
			}
		}
		else
		{
			mNextState = state;
			mAutoShowOrHide = autoShowOrHide;
		}
	}
	public bool setFileName(string file, string pathUnderStreamingAssets = CommonDefine.SA_VIDEO_PATH)
	{
		string newFileName = getFileName(file);
		if (newFileName != mFileName)
		{
			setVideoEndCallback(null);
			if (!file.StartsWith(pathUnderStreamingAssets))
			{
				file = pathUnderStreamingAssets + file;
			}
			if (!isFileExist(CommonDefine.F_STREAMING_ASSETS_PATH + file))
			{
				logError("找不到视频文件 : " + file);
				return false;
			}
			notifyVideoReady(false);
			mFileName = newFileName;
			mRawImage.texture = null;
			mMediaPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.RelativeToStreamingAssetsFolder, file, false);
		}
		return true;
	}
	public bool setFileURL(string url)
	{
		setVideoEndCallback(null);
		notifyVideoReady(false);
		mFileName = getFileName(url);
		mRawImage.texture = null;
		bool ret = mMediaPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, url, false);
		return ret;
	}
	public string getFileName(){return mFileName;}
	public void setLoop(bool loop)
	{
		if(mReady)
		{
			mMediaPlayer.Control.SetLooping(loop);
		}
		else
		{
			mNextLoop = loop;
		}
	}
	public bool isLoop(){return mMediaPlayer.m_Loop;}
	public void setRate(float rate)
	{
		if(mReady)
		{
			clamp(ref rate, 0.0f, 4.0f);
			if(!isFloatEqual(rate, getRate()))
			{
				mMediaPlayer.Control.SetPlaybackRate(rate);
			}
		}
		else
		{
			mNextRate = rate;
		}
	}
	public float getRate()
	{
		if(mMediaPlayer.Control == null)
		{
			return 0.0f;
		}
		return mMediaPlayer.Control.GetPlaybackRate();
	}
	public float getVideoLength()
	{
		if(mMediaPlayer.Info == null)
		{
			return 0.0f;
		}
		return mMediaPlayer.Info.GetDurationMs() * 0.001f;
	}
	public float getVideoPlayTime()
	{
		return mMediaPlayer.Control.GetCurrentTimeMs();
	}
	public void setVideoPlayTime(float timeMS)
	{
		if(mReady)
		{
			mMediaPlayer.Control.SeekFast(timeMS);
		}
		else
		{
			mNextSeekTime = timeMS;
		}
	}
	public void setVideoEndCallback(VideoCallback callback)
	{
		// 重新设置回调之前,先调用之前的回调
		clearAndCallEvent(ref mVideoEndCallback, true);
		mVideoEndCallback = callback;
	}
	public void closeVideo()
	{
		mMediaPlayer.CloseVideo();
	}
	public bool isPlaying()
	{
		return mMediaPlayer.Control.IsPlaying();
	}
	public void setVideoReadyCallback(VideoCallback callback){mVideoReadyCallback = callback;}
	public void setErrorCallback(VideoErrorCallback callback){ mErrorCallback = callback;}
	//----------------------------------------------------------------------------------------------------------------------------------------
	protected void notifyVideoReady(bool ready)
	{
		mReady = ready;
		if(mReady)
		{
			if(mNextState != PLAY_STATE.PS_NONE)
			{
				setPlayState(mNextState, mAutoShowOrHide);
			}
			setLoop(mNextLoop);
			setRate(mNextRate);
			setVideoPlayTime(mNextSeekTime);
		}
		else
		{
			mNextState = PLAY_STATE.PS_NONE;
			mNextRate = 1.0f;
			mNextLoop = false;
			mNextSeekTime = 0.0f;
		}
	}
	protected void clearAndCallEvent(ref VideoCallback callback, bool isBreak)
	{
		VideoCallback temp = callback;
		callback = null;
		temp?.Invoke(mFileName, isBreak);
	}
	protected void onVideoEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
	{
		logInfo("video event : " + eventType, LOG_LEVEL.LL_HIGH);
		if(eventType == MediaPlayerEvent.EventType.FinishedPlaying)
		{
			// 播放完后设置为停止状态
			clearAndCallEvent(ref mVideoEndCallback, false);
		}
		else if(eventType == MediaPlayerEvent.EventType.ReadyToPlay)
		{
			// 视频准备完毕时,设置实际的状态
			if(mMediaPlayer.Control == null)
			{
				logError("video is ready, but MediaPlayer.Control is null!");
			}
			notifyVideoReady(true);
		}
		else if(eventType == MediaPlayerEvent.EventType.Error)
		{
			logInfo("video error code : " + errorCode, LOG_LEVEL.LL_FORCE);
			mErrorCallback?.Invoke(errorCode);
		}
	}
	protected void play(bool autoShow = true)
	{
		if(mMediaPlayer.Control != null)
		{
			if(autoShow)
			{
				mRawImage.enabled = true;
			}
			if(!mMediaPlayer.Control.IsPlaying())
			{
				mMediaPlayer.Play();
			}
		}
	}
	protected void pause()
	{
		if(mMediaPlayer.Control != null && !mMediaPlayer.Control.IsPaused())
		{
			mMediaPlayer.Pause();
		}
	}
	protected void stop(bool autoHide = true)
	{
		// 停止并不是真正地停止视频,只是将视频暂停,并且移到视频开始位置
		if(mMediaPlayer.Control != null)
		{
			mMediaPlayer.Rewind(true);
			if(autoHide)
			{
				mRawImage.enabled = false;
			}
			clearAndCallEvent(ref mVideoEndCallback, true);
		}
	}
}