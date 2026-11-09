namespace LivLiv.Platform
{
    // Borrowed storage; key and JSON are source-compatible with localStorage lm_calib:<remember>.
    // Implementations may throw (denied storage/quota). FaceTracker treats persistence as best effort.
    public interface ICalibrationStore
    {
        string Get(string key);
        void Set(string key,string json);
    }
}
