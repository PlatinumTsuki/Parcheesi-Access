using NAudio.Vorbis;

var dir = args.Length > 0 ? args[0] : @"C:\Users\assaa\Downloads\Parchisi Access\dotnet\Parcheesi.App\Assets\Sounds";
var tracks = new[] {
    "satie_gymnopedie_1.ogg", "satie_gymnopedie_2.ogg", "satie_gymnopedie_3.ogg",
    "satie_gnossienne_1.ogg", "schumann_traumerei.ogg", "chopin_berceuse.ogg",
    "debussy_clair_de_lune.ogg", "chopin_raindrop.ogg",
};

foreach (var t in tracks)
{
    var path = Path.Combine(dir, t);
    using var stream = File.OpenRead(path);
    using var reader = new VorbisWaveReader(stream, false);
    var buf = new byte[1 << 16];
    var floats = new float[buf.Length / 4];
    double peak = 0, sumsq = 0;
    long count = 0;
    int n;
    while ((n = reader.Read(buf, 0, buf.Length)) > 0)
    {
        int fc = n / 4;
        Buffer.BlockCopy(buf, 0, floats, 0, n);
        for (int i = 0; i < fc; i++)
        {
            float v = floats[i];
            double a = Math.Abs(v);
            if (a > peak) peak = a;
            sumsq += v * v;
            count++;
        }
    }
    double rms = count > 0 ? Math.Sqrt(sumsq / count) : 0;
    double pdb = peak > 0 ? 20.0 * Math.Log10(peak) : double.NegativeInfinity;
    double rdb = rms > 0 ? 20.0 * Math.Log10(rms) : double.NegativeInfinity;
    Console.WriteLine($"{t,-30} peak={pdb,7:F2} dB  rms={rdb,7:F2} dB");
}
