namespace CacheStorage;

internal class ExceptionManager {
    public static KeyNotFoundException KeyNotFound () {
        return new KeyNotFoundException ( "The specified key was not found in the current cache storage." );
    }
}
