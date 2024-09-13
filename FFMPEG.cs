using System.Diagnostics;

namespace Filterizer2
{
    public class FFMPEG
    {
        Process ffmpeg;

        private void Exec(string? input, string? output, string arguments)
        {
            ffmpeg = new Process();

            ffmpeg.StartInfo.Arguments = " -i " + input+ (arguments != ""? " "+arguments:"")+" "+output; 
            ffmpeg.StartInfo.FileName = "ffmpeg/bin/ffmpeg.exe";
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardOutput = true;
            ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.StartInfo.CreateNoWindow = true;

            ffmpeg.Start();
            ffmpeg.WaitForExit();
            ffmpeg.Close();     
        }


        public void GetThumbnail(string? videoPath, string? thumbnailPath)
        {
            Exec(videoPath, thumbnailPath, "-s 64x64");
        }
    }
}