using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Filterizer2
{
    public partial class Ffmpeg
    {
        private void Exec(string? input, string? output, string arguments)
        {
	        Process ffmpeg = new Process();

            ffmpeg.StartInfo.Arguments = " -i " + input+ (arguments != ""? " "+arguments:"")+" "+output; 
            ffmpeg.StartInfo.FileName = "ffmpeg/bin/ffmpeg.exe";
            ffmpeg.StartInfo.UseShellExecute = false;
            // ffmpeg.StartInfo.RedirectStandardOutput = true;
            // ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.StartInfo.RedirectStandardOutput = false;
            ffmpeg.StartInfo.RedirectStandardError = false;
            ffmpeg.StartInfo.CreateNoWindow = true;

            ffmpeg.Start();
            
            // string stdout = ffmpeg.StandardOutput.ReadToEnd();
            // string stderr = ffmpeg.StandardError.ReadToEnd();
            
            ffmpeg.WaitForExit();
            ffmpeg.Close();     
        }


        public void GetThumbnail(string? videoPath, string? thumbnailPath)
        {
	        var duration = GetDuration(videoPath);
	        var position = TimeSpan.FromTicks(duration.Ticks / 3);

	        Exec(videoPath, thumbnailPath, $"-ss {position} -vframes 1 -s 64x64");
        }

        private static TimeSpan GetDuration(string? videoPath)
        {
	        if (videoPath == null)
	        {
		        return TimeSpan.Zero;
	        }
	        var p = new Process();
	        p.StartInfo.FileName = "ffmpeg/bin/ffmpeg.exe";
	        p.StartInfo.Arguments = $"-i \"{videoPath}\"";
	        p.StartInfo.UseShellExecute = false;
	        p.StartInfo.RedirectStandardError = true;
	        p.StartInfo.CreateNoWindow = true;

	        p.Start();

	        string output = p.StandardError.ReadToEnd();
	        p.WaitForExit();

	        var match = MyRegex().Match(output);
	        return !match.Success ? TimeSpan.Zero : TimeSpan.Parse(match.Groups[1].Value);
        }

        [GeneratedRegex(@"Duration: (\d\d:\d\d:\d\d\.\d\d)")]
        private static partial Regex MyRegex();
    }
}