﻿using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ConsoleBattery;

/// <summary>
/// Sample configuration property class.
/// </summary>
public class Config
{
    // NOTE: If you don't use the "var opts = JsonSerializerOptions { IncludeFields = true };"
    // when serializing a class then you must add the [JsonInclude] property above each field.

    [JsonInclude]
    [JsonPropertyName("version")]
    public string? version;

    [JsonInclude]
    [JsonPropertyName("time")]
    public DateTime time;

    [JsonInclude]
    [JsonPropertyName("firstrun")]
    public bool firstRun = true;

    [JsonInclude]
    [JsonPropertyName("logging")]
    public bool logging = true;

    [JsonInclude]
    [JsonPropertyName("lastrate")]
    public int lastRate = 17000; // mW

    [JsonInclude]
    [JsonPropertyName("refresh")]
    public int refresh = 3000; // ms

    public override string ToString() => JsonSerializer.Serialize<Config>(this, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
}

/// <summary>
/// Use these extension methods to store and retrieve local and roaming app data.
/// More details regarding storing and retrieving app data at https://learn.microsoft.com/en-us/windows/apps/design/app-settings/store-and-retrieve-app-data
/// </summary>
public static class ConfigHelper
{
    private const string FileExtension = ".json";
    private const string FileNameSuffix = "Config";

    #region [Tested Methods]
    public static bool DoesConfigExist()
    {
        return File.Exists(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
    }

    public static string GetConfigFullPath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}");
    }

    public static string ToJson(this Dictionary<string, Dictionary<string, string>> source, bool indented = true)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return System.Text.Json.JsonSerializer.Serialize(source, options);
    }

    public static T? DeserializeFromFile<T>(string filePath, ref string error)
    {
        try
        {
            string jsonString = File.ReadAllText(filePath);
            T? result = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
            error = string.Empty;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{nameof(DeserializeFromFile)}: {ex.Message}");
            error = ex.Message;
            return default(T);
        }
    }

    public static bool SerializeToFile<T>(T obj, string filePath, ref string error)
    {
        if (obj == null || string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
            File.WriteAllText(filePath, jsonString);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{nameof(SerializeToFile)}: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    public static void SaveEncryptedLocalUser(string data)
    {
        if (string.IsNullOrEmpty(data))
            return;

        using (var dest = File.Create(Path.Combine(Directory.GetCurrentDirectory(), "EncryptedUser.txt"), 1024, FileOptions.Encrypted))
        {
            dest.Write(Encoding.UTF8.GetBytes(data), 0, data.Length);
        }
    }

    /// <summary>
    /// Basic config saver.
    /// </summary>
    public static async Task<bool> SaveConfigAsync(Config? obj, bool encrypt = false)
    {
        if (obj == null)
            return false;

        var options = new JsonSerializerOptions { IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

        #region [Synchronous Writing]
        //string outputString = JsonSerializer.Serialize(obj, options);
        //File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"), outputString);
        #endregion

        #region [Asynchronous Writing]
        using FileStream createStream = File.Create(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"), 2048, encrypt ? FileOptions.Encrypted : FileOptions.None);
        await JsonSerializer.SerializeAsync(createStream, obj, options);
        await createStream.DisposeAsync();
        #endregion

        return true;
    }

    /// <summary>
    /// Basic config saver.
    /// </summary>
    public static bool SaveConfig(Config? obj, bool encrypt = false)
    {
        if (obj == null)
            return false;

        var options = new JsonSerializerOptions { IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

        string outputString = JsonSerializer.Serialize(obj, options);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"), outputString);

        return true;
    }

    /// <summary>
    /// Basic config loader.
    /// </summary>
    public static async Task<Config?> LoadConfigAsync()
    {
        var options = new JsonSerializerOptions { IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

        #region [Synchronous Reading]
        //string readString = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
        //Config readData = JsonSerializer.Deserialize<Config>(readString, options) ?? new Config();
        #endregion

        #region [Asynchronous Reading]
        using FileStream openStream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
        return await JsonSerializer.DeserializeAsync<Config>(openStream, options) ?? new Config();
        #endregion
    }

    /// <summary>
    /// Basic config loader.
    /// </summary>
    public static Config? LoadConfig()
    {
        var options = new JsonSerializerOptions { IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

        string readString = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
        Config readData = JsonSerializer.Deserialize<Config>(readString, options) ?? new Config();
        return readData;
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-how-to?pivots=dotnet-5-0
    /// </summary>
    public static async Task JsonSerializingTest(Config? obj, bool encrypt = false)
    {
        if (obj == null)
            return;

        var options = new JsonSerializerOptions { IncludeFields = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

        // Basic serialize from object:
        //string jsonString = JsonSerializer.Serialize<Config>(obj);

        // Basic deserialize to object:
        //obj = JsonSerializer.Deserialize<Config>(jsonString);

        #region [Synchronous Writing]
        //string outputString = JsonSerializer.Serialize(obj, options);
        //File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"), outputString);
        #endregion

        #region [Asynchronous Writing]
        using FileStream createStream = File.Create(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"), 2048, encrypt ? FileOptions.Encrypted : FileOptions.None);
        await JsonSerializer.SerializeAsync(createStream, obj, options);
        await createStream.DisposeAsync();
        #endregion


        #region [Synchronous Reading]
        //string readString = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
        //Config readExample1 = JsonSerializer.Deserialize<Config>(readString, options)!;
        #endregion

        #region [Asynchronous Reading]
        using FileStream openStream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}{FileNameSuffix}{FileExtension}"));
        Config readExample2 = await JsonSerializer.DeserializeAsync<Config>(openStream, options) ?? new Config();
        #endregion
    }

    public static void WriteBinaryToFile(string filePath, byte[] data, FileMode mode = FileMode.Create)
    {
        using (FileStream fs = new FileStream(filePath, mode))
        {
            using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
            {
                writer.Write(data);
            }
        }
    }

    public static byte[] ReadBinaryFromFile(string filePath)
    {
        byte[] result;
        using (FileStream fs = new FileStream(filePath, FileMode.Open))
        {
            using (BinaryReader reader = new BinaryReader(fs, Encoding.UTF8))
            {
                result = reader.ReadBytes((int)fs.Length);
            }
        }
        return result;
    }
    #endregion

    #region [Untested Methods]
    public static async Task SaveAsync<T>(this Windows.Storage.StorageFolder folder, string name, T content)
    {
        var file = await folder.CreateFileAsync(GetFileName(name), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        var fileContent = JsonSerializer.Serialize<T>(content);
        await Windows.Storage.FileIO.WriteTextAsync(file, fileContent);
    }

    public static async ValueTask<T?> ReadAsync<T>(this Windows.Storage.StorageFolder folder, string name)
    {
        if (!File.Exists(Path.Combine(folder.Path, GetFileName(name))))
        {
            return default;
        }

        var file = await folder.GetFileAsync($"{name}{FileExtension}");
        var fileContent = await Windows.Storage.FileIO.ReadTextAsync(file);

        return JsonSerializer.Deserialize<T>(fileContent);
    }

    public static void SaveAsync<T>(this Windows.Storage.ApplicationDataContainer settings, string key, T value)
    {
        settings.SaveString(key, JsonSerializer.Serialize<T>(value));
    }

    public static void SaveString(this Windows.Storage.ApplicationDataContainer settings, string key, string value)
    {
        settings.Values[key] = value;
    }

    public static T? ReadAsync<T>(this Windows.Storage.ApplicationDataContainer settings, string key)
    {
        object? obj;

        if (settings.Values.TryGetValue(key, out obj))
        {
            return JsonSerializer.Deserialize<T>((string)obj);
        }

        return default;
    }

    public static async Task<Windows.Storage.StorageFile> SaveFileAsync(this Windows.Storage.StorageFolder folder, byte[] content, string fileName, Windows.Storage.CreationCollisionOption options = Windows.Storage.CreationCollisionOption.ReplaceExisting)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("File name is null or empty. Specify a valid file name", nameof(fileName));

        Windows.Storage.StorageFile storageFile = await folder.CreateFileAsync(fileName, options);
        await Windows.Storage.FileIO.WriteBytesAsync(storageFile, content);
        return storageFile;
    }

    public static async Task<byte[]?> ReadBytesAsync(this Windows.Storage.StorageFolder folder, string fileName)
    {
        var item = await folder.TryGetItemAsync(fileName).AsTask().ConfigureAwait(false);

        if ((item != null) && item.IsOfType(Windows.Storage.StorageItemTypes.File))
        {
            Windows.Storage.StorageFile? storageFile = await folder.GetFileAsync(fileName);
            var content = await storageFile.ReadStorageBytesAsync();
            return content;
        }

        return null;
    }

    public static async Task<byte[]?> ReadStorageBytesAsync(this Windows.Storage.StorageFile file)
    {
        if (file != null)
        {
            using Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenReadAsync();
            using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            return bytes;
        }

        return null;
    }

    public static bool IsRoamingStorageAvailable(this Windows.Storage.ApplicationData appData)
    {
        return appData.RoamingStorageQuota == 0;
    }

    static string GetFileName(string name)
    {
        return string.Concat(name, FileExtension);
    }
    #endregion
}
