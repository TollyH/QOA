using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace QOA.Player
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private readonly WaveOutEvent audioPlayer = new();
        private WaveStream? audioSource;

        private readonly System.Timers.Timer updateTimer = new(100);

        private bool updatingControls = false;

        private string? openFile = null;

        public MainWindow()
        {
            MediaFoundationApi.Startup();

            InitializeComponent();

            updateTimer.Elapsed += updateTimer_Elapsed;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                LoadFile(args[1]);
            }
        }

        ~MainWindow()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            audioPlayer.Dispose();

            MediaFoundationApi.Shutdown();
        }

        private void LoadFile(string path)
        {
            audioPlayer.Stop();

            try
            {
                switch (Path.GetExtension(path).ToLower())
                {
                    case ".qoa":
                        QOAFile decodedFile = QOADecoder.Decode(File.ReadAllBytes(path));
                        byte[] pcmData = AudioFormatConvert.Int16ChannelsToInterleavedPCMBytesLE(decodedFile.ChannelSamples);

                        WaveFormat wavFormat = new((int)decodedFile.SampleRate, QOAConstants.BitDepth, decodedFile.ChannelCount);
                        audioSource = new RawSourceWaveStream(pcmData, 0, pcmData.Length, wavFormat);
                        break;
                    case ".wav":
                    case ".mp3":
                        audioSource = new AudioFileReader(path);
                        break;
                    default:
                        _ = MessageBox.Show("Invalid file type, must be one of: .qoa, .wav, or .mp3",
                            "Invalid Type", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }

                audioPlayer.Init(audioSource);
                openFile = path;
            }
            catch
            {
#if DEBUG
                throw;
#else
                _ = MessageBox.Show("Failed to open file. It may be missing or corrupt.",
                    "File Read Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
#endif
            }
        }

        private void SaveFile(string path)
        {
            if (audioSource is null || openFile is null)
            {
                return;
            }

            try
            {
                switch (Path.GetExtension(path).ToLower())
                {
                    case ".qoa":
                        string openExtension = Path.GetExtension(openFile).ToLower();

                        if (openExtension == ".qoa")
                        {
                            // The open file is already a QOA file, there is no conversion to be done.
                            File.Copy(openFile, path);
                            return;
                        }
                        if (openExtension != ".wav")
                        {
                            _ = MessageBox.Show("Only WAV files can be converted to QOA.",
                                "File Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        WaveFileReader reader = new(openFile);

                        if (reader.WaveFormat is not { BitsPerSample: QOAConstants.BitDepth, Encoding: WaveFormatEncoding.Pcm })
                        {
                            _ = MessageBox.Show("QOA can only encode signed 16-bit integer PCM audio files.",
                                "File Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        byte[] pcmData = new byte[reader.Length];
                        reader.ReadExactly(pcmData);

                        uint samplesPerChannel = (uint)reader.SampleCount;
                        short[][] channelSamples = AudioFormatConvert.InterleavedPCMBytesLEToInt16Channels(
                            pcmData, samplesPerChannel, (byte)reader.WaveFormat.Channels);

                        QOAFile newFile = new((byte)reader.WaveFormat.Channels, (uint)reader.WaveFormat.SampleRate, samplesPerChannel);
                        for (int channel = 0; channel < reader.WaveFormat.Channels; channel++)
                        {
                            channelSamples[channel].CopyTo(newFile.ChannelSamples[channel], 0);
                        }
                        File.WriteAllBytes(path, QOAEncoder.Encode(newFile));
                        break;
                    case ".wav":
                        WaveFileWriter.CreateWaveFile(path, audioSource);
                        break;
                    case ".mp3":
                        MediaFoundationEncoder.EncodeToMp3(audioSource, path, 256000);
                        break;
                    default:
                        _ = MessageBox.Show("Invalid file type, must be one of: .qoa, .wav, or .mp3",
                            "Invalid Type", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }
            }
            catch
            {
#if DEBUG
                throw;
#else
                _ = MessageBox.Show("Failed to save file. You may not have permission to save to the given path.",
                    "File Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
#endif
            }
        }

        private void PromptFileOpen()
        {
            OpenFileDialog fileDialog = new()
            {
                CheckFileExists = true,
                Filter = "All Supported Types|*.qoa;*.wav;*.mp3" +
                    "|QOA Audio File|*.qoa" +
                    "|WAV Audio File|*.wav" +
                    "|MP3 Audio File|*.mp3"
            };

            if (!fileDialog.ShowDialog(this) ?? true)
            {
                return;
            }

            LoadFile(fileDialog.FileName);
        }

        private void PromptFileSave()
        {
            if (audioSource is null || openFile is null)
            {
                return;
            }

            SaveFileDialog fileDialog = new()
            {
                CheckPathExists = true,
                DefaultExt = ".qoa",
                AddExtension = true,
                Filter = "QOA Audio File|*.qoa" +
                    "|WAV Audio File|*.wav" +
                    "|MP3 Audio File|*.mp3",
                FileName = Path.GetFileNameWithoutExtension(openFile)
            };

            if (!fileDialog.ShowDialog(this) ?? true)
            {
                return;
            }

            SaveFile(fileDialog.FileName);
        }

        private void AudioPlay()
        {
            try
            {
                audioPlayer.Play();
            }
            catch (InvalidOperationException) { }  // No audio loaded - do nothing
        }

        private void AudioStop()
        {
            audioPlayer.Stop();
            audioSource?.Seek(0, SeekOrigin.Begin);
        }

        private void AudioPause()
        {
            audioPlayer.Pause();
        }

        private void UpdateControls()
        {
            updatingControls = true;

            playerStatusLabel.Text = audioPlayer.PlaybackState.ToString();
            timeLabel.Text =
                $"{audioSource?.CurrentTime.TotalHours ?? 0:N0}:{audioSource?.CurrentTime.Minutes ?? 0:00}:{audioSource?.CurrentTime.Seconds ?? 0:00}" +
                $" / {audioSource?.TotalTime.TotalHours ?? 0:N0}:{audioSource?.TotalTime.Minutes ?? 0:00}:{audioSource?.TotalTime.Seconds ?? 0:00}";

            if (audioPlayer.PlaybackState == PlaybackState.Playing)
            {
                timeSlider.Value = audioSource?.CurrentTime.TotalSeconds ?? 0;
            }
            timeSlider.Maximum = audioSource?.TotalTime.TotalSeconds ?? 1;

            updatingControls = false;
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            PromptFileOpen();
        }

        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            PromptFileSave();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.O when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    PromptFileOpen();
                    break;
                case Key.S when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    PromptFileSave();
                    break;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            AudioPlay();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            AudioPause();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            AudioStop();
        }

        private void updateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(UpdateControls);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            updateTimer.Start();
        }

        private void timeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingControls)
            {
                return;
            }

            if (audioPlayer.PlaybackState != PlaybackState.Stopped)
            {
                AudioStop();
            }

            audioSource?.Seek((long)(timeSlider.Value * audioSource.WaveFormat.AverageBytesPerSecond), SeekOrigin.Begin);
        }
    }
}