namespace Domain.Models
{
    public class ShardKey : IEquatable<ShardKey>
    {
        public string Value { get; }

        public ShardKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Shard key cannot be null or empty", nameof(value));

            Value = value;
        }

        public bool Equals(ShardKey? other) => other is not null && Value == other.Value;
        public override bool Equals(object? obj) => Equals(obj as ShardKey);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static implicit operator string(ShardKey shardKey) => shardKey.Value;
        public static implicit operator ShardKey(string value) => new(value);
    }
}
