using Tmds.DBus.Protocol;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// The Secret Service's D-Bus calls that <see cref="SecretServiceStore"/> makes (docs/phase-7a-design.md
/// 3.6), written with Tmds.DBus.Protocol's message writer and read with its reader. Each method
/// builds its message at once and returns the call's task; the caller bounds every wait. A writer
/// is a mutable ref struct handed over by <c>CreateMessage</c>, so it is a plain local, as in
/// Tmds.DBus' generated code: a <c>using</c> local would be read-only and copied at every write.
/// </summary>
internal static class SecretServiceCalls
{
    public const string Bus = "org.freedesktop.secrets";

    public const string ServicePath = "/org/freedesktop/secrets";

    public const string LoginCollection = "/org/freedesktop/secrets/collection/login";

    /// <summary>The path that means "no object": no prompt is needed, or none was made.</summary>
    public const string NoObject = "/";

    public const string IsLockedError = "org.freedesktop.Secret.Error.IsLocked";

    public const string NotSupportedError = "org.freedesktop.DBus.Error.NotSupported";

    public const string ContentType = "text/plain; charset=utf-8";

    private const string ServiceInterface = "org.freedesktop.Secret.Service";
    private const string CollectionInterface = "org.freedesktop.Secret.Collection";
    private const string ItemInterface = "org.freedesktop.Secret.Item";
    private const string SessionInterface = "org.freedesktop.Secret.Session";
    private const string PromptInterface = "org.freedesktop.Secret.Prompt";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    private const string DBusName = "org.freedesktop.DBus";
    private const string DBusPath = "/org/freedesktop/DBus";

    /// <summary><c>org.freedesktop.DBus.NameHasOwner</c>.</summary>
    public static Task<bool> NameHasOwnerAsync(DBusConnection connection, string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(DBusName, DBusPath, DBusName, "NameHasOwner", "s", MessageFlags.None);
        writer.WriteString(name);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadBool();
        }, null);
    }

    /// <summary><c>Service.OpenSession("plain", "")</c>: the session's path.</summary>
    public static Task<string> OpenSessionAsync(DBusConnection connection)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "OpenSession", "sv", MessageFlags.None);
        writer.WriteString("plain");
        writer.WriteVariantString(string.Empty);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            reader.ReadVariantValue();
            return reader.ReadObjectPathAsString();
        }, null);
    }

    /// <summary><c>Session.Close()</c>.</summary>
    public static Task CloseSessionAsync(DBusConnection connection, string session) =>
        CallAsync(connection, session, SessionInterface, "Close");

    /// <summary><c>Service.SearchItems(attributes)</c>: the unlocked and the locked items, of every collection.</summary>
    public static Task<(string[] Unlocked, string[] Locked)> SearchItemsAsync(
        DBusConnection connection, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "SearchItems", "a{ss}", MessageFlags.None);
        WriteAttributes(ref writer, attributes);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return (Paths(reader.ReadArrayOfObjectPath()), Paths(reader.ReadArrayOfObjectPath()));
        }, null);
    }

    /// <summary><c>Service.Unlock(objects)</c>: those unlocked at once, and the prompt for the rest.</summary>
    public static Task<(string[] Unlocked, string Prompt)> UnlockAsync(DBusConnection connection, IReadOnlyList<string> objects)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "Unlock", "ao", MessageFlags.None);
        writer.WriteArray(objects.Select(path => new ObjectPath(path)).ToArray());
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return (Paths(reader.ReadArrayOfObjectPath()), reader.ReadObjectPathAsString());
        }, null);
    }

    /// <summary><c>Service.GetSecrets(items, session)</c>: each item's secret value, which the caller clears.</summary>
    public static Task<Dictionary<string, byte[]>> GetSecretsAsync(DBusConnection connection, IReadOnlyList<string> items, string session)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "GetSecrets", "aoo", MessageFlags.None);
        writer.WriteArray(items.Select(path => new ObjectPath(path)).ToArray());
        writer.WriteObjectPath(session);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            var secrets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var end = reader.ReadDictionaryStart();
            while (reader.HasNext(end))
            {
                var item = reader.ReadObjectPathAsString();
                reader.AlignStruct();
                reader.ReadObjectPath();
                reader.ReadArrayOfByte();
                var value = reader.ReadArrayOfByte();
                reader.ReadString();
                secrets[item] = value;
            }

            return secrets;
        }, null);
    }

    /// <summary><c>Service.ReadAlias(name)</c>: the collection's path, or <see cref="NoObject"/>.</summary>
    public static Task<string> ReadAliasAsync(DBusConnection connection, string alias)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "ReadAlias", "s", MessageFlags.None);
        writer.WriteString(alias);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadObjectPathAsString();
        }, null);
    }

    /// <summary><c>Service.CreateCollection({Label}, alias)</c>: the collection, or <see cref="NoObject"/> and a prompt.</summary>
    public static Task<(string Collection, string Prompt)> CreateCollectionAsync(DBusConnection connection, string label, string alias)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, ServicePath, ServiceInterface, "CreateCollection", "a{sv}s", MessageFlags.None);
        writer.WriteDictionary(new Dictionary<string, VariantValue>
        {
            [CollectionInterface + ".Label"] = VariantValue.String(label),
        });
        writer.WriteString(alias);
        return connection.CallMethodAsync(writer.CreateMessage(), ReadPathAndPrompt, null);
    }

    /// <summary>A collection's <c>Locked</c> property.</summary>
    public static Task<bool> IsLockedAsync(DBusConnection connection, string collection)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, collection, PropertiesInterface, "Get", "ss", MessageFlags.None);
        writer.WriteString(CollectionInterface);
        writer.WriteString("Locked");
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            var value = reader.ReadVariantValue();
            return (value.Type == VariantValueType.Variant ? value.GetVariantValue() : value).GetBool();
        }, null);
    }

    /// <summary>
    /// <c>Collection.CreateItem({Label, Attributes}, (session, [], secret, content type), replace: true)</c>:
    /// the item, or <see cref="NoObject"/> and a prompt. The message holds its own copy of
    /// <paramref name="secret"/> once this returns, so the caller may clear it at once.
    /// </summary>
    public static Task<(string Item, string Prompt)> CreateItemAsync(
        DBusConnection connection,
        string collection,
        string session,
        string label,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        byte[] secret)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, collection, CollectionInterface, "CreateItem", "a{sv}(oayays)b", MessageFlags.None);
        var properties = new Dict<string, string>();
        foreach (var (name, value) in attributes)
        {
            properties.Add(name, value);
        }

        writer.WriteDictionary(new Dictionary<string, VariantValue>
        {
            [ItemInterface + ".Label"] = VariantValue.String(label),
            [ItemInterface + ".Attributes"] = properties.AsVariantValue(),
        });
        writer.WriteStructureStart();
        writer.WriteObjectPath(session);
        writer.WriteArray(Array.Empty<byte>());
        writer.WriteArray(secret);
        writer.WriteString(ContentType);
        writer.WriteBool(true);
        return connection.CallMethodAsync(writer.CreateMessage(), ReadPathAndPrompt, null);
    }

    /// <summary><c>Item.Delete()</c>: a prompt, or <see cref="NoObject"/>.</summary>
    public static Task<string> DeleteItemAsync(DBusConnection connection, string item)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, item, ItemInterface, "Delete", null, MessageFlags.None);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadObjectPathAsString();
        }, null);
    }

    /// <summary>Watches <paramref name="prompt"/>'s <c>Completed</c> signal; dispose the result to remove the match.</summary>
    public static ValueTask<IDisposable> WatchPromptAsync(DBusConnection connection, string prompt, TaskCompletionSource<PromptCompletion> completed)
    {
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Interface = PromptInterface,
            Member = "Completed",
            Path = prompt,
        };
        return connection.AddMatchAsync(
            rule,
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                var dismissed = reader.ReadBool();
                return new PromptCompletion(dismissed, ResultPaths(reader.ReadVariantValue()));
            },
            (Notification<PromptCompletion> notification) =>
            {
                if (notification.HasValue)
                {
                    completed.TrySetResult(notification.Value);
                }
                else if (notification.Exception is { } exception)
                {
                    completed.TrySetException(exception);
                }
            },
            false,
            ObserverFlags.None,
            null);
    }

    /// <summary><c>Prompt.Prompt("")</c>: shows the service's dialog; <c>Completed</c> follows.</summary>
    public static Task ShowPromptAsync(DBusConnection connection, string prompt)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, prompt, PromptInterface, "Prompt", "s", MessageFlags.None);
        writer.WriteString(string.Empty);
        return connection.CallMethodAsync(writer.CreateMessage());
    }

    /// <summary><c>Prompt.Dismiss()</c>.</summary>
    public static Task DismissPromptAsync(DBusConnection connection, string prompt) =>
        CallAsync(connection, prompt, PromptInterface, "Dismiss");

    private static Task CallAsync(DBusConnection connection, string path, string @interface, string member)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Bus, path, @interface, member, null, MessageFlags.None);
        return connection.CallMethodAsync(writer.CreateMessage());
    }

    private static (string Path, string Prompt) ReadPathAndPrompt(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        return (reader.ReadObjectPathAsString(), reader.ReadObjectPathAsString());
    }

    private static void WriteAttributes(ref MessageWriter writer, IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        var start = writer.WriteDictionaryStart();
        foreach (var (name, value) in attributes)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteString(value);
        }

        writer.WriteDictionaryEnd(start);
    }

    private static string[] Paths(ObjectPath[] paths) => [.. paths.Select(path => path.ToString())];

    // A prompt's result: an object path (a created item or collection) or an array of them (unlocked objects).
    private static string[] ResultPaths(VariantValue value)
    {
        if (value.Type == VariantValueType.Variant)
        {
            value = value.GetVariantValue();
        }

        if (value.Type == VariantValueType.ObjectPath)
        {
            return [value.GetObjectPathAsString()];
        }

        if (value.Type == VariantValueType.Array && value.ItemType == VariantValueType.ObjectPath)
        {
            var paths = new string[value.Count];
            for (var i = 0; i < paths.Length; i++)
            {
                paths[i] = value.GetItem(i).GetObjectPathAsString();
            }

            return paths;
        }

        return [];
    }
}

/// <summary>A prompt's <c>Completed</c> signal: whether it was dismissed, and the object paths of its result.</summary>
internal sealed record PromptCompletion(bool Dismissed, IReadOnlyList<string> Paths);
